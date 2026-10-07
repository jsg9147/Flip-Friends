using System;
using NUnit.Framework;
using UnityEngine;

public sealed class MapEditorDraftStoreTests
{
    [TearDown]
    public void TearDown()
    {
        MapEditorDraftStore.Clear();
    }

    [Test]
    public void TryTake_보관한편집상태를파일명과함께돌려준다()
    {
        MapData source = CreateMap();

        MapEditorDraftStore.Store(source, "저장된 파일");

        Assert.That(MapEditorDraftStore.TryTake(out MapData data, out string fileName), Is.True);
        Assert.That(data.mapId, Is.EqualTo(source.mapId));
        Assert.That(data.mapName, Is.EqualTo(source.mapName));
        Assert.That(data.objects.Count, Is.EqualTo(1));
        Assert.That(data.objects[0].position.ToVector3(), Is.EqualTo(new Vector3(2f, 3f, 0f)));
        Assert.That(fileName, Is.EqualTo("저장된 파일"));
    }

    [Test]
    public void TryTake_저장한적없는맵은파일명이null이다()
    {
        MapEditorDraftStore.Store(CreateMap(), null);

        Assert.That(MapEditorDraftStore.TryTake(out _, out string fileName), Is.True);
        Assert.That(fileName, Is.Null);
    }

    [Test]
    public void TryTake_한번꺼내면비워진다()
    {
        MapEditorDraftStore.Store(CreateMap(), "파일");
        Assert.That(MapEditorDraftStore.TryTake(out _, out _), Is.True);

        Assert.That(MapEditorDraftStore.HasDraft, Is.False);
        Assert.That(MapEditorDraftStore.TryTake(out MapData data, out string fileName), Is.False);
        Assert.That(data, Is.Null);
        Assert.That(fileName, Is.Null);
    }

    [Test]
    public void Store_보관후원본을바꿔도보관본은그대로다()
    {
        MapData source = CreateMap();
        MapEditorDraftStore.Store(source, null);

        source.mapName = "바뀐 이름";
        source.objects.Clear();

        Assert.That(MapEditorDraftStore.TryTake(out MapData data, out _), Is.True);
        Assert.That(data.mapName, Is.EqualTo("편집 중인 맵"));
        Assert.That(data.objects.Count, Is.EqualTo(1));
    }

    [Test]
    public void Store_null은거부한다()
    {
        Assert.Throws<ArgumentNullException>(() => MapEditorDraftStore.Store(null, null));
        Assert.That(MapEditorDraftStore.HasDraft, Is.False);
    }

    private static MapData CreateMap()
    {
        var map = new MapData("편집 중인 맵", "테스터");
        map.objects.Add(new PlacedObjectData(
            "terrain.block",
            new Vector3(2f, 3f, 0f),
            0f,
            Vector3.one));
        return map;
    }
}
