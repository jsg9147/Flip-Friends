using System.Collections.Generic;
using UnityEngine;

public sealed class MapDataValidator
{
    private const string PlayerSpawnID = "essential.player_spawn";
    private const string FinishID = "essential.finish";

    private readonly MapEditorPalette palette;
    private readonly MapValidationSettings settings;

    public MapDataValidator(MapEditorPalette palette)
    {
        this.palette = palette;
        settings = palette?.validationSettings ?? new MapValidationSettings();
    }

    public MapValidationReport Validate(MapData mapData)
    {
        var issues = new List<MapValidationIssue>();
        ValidateMapId(mapData, issues);
        if (!TryGetObjects(mapData, issues, out List<PlacedObjectData> objects))
            return new MapValidationReport(issues);

        ValidateRequiredObjects(objects, issues);
        ValidateEntries(objects, issues);
        ValidateOverlaps(objects, issues);
        return new MapValidationReport(issues);
    }

    private void ValidateMapId(
        MapData mapData,
        List<MapValidationIssue> issues)
    {
        if (mapData != null &&
            MapData.TryNormalizeMapId(mapData.mapId, out string normalizedMapId) &&
            mapData.mapId == normalizedMapId)
            return;

        issues.Add(new MapValidationIssue(
            MapValidationSeverity.Error,
            "MapId가 없거나 정규 GUID 형식이 아닙니다."));
    }

    private bool TryGetObjects(
        MapData mapData,
        List<MapValidationIssue> issues,
        out List<PlacedObjectData> objects)
    {
        objects = mapData?.objects;
        if (mapData != null && objects != null && palette != null) return true;

        string message = palette == null
            ? "팔레트가 없어 맵을 검증할 수 없습니다."
            : "맵 데이터 또는 오브젝트 목록이 없습니다.";
        issues.Add(new MapValidationIssue(MapValidationSeverity.Error, message));
        return false;
    }

    private void ValidateRequiredObjects(
        List<PlacedObjectData> objects,
        List<MapValidationIssue> issues)
    {
        int spawnCount = CountByID(objects, PlayerSpawnID);
        int minimumSpawnCount = Mathf.Max(1, settings.minimumPlayerSpawnCount);
        if (spawnCount < minimumSpawnCount)
            AddMinimumCountError(
                issues,
                PlayerSpawnID,
                "플레이어 시작 지점",
                spawnCount,
                minimumSpawnCount);

        int finishCount = CountByID(objects, FinishID);
        int minimumFinishCount = GetMinimumCount(FinishID, 1);
        if (finishCount < minimumFinishCount)
            issues.Add(new MapValidationIssue(
                MapValidationSeverity.Error,
                $"도착 지점은 최소 {minimumFinishCount}개가 필요하지만 현재 {finishCount}개입니다.",
                FinishID));
    }

    private int GetMinimumCount(string prefabID, int fallback)
    {
        if (!palette.TryGetEntry(prefabID, out PaletteEntry entry))
            return fallback;

        return Mathf.Max(fallback, entry.validationRule?.minimumCount ?? 0);
    }

    private int CountByID(List<PlacedObjectData> objects, string prefabID)
    {
        int count = 0;
        foreach (PlacedObjectData data in objects)
        {
            if (data?.prefabID == prefabID)
                count++;
        }

        return count;
    }

    private void AddMinimumCountError(
        List<MapValidationIssue> issues,
        string prefabID,
        string displayName,
        int actualCount,
        int minimumCount)
    {
        issues.Add(new MapValidationIssue(
            MapValidationSeverity.Error,
            $"{displayName}은(는) 최소 {minimumCount}개가 필요하지만 현재 {actualCount}개입니다.",
            prefabID));
    }

    private void ValidateEntries(
        List<PlacedObjectData> objects,
        List<MapValidationIssue> issues)
    {
        var counts = new Dictionary<string, int>();
        for (int index = 0; index < objects.Count; index++)
        {
            PlacedObjectData data = objects[index];
            if (!TryGetEntry(data, index, issues, out PaletteEntry entry)) continue;

            counts.TryGetValue(data.prefabID, out int currentCount);
            counts[data.prefabID] = currentCount + 1;
            ValidatePosition(data, index, issues);
            ValidateMaximumCount(entry, counts[data.prefabID], issues);
        }
    }

    private bool TryGetEntry(
        PlacedObjectData data,
        int index,
        List<MapValidationIssue> issues,
        out PaletteEntry entry)
    {
        entry = null;
        if (data != null && !string.IsNullOrWhiteSpace(data.prefabID) &&
            palette.TryGetEntry(data.prefabID, out entry))
            return true;

        string prefabID = data?.prefabID;
        issues.Add(new MapValidationIssue(
            MapValidationSeverity.Error,
            $"오브젝트 #{index + 1}의 팔레트 ID가 없거나 등록되지 않았습니다.",
            prefabID,
            index));
        return false;
    }

    private void ValidatePosition(
        PlacedObjectData data,
        int index,
        List<MapValidationIssue> issues)
    {
        Vector3 position = data.position?.ToVector3() ?? Vector3.positiveInfinity;
        if (IsFinite(position) && IsInsideMap(position)) return;

        issues.Add(new MapValidationIssue(
            MapValidationSeverity.Error,
            $"오브젝트 #{index + 1} ({data.prefabID})의 좌표가 맵 경계 밖입니다: {position}",
            data.prefabID,
            index));
    }

    private bool IsFinite(Vector3 position)
    {
        return float.IsFinite(position.x) &&
               float.IsFinite(position.y) &&
               float.IsFinite(position.z);
    }

    private bool IsInsideMap(Vector3 position)
    {
        return position.x >= settings.minimumMapPosition.x &&
               position.x <= settings.maximumMapPosition.x &&
               position.y >= settings.minimumMapPosition.y &&
               position.y <= settings.maximumMapPosition.y;
    }

    private void ValidateMaximumCount(
        PaletteEntry entry,
        int count,
        List<MapValidationIssue> issues)
    {
        int maximumCount = entry.validationRule?.maximumCount ?? 0;
        if (maximumCount <= 0 || count <= maximumCount) return;

        issues.Add(new MapValidationIssue(
            MapValidationSeverity.Error,
            $"{entry.displayName} ({entry.id})은(는) 최대 {maximumCount}개만 배치할 수 있습니다.",
            entry.id));
    }

    private void ValidateOverlaps(
        List<PlacedObjectData> objects,
        List<MapValidationIssue> issues)
    {
        for (int first = 0; first < objects.Count; first++)
        {
            for (int second = first + 1; second < objects.Count; second++)
                ValidateOverlap(objects, first, second, issues);
        }
    }

    private void ValidateOverlap(
        List<PlacedObjectData> objects,
        int firstIndex,
        int secondIndex,
        List<MapValidationIssue> issues)
    {
        PlacedObjectData first = objects[firstIndex];
        PlacedObjectData second = objects[secondIndex];
        if (!TryGetRule(first, out MapObjectValidationRule firstRule) ||
            !TryGetRule(second, out MapObjectValidationRule secondRule))
            return;
        if (!firstRule.disallowPositionOverlap && !secondRule.disallowPositionOverlap) return;
        if (!HasSamePosition(first, second)) return;

        MapValidationSeverity severity = firstRule.overlapIsCritical ||
                                         secondRule.overlapIsCritical
            ? MapValidationSeverity.Error
            : MapValidationSeverity.Warning;
        issues.Add(CreateOverlapIssue(first, second, firstIndex, secondIndex, severity));
    }

    private bool TryGetRule(
        PlacedObjectData data,
        out MapObjectValidationRule rule)
    {
        rule = null;
        if (data == null || !palette.TryGetEntry(data.prefabID, out PaletteEntry entry))
            return false;

        rule = entry.validationRule ?? new MapObjectValidationRule();
        return true;
    }

    private bool HasSamePosition(PlacedObjectData first, PlacedObjectData second)
    {
        if (first.position == null || second.position == null) return false;

        Vector3 difference = first.position.ToVector3() - second.position.ToVector3();
        return difference.sqrMagnitude <=
               settings.overlapPositionTolerance * settings.overlapPositionTolerance;
    }

    private MapValidationIssue CreateOverlapIssue(
        PlacedObjectData first,
        PlacedObjectData second,
        int firstIndex,
        int secondIndex,
        MapValidationSeverity severity)
    {
        string message = $"오브젝트 #{firstIndex + 1} ({first.prefabID})과 " +
                         $"#{secondIndex + 1} ({second.prefabID})의 위치가 겹칩니다.";
        return new MapValidationIssue(severity, message, first.prefabID, firstIndex);
    }
}
