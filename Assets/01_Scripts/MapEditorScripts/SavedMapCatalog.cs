using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Mirror;
using UnityEngine;

public enum SavedMapAvailability
{
    Playable,
    PlayableWithWarnings,
    InvalidFile,
    UnsupportedVersion,
    DuplicateMapId,
    ValidationFailed,
    TransferTooLarge
}

public sealed class SavedMapListEntry
{
    public string MapId { get; }
    public string FileName { get; }
    public string MapName { get; }
    public string AuthorName { get; }
    public string Version { get; }
    public string Json { get; }
    public int JsonByteCount { get; }
    public SavedMapAvailability Availability { get; }
    public MapValidationReport ValidationReport { get; }
    public string Message { get; }
    public bool CanSelect =>
        Availability == SavedMapAvailability.Playable ||
        Availability == SavedMapAvailability.PlayableWithWarnings;

    public SavedMapListEntry(
        string mapId,
        string fileName,
        string mapName,
        string authorName,
        string version,
        string json,
        int jsonByteCount,
        SavedMapAvailability availability,
        MapValidationReport validationReport,
        string message)
    {
        MapId = mapId;
        FileName = fileName;
        MapName = mapName;
        AuthorName = authorName;
        Version = version;
        Json = json;
        JsonByteCount = jsonByteCount;
        Availability = availability;
        ValidationReport = validationReport;
        Message = message;
    }

    public SavedMapListEntry AsDuplicateMapId()
    {
        return new SavedMapListEntry(
            MapId,
            FileName,
            MapName,
            AuthorName,
            Version,
            Json,
            JsonByteCount,
            SavedMapAvailability.DuplicateMapId,
            ValidationReport,
            $"같은 MapId를 사용하는 파일이 둘 이상입니다: {MapId}");
    }
}

public sealed class SavedMapCatalog
{
    private const int FallbackPacketLimit = 1024 * 1024;
    private const int PacketReserveBytes = 4096;
    private const float SafePacketRatio = 0.8f;

    private readonly MapDataValidator validator;

    public SavedMapCatalog(MapEditorPalette palette)
    {
        validator = new MapDataValidator(palette);
    }

    public IReadOnlyList<SavedMapListEntry> GetEntries()
    {
        List<SavedMapListEntry> entries = MapDataRepository.GetAllMapNames()
            .Select(CreateEntry)
            .OrderBy(entry => entry.MapName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.MapName, StringComparer.Ordinal)
            .ToList();
        MarkDuplicateMapIds(entries);
        return entries;
    }

    public SavedMapListEntry Refresh(SavedMapListEntry entry)
    {
        if (entry == null) return null;

        return GetEntries().FirstOrDefault(candidate => candidate.MapId == entry.MapId);
    }

    public SavedMapListEntry FindById(string mapId)
    {
        if (!MapData.TryNormalizeMapId(mapId, out string normalizedMapId)) return null;

        return GetEntries().FirstOrDefault(entry => entry.MapId == normalizedMapId);
    }

    private SavedMapListEntry CreateEntry(string fileName)
    {
        if (!MapDataRepository.TryLoadWithJson(
                fileName,
                out MapData mapData,
                out string json,
                out MapRepositoryFailure failure))
            return CreateUnavailableEntry(fileName, failure);

        MapValidationReport report = validator.Validate(mapData);
        int byteCount = Encoding.UTF8.GetByteCount(json);
        if (byteCount >= GetSafeTransferLimit())
            return CreateTransferBlockedEntry(fileName, mapData, json, byteCount, report);
        if (report.HasErrors)
            return CreateValidationBlockedEntry(fileName, mapData, json, byteCount, report);

        SavedMapAvailability availability = report.HasWarnings
            ? SavedMapAvailability.PlayableWithWarnings
            : SavedMapAvailability.Playable;
        string message = report.HasWarnings
            ? BuildIssueMessage(report)
            : "플레이 가능성 검증을 통과했습니다.";
        return CreateEntry(fileName, mapData, json, byteCount, availability, report, message);
    }

    private SavedMapListEntry CreateUnavailableEntry(
        string mapName,
        MapRepositoryFailure failure)
    {
        SavedMapAvailability availability =
            failure.Kind == MapRepositoryFailureKind.UnsupportedVersion
                ? SavedMapAvailability.UnsupportedVersion
                : SavedMapAvailability.InvalidFile;
        Debug.LogWarning($"커스텀 맵을 선택할 수 없습니다: {mapName}\n{failure.Message}");
        return new SavedMapListEntry(
            string.Empty,
            mapName,
            mapName,
            "-",
            "-",
            null,
            0,
            availability,
            null,
            failure.Message);
    }

    private SavedMapListEntry CreateTransferBlockedEntry(
        string fileName,
        MapData data,
        string json,
        int byteCount,
        MapValidationReport report)
    {
        int limit = GetSafeTransferLimit();
        string message =
            $"JSON 크기 {FormatBytes(byteCount)}가 안전 전송 한계 " +
            $"{FormatBytes(limit)} 이상입니다. 맵 오브젝트 수를 줄여 주세요.";
        Debug.LogWarning($"커스텀 맵 전송을 차단했습니다: {data.mapName}\n{message}");
        return CreateEntry(
            fileName,
            data,
            json,
            byteCount,
            SavedMapAvailability.TransferTooLarge,
            report,
            message);
    }

    private SavedMapListEntry CreateValidationBlockedEntry(
        string fileName,
        MapData data,
        string json,
        int byteCount,
        MapValidationReport report)
    {
        string message = BuildIssueMessage(report);
        Debug.LogWarning($"플레이할 수 없는 커스텀 맵입니다: {data.mapName}\n{message}");
        return CreateEntry(
            fileName,
            data,
            json,
            byteCount,
            SavedMapAvailability.ValidationFailed,
            report,
            message);
    }

    private SavedMapListEntry CreateEntry(
        string fileName,
        MapData data,
        string json,
        int byteCount,
        SavedMapAvailability availability,
        MapValidationReport report,
        string message)
    {
        return new SavedMapListEntry(
            data.mapId,
            fileName,
            data.mapName,
            string.IsNullOrWhiteSpace(data.authorName) ? "알 수 없음" : data.authorName,
            data.version,
            json,
            byteCount,
            availability,
            report,
            message);
    }

    private void MarkDuplicateMapIds(List<SavedMapListEntry> entries)
    {
        HashSet<string> duplicateIds = entries
            .Where(entry => !string.IsNullOrEmpty(entry.MapId))
            .GroupBy(entry => entry.MapId, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToHashSet(StringComparer.Ordinal);

        for (int index = 0; index < entries.Count; index++)
        {
            if (duplicateIds.Contains(entries[index].MapId))
                entries[index] = entries[index].AsDuplicateMapId();
        }
    }

    private int GetSafeTransferLimit()
    {
        int packetLimit = Transport.active != null
            ? Transport.active.GetMaxPacketSize(Channels.Reliable)
            : FallbackPacketLimit;
        int ratioLimit = Mathf.FloorToInt(packetLimit * SafePacketRatio);
        return Mathf.Max(1, Mathf.Min(ratioLimit, packetLimit - PacketReserveBytes));
    }

    private string BuildIssueMessage(MapValidationReport report)
    {
        return string.Join(
            "\n",
            report.Issues.Select(issue =>
                $"[{GetSeverityLabel(issue.Severity)}] {issue.Message}"));
    }

    private string GetSeverityLabel(MapValidationSeverity severity)
    {
        return severity == MapValidationSeverity.Error ? "오류" : "경고";
    }

    private string FormatBytes(int byteCount)
    {
        return byteCount >= 1024
            ? $"{byteCount / 1024f:0.0} KB"
            : $"{byteCount} B";
    }
}
