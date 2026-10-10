using System;
using System.Text;
using UnityEngine;

public sealed class MapSessionSnapshot
{
    public const int MaximumContentBytes = 512 * 1024;

    public uint Generation { get; }
    public string TransferId { get; }
    public string MapId { get; }
    public string Version { get; }
    public string MapName { get; }
    public string AuthorName { get; }
    public byte[] Content { get; }
    public string ContentHash { get; }
    public int ByteLength => Content.Length;

    private MapSessionSnapshot(uint generation, MapData data, byte[] content)
    {
        Generation = generation;
        TransferId = Guid.NewGuid().ToString("N");
        MapId = data.mapId;
        Version = data.version;
        MapName = data.mapName ?? string.Empty;
        AuthorName = data.authorName ?? string.Empty;
        Content = content;
        ContentHash = MapContentHash.Compute(content);
    }

    public static bool TryCreate(
        uint generation,
        string sourceJson,
        int maximumBytes,
        MapEditorPalette palette,
        out MapSessionSnapshot snapshot,
        out string error)
    {
        snapshot = null;
        MapData data = MapDataRepository.FromJson(sourceJson);
        if (data == null)
        {
            error = "선택된 맵 JSON을 읽을 수 없습니다.";
            return false;
        }

        MapValidationReport report = new MapDataValidator(palette).Validate(data);
        if (report.HasErrors)
        {
            error = "선택된 맵이 플레이 가능성 검증을 통과하지 못했습니다.";
            return false;
        }

        string canonicalJson = MapDataRepository.ToJson(data);
        byte[] content = Encoding.UTF8.GetBytes(canonicalJson);
        if (content.Length == 0 || content.Length > maximumBytes)
        {
            error = $"맵 크기가 세션 전송 한계를 벗어났습니다: {content.Length}/{maximumBytes}";
            return false;
        }

        snapshot = new MapSessionSnapshot(generation, data, content);
        error = null;
        return true;
    }
}

public static class MapSessionCache
{
    private static string mapId;
    private static string contentHash;
    private static byte[] content;
    private static MapData mapData;

    public static bool TryGet(
        string requestedMapId,
        string requestedHash,
        out MapData data)
    {
        data = null;
        if (mapData == null ||
            mapId != requestedMapId ||
            contentHash != requestedHash)
            return false;

        data = mapData;
        return true;
    }

    public static bool TryStore(
        string requestedMapId,
        string requestedHash,
        byte[] source,
        MapEditorPalette palette,
        out string error)
    {
        if (!MapSessionContentValidator.TryValidate(
                requestedMapId, requestedHash, source, palette,
                out MapData parsed, out error))
            return false;

        mapId = requestedMapId;
        contentHash = requestedHash;
        content = (byte[])source.Clone();
        mapData = parsed;
        error = null;
        return true;
    }

    public static bool TryGetBytes(string requestedMapId, string requestedHash, out byte[] bytes)
    {
        bytes = null;
        if (content == null || mapId != requestedMapId || contentHash != requestedHash)
            return false;

        bytes = (byte[])content.Clone();
        return true;
    }

    public static void Clear()
    {
        mapId = null;
        contentHash = null;
        content = null;
        mapData = null;
    }
}

public sealed class ServerMapSessionStore
{
    private string mapId;
    private string contentHash;
    private byte[] content;
    private MapData mapData;

    public bool TryStore(MapSessionSnapshot snapshot, MapEditorPalette palette, out string error)
    {
        if (snapshot == null)
        {
            error = "서버 세션 스냅샷이 없습니다.";
            return false;
        }

        if (!MapSessionContentValidator.TryValidate(
                snapshot.MapId, snapshot.ContentHash, snapshot.Content,
                palette, out MapData parsed, out error))
            return false;

        mapId = snapshot.MapId;
        contentHash = snapshot.ContentHash;
        content = (byte[])snapshot.Content.Clone();
        mapData = parsed;
        return true;
    }

    public bool TryGet(string requestedMapId, string requestedHash, out MapData data)
    {
        data = null;
        if (mapData == null || mapId != requestedMapId || contentHash != requestedHash)
            return false;

        data = mapData;
        return true;
    }

    public void Clear()
    {
        mapId = null;
        contentHash = null;
        content = null;
        mapData = null;
    }
}

public static class MapSessionContentValidator
{
    public static bool TryValidate(
        string requestedMapId,
        string requestedHash,
        byte[] source,
        MapEditorPalette palette,
        out MapData parsed,
        out string error)
    {
        parsed = null;
        if (source == null || MapContentHash.Compute(source) != requestedHash)
        {
            error = "수신 맵의 콘텐츠 해시가 manifest와 일치하지 않습니다.";
            return false;
        }

        string json;
        try
        {
            json = new UTF8Encoding(false, true).GetString(source);
        }
        catch (DecoderFallbackException exception)
        {
            Debug.LogWarning($"수신 맵 UTF-8 디코딩을 거부했습니다: {exception.Message}");
            error = "수신 맵이 올바른 UTF-8 형식이 아닙니다.";
            return false;
        }

        parsed = MapDataRepository.FromJson(json);
        if (parsed == null || parsed.mapId != requestedMapId ||
            parsed.version != MapData.CurrentVersion)
        {
            error = "수신 맵의 버전 또는 MapId가 manifest와 일치하지 않습니다.";
            return false;
        }

        MapValidationReport report = new MapDataValidator(palette).Validate(parsed);
        if (report.HasErrors)
        {
            parsed = null;
            error = "수신 맵이 플레이 가능성 검증을 통과하지 못했습니다.";
            return false;
        }

        error = null;
        return true;
    }
}
