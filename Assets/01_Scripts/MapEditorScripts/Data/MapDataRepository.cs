using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Mirror;
using UnityEngine;

public static class MapDataRepository
{
    private static readonly HashSet<string> WindowsReservedNames =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "CON", "PRN", "AUX", "NUL",
            "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
            "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
        };

    private static string SaveDirectory => Path.Combine(Application.persistentDataPath, "Maps");
    public static event Action<string> OperationFailed;

    public static string CreateUniqueMapId()
    {
        HashSet<string> existingIds = GetStoredMapIds();
        string mapId;
        do
        {
            mapId = MapData.CreateMapId();
        }
        while (existingIds.Contains(mapId));

        return mapId;
    }

    public static bool TryCreate(
        MapData data,
        bool replaceExistingFile,
        out MapSaveResult result)
    {
        if (!TryPrepareSave(data, out string targetPath, out result))
            return false;
        if (HasStoredMapId(data.mapId))
            return FailSave(
                MapSaveFailureKind.DuplicateMapId,
                $"이미 저장된 MapId입니다: {data.mapId}",
                out result);
        if (File.Exists(targetPath) && !replaceExistingFile)
            return FailSave(
                MapSaveFailureKind.NameConflict,
                $"같은 이름의 맵 파일이 이미 있습니다: {data.mapName}",
                out result);

        return TryWriteMap(targetPath, data, out result);
    }

    public static bool TryUpdate(
        string sourceFileName,
        MapData data,
        out MapSaveResult result)
    {
        if (!TryPrepareSave(data, out string targetPath, out result))
            return false;
        if (!TryGetFilePath(sourceFileName, out string sourcePath, out string pathError))
            return FailSave(MapSaveFailureKind.InvalidName, pathError, out result);
        if (!TryValidateUpdateSource(sourcePath, data.mapId, out result))
            return false;
        if (!PathsEqual(sourcePath, targetPath) && File.Exists(targetPath))
            return FailSave(
                MapSaveFailureKind.NameConflict,
                $"이름을 변경할 대상 파일이 이미 있습니다: {data.mapName}",
                out result);

        return PathsEqual(sourcePath, targetPath)
            ? TryWriteMap(targetPath, data, out result)
            : TryMoveAndWriteMap(sourcePath, targetPath, data, out result);
    }

    public static bool TryImport(MapData data, out MapSaveResult result)
    {
        if (!TryPrepareSave(data, out string targetPath, out result))
            return false;
        if (HasStoredMapId(data.mapId))
            return FailSave(
                MapSaveFailureKind.DuplicateMapId,
                $"같은 MapId의 맵이 이미 저장되어 있습니다: {data.mapId}",
                out result);
        if (File.Exists(targetPath))
            return FailSave(
                MapSaveFailureKind.NameConflict,
                $"같은 이름의 다른 맵이 이미 저장되어 있습니다: {data.mapName}",
                out result);

        return TryWriteMap(targetPath, data, out result);
    }

    private static bool TryPrepareSave(
        MapData data,
        out string targetPath,
        out MapSaveResult result)
    {
        targetPath = null;
        if (data == null)
            return FailSave(
                MapSaveFailureKind.MissingData,
                "저장할 맵 데이터가 없습니다.",
                out result);
        if (!TryNormalizeMapName(data.mapName, out string mapName, out string error))
            return FailSave(
                MapSaveFailureKind.InvalidName,
                $"맵을 저장할 수 없습니다: {error}",
                out result);
        if (!TryPrepareForSave(data, out error))
            return FailSave(
                MapSaveFailureKind.InvalidMapId,
                $"맵을 저장할 수 없습니다: {error}",
                out result);

        data.mapName = mapName;
        EnsureDirectoryExists();
        if (!TryGetFilePath(mapName, out targetPath, out error))
        {
            return FailSave(
                MapSaveFailureKind.InvalidName,
                $"맵을 저장할 수 없습니다: {error}",
                out result);
        }

        result = default;
        return true;
    }

    private static bool TryWriteMap(
        string targetPath,
        MapData data,
        out MapSaveResult result)
    {
        try
        {
            string json = JsonUtility.ToJson(data, prettyPrint: true);
            WriteSafely(targetPath, json);
            result = MapSaveResult.Success(data.mapName);
            return true;
        }
        catch (Exception exception)
        {
            string message = $"맵 저장 중 오류가 발생했습니다: {data.mapName}";
            ReportException(message, exception);
            result = MapSaveResult.Failure(MapSaveFailureKind.FileSystem, message);
            return false;
        }
    }

    public static MapData Load(string mapName)
    {
        if (!TryGetFilePath(mapName, out string path, out string error))
        {
            ReportFailure($"맵을 불러올 수 없습니다: {error}");
            return null;
        }

        try
        {
            if (!File.Exists(path))
            {
                ReportFailure($"맵 파일을 찾을 수 없습니다: {path}");
                return null;
            }

            string json = File.ReadAllText(path);
            if (!TryParseJson(
                    json,
                    $"맵 파일 '{mapName}'",
                    out MapData mapData,
                    out MapRepositoryFailure _,
                    out bool wasMigrated))
                return null;

            if (wasMigrated)
                WriteSafely(path, JsonUtility.ToJson(mapData, prettyPrint: true));
            return mapData;
        }
        catch (Exception exception)
        {
            ReportException($"맵 불러오기 중 오류가 발생했습니다: {mapName}", exception);
            return null;
        }
    }

    public static bool TryLoadWithJson(
        string mapName,
        out MapData mapData,
        out string json,
        out MapRepositoryFailure failure)
    {
        mapData = null;
        json = null;
        if (!TryGetFilePath(mapName, out string path, out string error))
            return FailLoad(MapRepositoryFailureKind.InvalidName, error, out failure);

        try
        {
            if (!File.Exists(path))
                return FailLoad(
                    MapRepositoryFailureKind.MissingFile,
                    $"맵 파일을 찾을 수 없습니다: {path}",
                    out failure);

            json = File.ReadAllText(path);
            if (!TryParseJson(
                    json,
                    $"맵 파일 '{mapName}'",
                    out mapData,
                    out failure,
                    out bool wasMigrated))
                return false;

            if (wasMigrated)
            {
                json = JsonUtility.ToJson(mapData, prettyPrint: true);
                WriteSafely(path, json);
            }

            return true;
        }
        catch (Exception exception)
        {
            string message = $"맵 불러오기 중 오류가 발생했습니다: {mapName}";
            ReportException(message, exception);
            failure = new MapRepositoryFailure(MapRepositoryFailureKind.FileSystem, message);
            return false;
        }
    }

    public static List<string> GetAllMapNames()
    {
        var names = new List<string>();
        try
        {
            EnsureDirectoryExists();
            foreach (string file in Directory.GetFiles(SaveDirectory, "*.json"))
                names.Add(Path.GetFileNameWithoutExtension(file));
        }
        catch (Exception exception)
        {
            ReportException("저장된 맵 목록을 읽는 중 오류가 발생했습니다.", exception);
        }

        names.Sort(CompareMapNames);
        return names;
    }

    public static bool TryLoadById(
        string mapId,
        out string fileName,
        out MapData mapData,
        out string json,
        out MapRepositoryFailure failure)
    {
        fileName = null;
        mapData = null;
        json = null;
        if (!MapData.TryNormalizeMapId(mapId, out string normalizedMapId))
            return FailLoad(
                MapRepositoryFailureKind.InvalidMapId,
                "조회할 MapId 형식이 올바르지 않습니다.",
                out failure);

        foreach (string candidate in GetAllMapNames())
        {
            if (!TryLoadWithJson(
                    candidate,
                    out MapData candidateData,
                    out string candidateJson,
                    out MapRepositoryFailure candidateFailure))
                continue;
            if (candidateData.mapId != normalizedMapId) continue;
            if (fileName != null)
                return FailLoad(
                    MapRepositoryFailureKind.DuplicateMapId,
                    $"같은 MapId를 사용하는 파일이 둘 이상입니다: {normalizedMapId}",
                    out failure);

            fileName = candidate;
            mapData = candidateData;
            json = candidateJson;
        }

        if (fileName != null)
        {
            failure = default;
            return true;
        }

        failure = new MapRepositoryFailure(
            MapRepositoryFailureKind.MissingFile,
            $"MapId에 해당하는 맵 파일을 찾을 수 없습니다: {normalizedMapId}");
        return false;
    }

    public static bool Delete(string mapName)
    {
        if (!TryGetFilePath(mapName, out string path, out string error))
        {
            ReportFailure($"맵을 삭제할 수 없습니다: {error}");
            return false;
        }

        try
        {
            if (!File.Exists(path))
            {
                ReportFailure($"삭제할 맵 파일을 찾을 수 없습니다: {path}");
                return false;
            }

            File.Delete(path);
            return true;
        }
        catch (Exception exception)
        {
            ReportException($"맵 삭제 중 오류가 발생했습니다: {mapName}", exception);
            return false;
        }
    }

    public static bool TryExists(string mapName, out bool exists)
    {
        exists = false;
        if (!TryGetFilePath(mapName, out string path, out string error))
        {
            ReportFailure($"맵 파일을 확인할 수 없습니다: {error}");
            return false;
        }

        try
        {
            exists = File.Exists(path);
            return true;
        }
        catch (Exception exception)
        {
            ReportException($"맵 파일 존재 여부 확인 중 오류가 발생했습니다: {mapName}", exception);
            return false;
        }
    }

    public static string ToJson(MapData data)
    {
        if (data == null)
        {
            ReportFailure("JSON으로 변환할 맵 데이터가 없습니다.");
            return null;
        }

        try
        {
            return JsonUtility.ToJson(data);
        }
        catch (Exception exception)
        {
            ReportException("맵 데이터를 JSON으로 변환하는 중 오류가 발생했습니다.", exception);
            return null;
        }
    }

    public static MapData FromJson(string json) => ParseJson(json, "수신된 맵 데이터");

    public static bool TryNormalizeMapName(
        string mapName,
        out string normalizedName,
        out string error)
    {
        normalizedName = mapName?.Trim();
        if (string.IsNullOrEmpty(normalizedName))
        {
            error = "맵 이름을 입력하세요.";
            return false;
        }

        if (normalizedName.Contains("..", StringComparison.Ordinal))
        {
            error = "맵 이름에는 '..'를 사용할 수 없습니다.";
            return false;
        }

        if (normalizedName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
            normalizedName.IndexOf(Path.DirectorySeparatorChar) >= 0 ||
            normalizedName.IndexOf(Path.AltDirectorySeparatorChar) >= 0)
        {
            error = "맵 이름에 파일명으로 사용할 수 없는 문자가 있습니다.";
            return false;
        }

        if (normalizedName.EndsWith(".", StringComparison.Ordinal))
        {
            error = "맵 이름은 마침표로 끝날 수 없습니다.";
            return false;
        }

        string baseName = Path.GetFileNameWithoutExtension(normalizedName);
        if (WindowsReservedNames.Contains(baseName))
        {
            error = $"'{normalizedName}'은(는) 파일명으로 사용할 수 없는 이름입니다.";
            return false;
        }

        error = null;
        return true;
    }

    private static bool TryGetFilePath(
        string mapName,
        out string filePath,
        out string error)
    {
        filePath = null;
        if (!TryNormalizeMapName(mapName, out string normalizedName, out error))
            return false;

        string directoryPath = Path.GetFullPath(SaveDirectory);
        string candidatePath = Path.GetFullPath(
            Path.Combine(directoryPath, $"{normalizedName}.json"));
        string directoryPrefix = directoryPath.TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;

        if (!candidatePath.StartsWith(directoryPrefix, StringComparison.OrdinalIgnoreCase))
        {
            error = "저장 폴더 밖의 경로에는 접근할 수 없습니다.";
            return false;
        }

        filePath = candidatePath;
        return true;
    }

    private static void EnsureDirectoryExists()
    {
        if (!Directory.Exists(SaveDirectory))
            Directory.CreateDirectory(SaveDirectory);
    }

    private static void WriteSafely(string destinationPath, string contents)
    {
        string temporaryPath = $"{destinationPath}.tmp";
        try
        {
            DeleteStaleTemporaryFile(temporaryPath);
            File.WriteAllText(temporaryPath, contents);

            if (File.Exists(destinationPath))
                File.Replace(temporaryPath, destinationPath, null);
            else
                File.Move(temporaryPath, destinationPath);
        }
        finally
        {
            DeleteStaleTemporaryFile(temporaryPath);
        }
    }

    private static bool TryValidateUpdateSource(
        string sourcePath,
        string mapId,
        out MapSaveResult result)
    {
        if (!File.Exists(sourcePath))
            return FailSave(
                MapSaveFailureKind.MissingTarget,
                $"업데이트할 원본 파일을 찾을 수 없습니다: {sourcePath}",
                out result);

        string existingJson = File.ReadAllText(sourcePath);
        if (!TryParseJson(
                existingJson,
                $"업데이트 원본 파일 '{sourcePath}'",
                out MapData existingData,
                out MapRepositoryFailure failure,
                out bool _))
            return FailSave(
                MapSaveFailureKind.InvalidTarget,
                failure.Message,
                out result);
        if (existingData.mapId != mapId)
            return FailSave(
                MapSaveFailureKind.TargetMismatch,
                "업데이트 대상의 MapId가 현재 맵과 일치하지 않습니다.",
                out result);
        if (CountStoredMapId(mapId) > 1)
            return FailSave(
                MapSaveFailureKind.DuplicateMapId,
                $"같은 MapId를 사용하는 파일이 둘 이상이라 업데이트할 수 없습니다: {mapId}",
                out result);

        result = default;
        return true;
    }

    private static bool TryMoveAndWriteMap(
        string sourcePath,
        string targetPath,
        MapData data,
        out MapSaveResult result)
    {
        if (!TryWriteMap(sourcePath, data, out result))
            return false;

        try
        {
            File.Move(sourcePath, targetPath);
            result = MapSaveResult.Success(data.mapName);
            return true;
        }
        catch (Exception exception)
        {
            string message = $"맵 파일 이름 변경 중 오류가 발생했습니다: {data.mapName}";
            ReportException(message, exception);
            result = MapSaveResult.Failure(MapSaveFailureKind.FileSystem, message);
            return false;
        }
    }

    private static HashSet<string> GetStoredMapIds()
    {
        var mapIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (string fileName in GetAllMapNames())
        {
            if (TryLoadWithJson(
                    fileName,
                    out MapData mapData,
                    out string _,
                    out MapRepositoryFailure _))
                mapIds.Add(mapData.mapId);
        }

        return mapIds;
    }

    private static bool HasStoredMapId(string mapId) => CountStoredMapId(mapId) > 0;

    private static int CountStoredMapId(string mapId)
    {
        int count = 0;
        foreach (string fileName in GetAllMapNames())
        {
            if (TryLoadWithJson(
                    fileName,
                    out MapData mapData,
                    out string _,
                    out MapRepositoryFailure _) &&
                mapData.mapId == mapId)
                count++;
        }

        return count;
    }

    private static bool PathsEqual(string first, string second) =>
        string.Equals(first, second, StringComparison.OrdinalIgnoreCase);

    private static bool FailSave(
        MapSaveFailureKind kind,
        string message,
        out MapSaveResult result)
    {
        ReportFailure(message);
        result = MapSaveResult.Failure(kind, message);
        return false;
    }

    private static void DeleteStaleTemporaryFile(string temporaryPath)
    {
        if (!File.Exists(temporaryPath)) return;

        try
        {
            File.Delete(temporaryPath);
        }
        catch (Exception exception)
        {
            Debug.LogWarning(
                $"맵 저장 임시 파일을 정리하지 못했습니다: {temporaryPath}\n{exception}");
        }
    }

    private static int CompareMapNames(string first, string second)
    {
        int ignoreCaseResult = StringComparer.OrdinalIgnoreCase.Compare(first, second);
        return ignoreCaseResult != 0
            ? ignoreCaseResult
            : StringComparer.Ordinal.Compare(first, second);
    }

    private static MapData ParseJson(string json, string context)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            ReportFailure($"{context}의 JSON이 비어 있습니다.");
            return null;
        }

        try
        {
            MapData data = JsonUtility.FromJson<MapData>(json);
            if (data == null)
            {
                ReportFailure($"{context}의 JSON을 맵 데이터로 변환할 수 없습니다.");
                return null;
            }

            if (!TryMigrate(
                    data,
                    out MapData migratedData,
                    out string error,
                    out bool _))
            {
                ReportFailure($"{context}을(를) 불러올 수 없습니다: {error}");
                return null;
            }

            return migratedData;
        }
        catch (Exception exception)
        {
            ReportException($"{context}의 JSON 형식이 올바르지 않습니다.", exception);
            return null;
        }
    }

    private static bool TryParseJson(
        string json,
        string context,
        out MapData mapData,
        out MapRepositoryFailure failure,
        out bool wasMigrated)
    {
        mapData = null;
        wasMigrated = false;
        if (string.IsNullOrWhiteSpace(json))
            return FailLoad(
                MapRepositoryFailureKind.InvalidJson,
                $"{context}의 JSON이 비어 있습니다.",
                out failure);

        try
        {
            MapData source = JsonUtility.FromJson<MapData>(json);
            if (source == null)
                return FailLoad(
                    MapRepositoryFailureKind.InvalidJson,
                    $"{context}의 JSON을 맵 데이터로 변환할 수 없습니다.",
                    out failure);
            if (!TryMigrate(source, out mapData, out string error, out wasMigrated))
                return FailLoad(
                    source.version == MapData.CurrentVersion
                        ? MapRepositoryFailureKind.InvalidMapId
                        : MapRepositoryFailureKind.UnsupportedVersion,
                    $"{context}을(를) 불러올 수 없습니다: {error}",
                    out failure);

            failure = default;
            return true;
        }
        catch (Exception exception)
        {
            string message = $"{context}의 JSON 형식이 올바르지 않습니다.";
            ReportException(message, exception);
            failure = new MapRepositoryFailure(MapRepositoryFailureKind.InvalidJson, message);
            return false;
        }
    }

    private static bool FailLoad(
        MapRepositoryFailureKind kind,
        string message,
        out MapRepositoryFailure failure)
    {
        ReportFailure(message);
        failure = new MapRepositoryFailure(kind, message);
        return false;
    }

    private static void ReportFailure(string message)
    {
        Debug.LogWarning(message);
        OperationFailed?.Invoke(message);
    }

    private static bool TryMigrate(
        MapData source,
        out MapData migratedData,
        out string error,
        out bool wasMigrated)
    {
        migratedData = null;
        wasMigrated = false;
        if (string.IsNullOrWhiteSpace(source.version))
        {
            error = "맵 데이터 버전이 없어 안전하게 변환할 수 없습니다.";
            return false;
        }

        switch (source.version)
        {
            case MapData.CurrentVersion:
                if (!MapData.TryNormalizeMapId(source.mapId, out string normalizedMapId))
                {
                    error = "MapId가 없거나 형식이 올바르지 않습니다.";
                    return false;
                }

                wasMigrated = source.mapId != normalizedMapId;
                source.mapId = normalizedMapId;
                migratedData = source;
                error = null;
                return true;
            case MapData.PreviousVersion:
                source.mapId = MapData.CreateMapId();
                source.version = MapData.CurrentVersion;
                migratedData = source;
                error = null;
                wasMigrated = true;
                return true;
            default:
                error = $"지원하지 않는 맵 데이터 버전입니다: {source.version} " +
                        $"(현재 버전: {MapData.CurrentVersion})";
                return false;
        }
    }

    private static bool TryPrepareForSave(MapData data, out string error)
    {
        if (!MapData.TryNormalizeMapId(data.mapId, out string normalizedMapId))
        {
            error = "MapId가 없거나 형식이 올바르지 않습니다.";
            return false;
        }

        data.mapId = normalizedMapId;
        data.version = MapData.CurrentVersion;
        error = null;
        return true;
    }

    private static void ReportException(string message, Exception exception)
    {
        Debug.LogError(message);
        Debug.LogException(exception);
        OperationFailed?.Invoke(message);
    }
}

public enum MapSaveFailureKind
{
    None,
    MissingData,
    InvalidName,
    InvalidMapId,
    DuplicateMapId,
    NameConflict,
    MissingTarget,
    InvalidTarget,
    TargetMismatch,
    FileSystem
}

public readonly struct MapSaveResult
{
    public bool IsSuccess { get; }
    public MapSaveFailureKind FailureKind { get; }
    public string Message { get; }

    private MapSaveResult(
        bool isSuccess,
        MapSaveFailureKind failureKind,
        string message)
    {
        IsSuccess = isSuccess;
        FailureKind = failureKind;
        Message = message;
    }

    public static MapSaveResult Success(string mapName) =>
        new(true, MapSaveFailureKind.None, $"저장 완료: {mapName}");

    public static MapSaveResult Failure(
        MapSaveFailureKind failureKind,
        string message) =>
        new(false, failureKind, message);
}

public enum MapRepositoryFailureKind
{
    InvalidName,
    InvalidMapId,
    DuplicateMapId,
    MissingFile,
    InvalidJson,
    UnsupportedVersion,
    FileSystem
}

public readonly struct MapRepositoryFailure
{
    public MapRepositoryFailureKind Kind { get; }
    public string Message { get; }

    public MapRepositoryFailure(MapRepositoryFailureKind kind, string message)
    {
        Kind = kind;
        Message = message;
    }
}

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
