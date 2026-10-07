using System;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class MapDataRepositoryTests
{
    private const string LegacyMapJson =
        "{\"mapName\":\"옛 맵\",\"authorName\":\"테스터\",\"version\":\"2.0\",\"objects\":[]}";

    private string saveDirectory;

    [SetUp]
    public void SetUp()
    {
        saveDirectory = Path.Combine(
            Path.GetTempPath(),
            "FlipFriendsMapRepositoryTests",
            Guid.NewGuid().ToString("N"));
        MapDataRepository.SaveDirectoryOverride = saveDirectory;
    }

    [TearDown]
    public void TearDown()
    {
        MapDataRepository.SaveDirectoryOverride = null;
        if (Directory.Exists(saveDirectory))
            Directory.Delete(saveDirectory, recursive: true);
    }

    [Test]
    public void TryCreate_저장한맵을그대로불러온다()
    {
        MapData source = CreateMap("왕복 맵");
        source.objects.Add(new PlacedObjectData(
            "terrain.block",
            new Vector3(1.5f, -2f, 0f),
            90f,
            new Vector3(-1f, 1f, 1f)));

        Assert.That(MapDataRepository.TryCreate(source, false, out MapSaveResult result), Is.True);
        Assert.That(result.IsSuccess, Is.True);

        MapData loaded = MapDataRepository.Load("왕복 맵");
        Assert.That(loaded.mapId, Is.EqualTo(source.mapId));
        Assert.That(loaded.authorName, Is.EqualTo("테스터"));
        Assert.That(loaded.version, Is.EqualTo(MapData.CurrentVersion));
        Assert.That(loaded.objects.Count, Is.EqualTo(1));
        PlacedObjectData placed = loaded.objects[0];
        Assert.That(placed.prefabID, Is.EqualTo("terrain.block"));
        Assert.That(placed.position.ToVector3(), Is.EqualTo(new Vector3(1.5f, -2f, 0f)));
        Assert.That(placed.rotation, Is.EqualTo(90f));
        Assert.That(placed.scale.ToVector3(), Is.EqualTo(new Vector3(-1f, 1f, 1f)));
    }

    [Test]
    public void TryCreate_맵이름앞뒤공백을제거해저장한다()
    {
        MapData source = CreateMap("  공백 맵  ");

        Assert.That(MapDataRepository.TryCreate(source, false, out _), Is.True);

        Assert.That(source.mapName, Is.EqualTo("공백 맵"));
        Assert.That(File.Exists(MapPath("공백 맵")), Is.True);
    }

    [Test]
    public void TryCreate_이미저장된MapId는거부한다()
    {
        MapData first = CreateMap("첫째");
        Assert.That(MapDataRepository.TryCreate(first, false, out _), Is.True);
        MapData second = CreateMap("둘째");
        second.mapId = first.mapId;

        Assert.That(MapDataRepository.TryCreate(second, false, out MapSaveResult result), Is.False);

        Assert.That(result.FailureKind, Is.EqualTo(MapSaveFailureKind.DuplicateMapId));
        Assert.That(File.Exists(MapPath("둘째")), Is.False);
    }

    [Test]
    public void TryCreate_같은파일명은허락없이덮어쓰지않는다()
    {
        MapData first = CreateMap("같은 이름");
        Assert.That(MapDataRepository.TryCreate(first, false, out _), Is.True);
        MapData second = CreateMap("같은 이름");

        Assert.That(MapDataRepository.TryCreate(second, false, out MapSaveResult result), Is.False);
        Assert.That(result.FailureKind, Is.EqualTo(MapSaveFailureKind.NameConflict));
        Assert.That(MapDataRepository.Load("같은 이름").mapId, Is.EqualTo(first.mapId));

        Assert.That(MapDataRepository.TryCreate(second, true, out _), Is.True);
        Assert.That(MapDataRepository.Load("같은 이름").mapId, Is.EqualTo(second.mapId));
    }

    [Test]
    public void TryUpdate_이름이바뀌면파일을옮긴다()
    {
        MapData map = CreateMap("이전 이름");
        Assert.That(MapDataRepository.TryCreate(map, false, out _), Is.True);
        map.mapName = "새 이름";

        Assert.That(MapDataRepository.TryUpdate("이전 이름", map, out MapSaveResult result), Is.True);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(File.Exists(MapPath("이전 이름")), Is.False);
        Assert.That(MapDataRepository.Load("새 이름").mapId, Is.EqualTo(map.mapId));
    }

    [Test]
    public void TryUpdate_다른맵의파일은덮어쓰지않는다()
    {
        Assert.That(MapDataRepository.TryCreate(CreateMap("원본"), false, out _), Is.True);
        MapData other = CreateMap("원본");

        Assert.That(MapDataRepository.TryUpdate("원본", other, out MapSaveResult result), Is.False);

        Assert.That(result.FailureKind, Is.EqualTo(MapSaveFailureKind.TargetMismatch));
    }

    [Test]
    public void TryUpdate_이름변경대상이이미있으면거부한다()
    {
        MapData map = CreateMap("내 맵");
        Assert.That(MapDataRepository.TryCreate(map, false, out _), Is.True);
        Assert.That(MapDataRepository.TryCreate(CreateMap("남의 맵"), false, out _), Is.True);
        map.mapName = "남의 맵";

        Assert.That(MapDataRepository.TryUpdate("내 맵", map, out MapSaveResult result), Is.False);

        Assert.That(result.FailureKind, Is.EqualTo(MapSaveFailureKind.NameConflict));
        Assert.That(File.Exists(MapPath("내 맵")), Is.True);
    }

    [Test]
    public void TryImport_같은MapId나파일명이있으면거부한다()
    {
        MapData stored = CreateMap("공유 맵");
        Assert.That(MapDataRepository.TryCreate(stored, false, out _), Is.True);
        MapData sameId = CreateMap("다른 이름");
        sameId.mapId = stored.mapId;

        Assert.That(MapDataRepository.TryImport(sameId, out MapSaveResult idResult), Is.False);
        Assert.That(MapDataRepository.TryImport(CreateMap("공유 맵"), out MapSaveResult nameResult), Is.False);

        Assert.That(idResult.FailureKind, Is.EqualTo(MapSaveFailureKind.DuplicateMapId));
        Assert.That(nameResult.FailureKind, Is.EqualTo(MapSaveFailureKind.NameConflict));
    }

    [TestCase("")]
    [TestCase("   ")]
    [TestCase("..")]
    [TestCase("../밖")]
    [TestCase("..\\밖")]
    [TestCase("폴더/맵")]
    [TestCase("폴더\\맵")]
    [TestCase("C:\\맵")]
    [TestCase("맵?")]
    [TestCase("마침표.")]
    [TestCase("CON")]
    [TestCase("com1")]
    [TestCase("LPT9.json")]
    public void TryNormalizeMapName_경로이탈과사용불가이름을거부한다(string mapName)
    {
        Assert.That(MapDataRepository.TryNormalizeMapName(mapName, out _, out string error), Is.False);
        Assert.That(error, Is.Not.Empty);
    }

    [Test]
    public void TryCreate_경로이탈이름은저장폴더밖에쓰지않는다()
    {
        MapData map = CreateMap("../escape");

        Assert.That(MapDataRepository.TryCreate(map, false, out MapSaveResult result), Is.False);

        Assert.That(result.FailureKind, Is.EqualTo(MapSaveFailureKind.InvalidName));
        Assert.That(File.Exists(Path.Combine(saveDirectory, "..", "escape.json")), Is.False);
    }

    [Test]
    public void TryLoadWithJson_경로이탈이름은InvalidName이다()
    {
        Assert.That(
            MapDataRepository.TryLoadWithJson("../밖", out _, out _, out MapRepositoryFailure failure),
            Is.False);
        Assert.That(failure.Kind, Is.EqualTo(MapRepositoryFailureKind.InvalidName));
    }

    [Test]
    public void TryLoadWithJson_없는파일은MissingFile이다()
    {
        Directory.CreateDirectory(saveDirectory);

        Assert.That(
            MapDataRepository.TryLoadWithJson("없는 맵", out _, out _, out MapRepositoryFailure failure),
            Is.False);
        Assert.That(failure.Kind, Is.EqualTo(MapRepositoryFailureKind.MissingFile));
    }

    [Test]
    public void TryLoadWithJson_빈파일은InvalidJson이다()
    {
        WriteRawMap("빈 파일", "   ");

        Assert.That(
            MapDataRepository.TryLoadWithJson("빈 파일", out _, out _, out MapRepositoryFailure failure),
            Is.False);
        Assert.That(failure.Kind, Is.EqualTo(MapRepositoryFailureKind.InvalidJson));
    }

    [Test]
    public void TryLoadWithJson_손상된JSON은InvalidJson이다()
    {
        WriteRawMap("손상", "{ \"mapName\": ");
        LogAssert.Expect(LogType.Error, new Regex("JSON 형식이 올바르지 않습니다"));
        LogAssert.Expect(LogType.Exception, new Regex("."));

        Assert.That(
            MapDataRepository.TryLoadWithJson("손상", out MapData data, out _, out MapRepositoryFailure failure),
            Is.False);
        Assert.That(data, Is.Null);
        Assert.That(failure.Kind, Is.EqualTo(MapRepositoryFailureKind.InvalidJson));
    }

    [Test]
    public void TryLoadWithJson_이전버전을변환하고파일에다시쓴다()
    {
        WriteRawMap("옛 맵", LegacyMapJson);

        Assert.That(
            MapDataRepository.TryLoadWithJson("옛 맵", out MapData data, out string json, out _),
            Is.True);

        Assert.That(data.version, Is.EqualTo(MapData.CurrentVersion));
        Assert.That(MapData.TryNormalizeMapId(data.mapId, out string normalized), Is.True);
        Assert.That(data.mapId, Is.EqualTo(normalized));
        Assert.That(File.ReadAllText(MapPath("옛 맵")), Is.EqualTo(json));
        Assert.That(MapDataRepository.Load("옛 맵").mapId, Is.EqualTo(data.mapId));
    }

    [Test]
    public void TryLoadWithJson_대문자MapId를정규형으로바꿔저장한다()
    {
        MapData map = CreateMap("대문자");
        string upperId = map.mapId.ToUpperInvariant();
        map.mapId = upperId;
        WriteRawMap("대문자", JsonUtility.ToJson(map));

        Assert.That(MapDataRepository.TryLoadWithJson("대문자", out MapData data, out _, out _), Is.True);

        Assert.That(data.mapId, Is.EqualTo(upperId.ToLowerInvariant()));
        Assert.That(File.ReadAllText(MapPath("대문자")), Does.Not.Contain(upperId));
    }

    [Test]
    public void TryLoadWithJson_지원하지않는버전은UnsupportedVersion이다()
    {
        WriteRawMap("미래", LegacyMapJson.Replace("\"2.0\"", "\"9.9\""));

        Assert.That(
            MapDataRepository.TryLoadWithJson("미래", out _, out _, out MapRepositoryFailure failure),
            Is.False);
        Assert.That(failure.Kind, Is.EqualTo(MapRepositoryFailureKind.UnsupportedVersion));
    }

    [Test]
    public void TryLoadWithJson_현재버전의잘못된MapId는교체하지않고거부한다()
    {
        MapData map = CreateMap("잘못된 ID");
        map.mapId = "not-a-guid";
        string json = JsonUtility.ToJson(map);
        WriteRawMap("잘못된 ID", json);

        Assert.That(
            MapDataRepository.TryLoadWithJson("잘못된 ID", out _, out _, out MapRepositoryFailure failure),
            Is.False);

        Assert.That(failure.Kind, Is.EqualTo(MapRepositoryFailureKind.InvalidMapId));
        Assert.That(File.ReadAllText(MapPath("잘못된 ID")), Is.EqualTo(json));
    }

    [Test]
    public void TryLoadById_MapId로파일을찾고중복이면거부한다()
    {
        MapData map = CreateMap("찾을 맵");
        Assert.That(MapDataRepository.TryCreate(map, false, out _), Is.True);

        Assert.That(
            MapDataRepository.TryLoadById(map.mapId, out string fileName, out _, out _, out _),
            Is.True);
        Assert.That(fileName, Is.EqualTo("찾을 맵"));

        File.Copy(MapPath("찾을 맵"), MapPath("복사본"));
        Assert.That(
            MapDataRepository.TryLoadById(map.mapId, out _, out _, out _, out MapRepositoryFailure failure),
            Is.False);
        Assert.That(failure.Kind, Is.EqualTo(MapRepositoryFailureKind.DuplicateMapId));
    }

    [Test]
    public void GetAllMapNames_JSON파일만대소문자무시순서로돌려준다()
    {
        WriteRawMap("b맵", LegacyMapJson);
        WriteRawMap("A맵", LegacyMapJson);
        File.WriteAllText(Path.Combine(saveDirectory, "메모.txt"), "무시");

        Assert.That(MapDataRepository.GetAllMapNames(), Is.EqualTo(new[] { "A맵", "b맵" }));
    }

    [Test]
    public void Delete_저장된맵을지운다()
    {
        Assert.That(MapDataRepository.TryCreate(CreateMap("지울 맵"), false, out _), Is.True);

        Assert.That(MapDataRepository.Delete("지울 맵"), Is.True);

        Assert.That(MapDataRepository.TryExists("지울 맵", out bool exists), Is.True);
        Assert.That(exists, Is.False);
    }

    private static MapData CreateMap(string mapName) => new(mapName, "테스터");

    private string MapPath(string mapName) => Path.Combine(saveDirectory, $"{mapName}.json");

    private void WriteRawMap(string mapName, string json)
    {
        Directory.CreateDirectory(saveDirectory);
        File.WriteAllText(MapPath(mapName), json);
    }
}
