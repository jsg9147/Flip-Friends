using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

public sealed class MapDataValidatorTests
{
    private const string SpawnId = "essential.player_spawn";
    private const string FinishId = "essential.finish";
    private const string BlockId = "terrain.block";

    [Test]
    public void Validate_시작점과도착점이있으면오류가없다()
    {
        MapValidationReport report = CreateValidator().Validate(CreateMinimalMap());

        Assert.That(report.Issues, Is.Empty);
        Assert.That(report.CanStartPlay, Is.True);
    }

    [Test]
    public void Validate_시작점이없으면오류다()
    {
        MapData map = CreateMap(Place(FinishId, 1f, 0f));

        MapValidationReport report = CreateValidator().Validate(map);

        AssertSingleError(report, SpawnId);
    }

    [Test]
    public void Validate_도착점이없으면오류다()
    {
        MapData map = CreateMap(Place(SpawnId, 0f, 0f));

        MapValidationReport report = CreateValidator().Validate(map);

        AssertSingleError(report, FinishId);
    }

    [Test]
    public void Validate_설정의최소시작점수를적용한다()
    {
        var palette = new FakePalette();
        palette.Settings.minimumPlayerSpawnCount = 2;

        MapValidationReport report = new MapDataValidator(palette).Validate(CreateMinimalMap());

        AssertSingleError(report, SpawnId);
    }

    [Test]
    public void Validate_팔레트규칙의최소개수가도착점요구치를높인다()
    {
        var palette = new FakePalette();
        palette.Entries[FinishId].validationRule.minimumCount = 2;

        MapValidationReport report = new MapDataValidator(palette).Validate(CreateMinimalMap());

        AssertSingleError(report, FinishId);
    }

    [Test]
    public void Validate_등록되지않은팔레트ID는오브젝트번호와함께오류다()
    {
        MapData map = CreateMinimalMap();
        map.objects.Add(Place("missing.object", 5f, 5f));

        MapValidationReport report = CreateValidator().Validate(map);

        MapValidationIssue issue = AssertSingleError(report, "missing.object");
        Assert.That(issue.ObjectIndex, Is.EqualTo(2));
    }

    [Test]
    public void Validate_빈팔레트ID와null오브젝트는오류다()
    {
        MapData map = CreateMinimalMap();
        map.objects.Add(Place("", 5f, 5f));
        map.objects.Add(null);

        MapValidationReport report = CreateValidator().Validate(map);

        Assert.That(report.Issues.Select(issue => issue.ObjectIndex), Is.EqualTo(new[] { 2, 3 }));
        Assert.That(report.HasErrors, Is.True);
    }

    [Test]
    public void Validate_팔레트가없으면오류다()
    {
        MapValidationReport report = new MapDataValidator(null).Validate(CreateMinimalMap());

        Assert.That(report.Issues.Count, Is.EqualTo(1));
        Assert.That(report.CanStartPlay, Is.False);
    }

    [Test]
    public void Validate_맵데이터가없으면MapId와데이터오류를함께보고한다()
    {
        MapValidationReport report = CreateValidator().Validate(null);

        Assert.That(report.Issues.Count, Is.EqualTo(2));
        Assert.That(report.HasErrors, Is.True);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("not-a-guid")]
    [TestCase("0123456789ABCDEF0123456789ABCDEF")]
    [TestCase("01234567-89ab-cdef-0123-456789abcdef")]
    public void Validate_정규GUID가아닌MapId는오류다(string mapId)
    {
        MapData map = CreateMinimalMap();
        map.mapId = mapId;

        MapValidationReport report = CreateValidator().Validate(map);

        AssertSingleError(report, null);
    }

    [TestCase(51f, 0f)]
    [TestCase(-51f, 0f)]
    [TestCase(0f, 31f)]
    [TestCase(0f, -31f)]
    [TestCase(float.NaN, 0f)]
    [TestCase(float.PositiveInfinity, 0f)]
    public void Validate_맵경계밖이거나유한하지않은좌표는오류다(float x, float y)
    {
        MapData map = CreateMinimalMap();
        map.objects.Add(Place(BlockId, x, y));

        MapValidationReport report = CreateValidator().Validate(map);

        Assert.That(AssertSingleError(report, BlockId).ObjectIndex, Is.EqualTo(2));
    }

    [Test]
    public void Validate_맵경계선위의좌표는허용한다()
    {
        MapData map = CreateMinimalMap();
        map.objects.Add(Place(BlockId, 50f, -30f));

        Assert.That(CreateValidator().Validate(map).Issues, Is.Empty);
    }

    [Test]
    public void Validate_최대개수를넘으면오류다()
    {
        var palette = new FakePalette();
        palette.Entries[BlockId].validationRule.maximumCount = 1;
        MapData map = CreateMinimalMap();
        map.objects.Add(Place(BlockId, 3f, 0f));
        map.objects.Add(Place(BlockId, 4f, 0f));

        MapValidationReport report = new MapDataValidator(palette).Validate(map);

        AssertSingleError(report, BlockId);
    }

    [Test]
    public void Validate_겹침금지오브젝트가같은위치면경고다()
    {
        MapData map = CreateMinimalMap();
        map.objects.Add(Place(BlockId, 3f, 0f));
        map.objects.Add(Place(BlockId, 3.005f, 0f));

        MapValidationReport report = CreateValidator().Validate(map);

        Assert.That(report.Issues.Count, Is.EqualTo(1));
        Assert.That(report.Issues[0].Severity, Is.EqualTo(MapValidationSeverity.Warning));
        Assert.That(report.CanStartPlay, Is.True);
    }

    [Test]
    public void Validate_치명적겹침은오류다()
    {
        var palette = new FakePalette();
        palette.Entries[BlockId].validationRule.overlapIsCritical = true;
        MapData map = CreateMinimalMap();
        map.objects.Add(Place(BlockId, 3f, 0f));
        map.objects.Add(Place(BlockId, 3f, 0f));

        MapValidationReport report = new MapDataValidator(palette).Validate(map);

        AssertSingleError(report, BlockId);
    }

    [Test]
    public void Validate_허용오차보다멀거나겹침을허용하면문제가없다()
    {
        var palette = new FakePalette();
        palette.Entries[SpawnId].validationRule.disallowPositionOverlap = false;
        MapData map = CreateMinimalMap();
        map.objects.Add(Place(BlockId, 3f, 0f));
        map.objects.Add(Place(BlockId, 3.02f, 0f));
        map.objects.Add(Place(SpawnId, 6f, 0f));
        map.objects.Add(Place(SpawnId, 6f, 0f));

        Assert.That(new MapDataValidator(palette).Validate(map).Issues, Is.Empty);
    }

    private static MapDataValidator CreateValidator() => new(new FakePalette());

    private static MapData CreateMinimalMap() =>
        CreateMap(Place(SpawnId, 0f, 0f), Place(FinishId, 1f, 0f));

    private static MapData CreateMap(params PlacedObjectData[] objects)
    {
        var map = new MapData("테스트 맵", "테스터");
        map.objects.AddRange(objects);
        return map;
    }

    private static PlacedObjectData Place(string prefabId, float x, float y) =>
        new(prefabId, new Vector3(x, y, 0f), 0f, Vector3.one);

    private static MapValidationIssue AssertSingleError(
        MapValidationReport report,
        string prefabId)
    {
        Assert.That(report.Issues.Count, Is.EqualTo(1), DescribeIssues(report));
        MapValidationIssue issue = report.Issues[0];
        Assert.That(issue.Severity, Is.EqualTo(MapValidationSeverity.Error));
        Assert.That(issue.PrefabID, Is.EqualTo(prefabId));
        Assert.That(report.CanStartPlay, Is.False);
        return issue;
    }

    private static string DescribeIssues(MapValidationReport report) =>
        string.Join("\n", report.Issues.Select(issue => issue.Message));

    private sealed class FakePalette : IMapPaletteLookup
    {
        public readonly Dictionary<string, PaletteEntry> Entries = new()
        {
            [SpawnId] = CreateEntry(SpawnId, MapObjectCategory.Essential),
            [FinishId] = CreateEntry(FinishId, MapObjectCategory.Essential),
            [BlockId] = CreateEntry(BlockId, MapObjectCategory.Terrain)
        };

        public MapValidationSettings Settings { get; } = new();

        public MapValidationSettings ValidationSettings => Settings;

        public bool TryGetEntry(string prefabID, out PaletteEntry entry)
        {
            entry = null;
            return prefabID != null && Entries.TryGetValue(prefabID, out entry);
        }

        private static PaletteEntry CreateEntry(string id, MapObjectCategory category) =>
            new() { id = id, displayName = id, category = category };
    }
}
