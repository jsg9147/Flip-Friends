using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

public enum MapCompletionState
{
    NotCompleted,
    PreviousRevisionCompleted,
    CurrentRevisionCompleted
}

public enum MapCompletionLoadState
{
    Loaded,
    Missing,
    CorruptedRecovered,
    UnsupportedSchema,
    FileSystemFailure
}

public enum CurrentRevisionVerificationState
{
    Completed,
    NotCompleted,
    Unavailable
}

public readonly struct MapCompletionStorageIdentity : IEquatable<MapCompletionStorageIdentity>
{
    public LobbyMapKind Kind { get; }
    public string MapId { get; }

    public MapCompletionStorageIdentity(LobbyMapKind kind, string mapId)
    {
        Kind = kind;
        MapId = mapId ?? string.Empty;
    }

    public bool Equals(MapCompletionStorageIdentity other) =>
        Kind == other.Kind && string.Equals(MapId, other.MapId, StringComparison.Ordinal);

    public override bool Equals(object obj) =>
        obj is MapCompletionStorageIdentity other && Equals(other);

    public override int GetHashCode() => HashCode.Combine((int)Kind, MapId);
}

public readonly struct MapCompletionTarget : IEquatable<MapCompletionTarget>
{
    public MapKey MapKey { get; }
    public string Revision { get; }
    public bool IsValid => MapKey.IsValid && !string.IsNullOrEmpty(Revision);

    private MapCompletionTarget(MapKey mapKey, string revision)
    {
        MapKey = mapKey;
        Revision = revision;
    }

    public static bool TryCreateOfficial(
        string mapId,
        int completionRevision,
        out MapCompletionTarget target)
    {
        target = default;
        if (completionRevision < 1 ||
            !MapKey.TryCreate(LobbyMapKind.Official, mapId, out MapKey mapKey) ||
            mapKey.MapId != mapId)
            return false;

        target = new MapCompletionTarget(mapKey, completionRevision.ToString());
        return true;
    }

    public static bool TryCreateCustom(
        string mapId,
        string contentHash,
        out MapCompletionTarget target)
    {
        target = default;
        if (!MapKey.TryCreate(LobbyMapKind.Custom, mapId, out MapKey mapKey) ||
            mapKey.MapId != mapId ||
            !MapCompletionRules.TryNormalizeContentHash(contentHash, out string hash) ||
            hash != contentHash)
            return false;

        target = new MapCompletionTarget(mapKey, hash);
        return true;
    }

    public bool Equals(MapCompletionTarget other) =>
        MapKey == other.MapKey &&
        string.Equals(Revision, other.Revision, StringComparison.Ordinal);

    public override bool Equals(object obj) =>
        obj is MapCompletionTarget other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(MapKey, Revision);
}

public static class MapCompletionRules
{
    public const int ContentHashLength = 64;

    public static bool TryNormalizeContentHash(string value, out string normalized)
    {
        normalized = value?.Trim().ToLowerInvariant();
        if (normalized?.Length != ContentHashLength)
            return false;

        foreach (char character in normalized)
        {
            bool isDigit = character >= '0' && character <= '9';
            bool isLowerHex = character >= 'a' && character <= 'f';
            if (!isDigit && !isLowerHex)
                return false;
        }

        return true;
    }
}

public sealed class GameplayCompletionSession
{
    private readonly HashSet<int> participantConnectionIds;

    public Guid SessionId { get; }
    public MapKey MapKey { get; }
    public string CustomContentHash { get; }
    public int ParticipantCount => participantConnectionIds.Count;

    private GameplayCompletionSession(
        MapKey mapKey,
        string customContentHash,
        IEnumerable<int> connectionIds)
    {
        SessionId = Guid.NewGuid();
        MapKey = mapKey;
        CustomContentHash = customContentHash ?? string.Empty;
        participantConnectionIds = new HashSet<int>(connectionIds);
    }

    public static bool TryCreate(
        LobbyMapKind kind,
        string mapId,
        string customContentHash,
        IEnumerable<int> connectionIds,
        out GameplayCompletionSession session)
    {
        session = null;
        if (!MapKey.TryCreate(kind, mapId, out MapKey mapKey) ||
            mapKey.MapId != mapId || connectionIds == null)
            return false;

        var uniqueIds = new HashSet<int>(connectionIds);
        if (uniqueIds.Count == 0 || uniqueIds.Any(connectionId => connectionId < 0))
            return false;
        if (kind == LobbyMapKind.Official && !string.IsNullOrEmpty(customContentHash))
            return false;
        if (kind == LobbyMapKind.Custom &&
            (!MapCompletionRules.TryNormalizeContentHash(
                customContentHash, out string normalizedHash) ||
             normalizedHash != customContentHash))
            return false;

        session = new GameplayCompletionSession(
            mapKey, customContentHash, uniqueIds);
        return true;
    }

    public bool AreAllParticipantsComplete(IEnumerable<int> completedConnectionIds)
    {
        if (completedConnectionIds == null) return false;

        var completed = new HashSet<int>(completedConnectionIds);
        return completed.SetEquals(participantConnectionIds);
    }

    public bool ContainsParticipant(int connectionId) =>
        participantConnectionIds.Contains(connectionId);
}

public interface IMapCompletionFileSystem
{
    bool FileExists(string path);
    string ReadAllText(string path);
    void CreateDirectory(string path);
    void WriteAllTextDurable(string path, string content);
    void MoveFile(string sourcePath, string destinationPath);
    void ReplaceFile(string sourcePath, string destinationPath, string backupPath);
    void DeleteFile(string path);
}

public sealed class PhysicalMapCompletionFileSystem : IMapCompletionFileSystem
{
    public bool FileExists(string path) => File.Exists(path);

    public string ReadAllText(string path) =>
        File.ReadAllText(path, new UTF8Encoding(false, true));

    public void CreateDirectory(string path) => Directory.CreateDirectory(path);

    public void WriteAllTextDurable(string path, string content)
    {
        byte[] bytes = new UTF8Encoding(false).GetBytes(content);
        using var stream = new FileStream(
            path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        stream.Write(bytes, 0, bytes.Length);
        stream.Flush(true);
    }

    public void MoveFile(string sourcePath, string destinationPath) =>
        File.Move(sourcePath, destinationPath);

    public void ReplaceFile(
        string sourcePath,
        string destinationPath,
        string backupPath) =>
        File.Replace(sourcePath, destinationPath, backupPath);

    public void DeleteFile(string path) => File.Delete(path);
}

public sealed class MapCompletionRepository
{
    public const int CurrentSchemaVersion = 1;
    public const string DefaultFileName = "map-completions.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
        WriteIndented = true
    };

    private readonly string directoryPath;
    private readonly string filePath;
    private readonly IMapCompletionFileSystem fileSystem;
    private readonly Action<string> logWarning;
    private readonly Action<string, Exception> logError;
    private readonly HashSet<MapCompletionTarget> records = new();
    private bool canSave = true;

    public event Action Changed;
    public MapCompletionLoadState LoadState { get; private set; }
    public string FilePath => filePath;
    public int RecordCount => records.Count;

    public MapCompletionRepository(
        string directoryPath,
        IMapCompletionFileSystem fileSystem = null,
        Action<string> logWarning = null,
        Action<string, Exception> logError = null)
    {
        if (string.IsNullOrWhiteSpace(directoryPath))
            throw new ArgumentException("완료 기록 저장 경로가 비어 있습니다.", nameof(directoryPath));

        this.directoryPath = Path.GetFullPath(directoryPath);
        filePath = Path.Combine(this.directoryPath, DefaultFileName);
        this.fileSystem = fileSystem ?? new PhysicalMapCompletionFileSystem();
        this.logWarning = logWarning;
        this.logError = logError;
        Load();
    }

    public bool HasCompleted(MapKey mapKey)
    {
        if (!mapKey.IsValid) return false;

        return records.Any(record => record.MapKey == mapKey);
    }

    public bool HasCompletedCurrentRevision(MapCompletionTarget target) =>
        target.IsValid && records.Contains(target);

    public MapCompletionState GetState(MapCompletionTarget target)
    {
        if (!target.IsValid || !HasCompleted(target.MapKey))
            return MapCompletionState.NotCompleted;

        return records.Contains(target)
            ? MapCompletionState.CurrentRevisionCompleted
            : MapCompletionState.PreviousRevisionCompleted;
    }

    public CurrentRevisionVerificationState GetCurrentRevisionVerification(
        MapCompletionTarget target)
    {
        if (!target.IsValid)
            return CurrentRevisionVerificationState.Unavailable;
        if (records.Contains(target))
            return CurrentRevisionVerificationState.Completed;

        return LoadState == MapCompletionLoadState.Loaded ||
               LoadState == MapCompletionLoadState.Missing
            ? CurrentRevisionVerificationState.NotCompleted
            : CurrentRevisionVerificationState.Unavailable;
    }

    public bool TryRecordCompletion(MapCompletionTarget target, out string error)
    {
        error = null;
        if (!target.IsValid)
        {
            error = "MapId 또는 완료 리비전이 올바르지 않습니다.";
            logWarning?.Invoke(error);
            return false;
        }
        if (!canSave)
        {
            error = "지원하지 않는 완료 기록 스키마는 덮어쓸 수 없습니다.";
            logWarning?.Invoke(error);
            return false;
        }
        if (records.Contains(target))
            return true;

        records.Add(target);
        if (!TrySave(out error))
        {
            records.Remove(target);
            return false;
        }

        Changed?.Invoke();
        return true;
    }

    private void Load()
    {
        if (!fileSystem.FileExists(filePath))
        {
            LoadState = MapCompletionLoadState.Missing;
            return;
        }

        try
        {
            string json = fileSystem.ReadAllText(filePath);
            CompletionDocument document = JsonSerializer.Deserialize<CompletionDocument>(
                json, JsonOptions);
            if (document == null || document.Records == null)
                throw new JsonException("완료 기록 문서의 필수 필드가 없습니다.");
            if (document.SchemaVersion != CurrentSchemaVersion)
            {
                canSave = false;
                LoadState = MapCompletionLoadState.UnsupportedSchema;
                logWarning?.Invoke(
                    $"지원하지 않는 완료 기록 schemaVersion입니다: " +
                    $"{document.SchemaVersion} (현재 {CurrentSchemaVersion})");
                return;
            }

            int rejectedCount = 0;
            int duplicateCount = 0;
            foreach (CompletionRecord record in document.Records)
            {
                if (TryConvert(record, out MapCompletionTarget target, out string reason))
                {
                    if (!records.Add(target))
                        duplicateCount++;
                }
                else
                {
                    rejectedCount++;
                    logWarning?.Invoke(
                        $"잘못된 완료 기록을 제외했습니다: " +
                        $"kind={record?.MapKind}, mapId={record?.MapId}, reason={reason}");
                }
            }

            if (rejectedCount > 0)
                logWarning?.Invoke($"잘못된 완료 기록 {rejectedCount}개를 제외했습니다.");
            if (duplicateCount > 0)
                logWarning?.Invoke($"중복 완료 기록 {duplicateCount}개를 정규화했습니다.");
            LoadState = MapCompletionLoadState.Loaded;
        }
        catch (JsonException exception)
        {
            RecoverCorruptedFile(exception);
        }
        catch (DecoderFallbackException exception)
        {
            RecoverCorruptedFile(exception);
        }
        catch (Exception exception)
        {
            LoadState = MapCompletionLoadState.FileSystemFailure;
            canSave = false;
            logError?.Invoke($"완료 기록 파일을 읽지 못했습니다: {filePath}", exception);
        }
    }

    private void RecoverCorruptedFile(Exception exception)
    {
        string quarantinePath = filePath + ".corrupt." + DateTime.UtcNow.Ticks;
        try
        {
            fileSystem.MoveFile(filePath, quarantinePath);
            LoadState = MapCompletionLoadState.CorruptedRecovered;
            logError?.Invoke(
                $"손상된 완료 기록을 격리했습니다: {quarantinePath}", exception);
        }
        catch (Exception quarantineException)
        {
            LoadState = MapCompletionLoadState.FileSystemFailure;
            canSave = false;
            logError?.Invoke(
                $"손상된 완료 기록을 격리하지 못했습니다: {filePath}",
                quarantineException);
        }
    }

    private bool TrySave(out string error)
    {
        string temporaryPath = filePath + ".tmp." + Guid.NewGuid().ToString("N");
        string backupPath = filePath + ".bak";
        try
        {
            fileSystem.CreateDirectory(directoryPath);
            string json = JsonSerializer.Serialize(CreateDocument(), JsonOptions);
            fileSystem.WriteAllTextDurable(temporaryPath, json);
            if (fileSystem.FileExists(filePath))
            {
                if (fileSystem.FileExists(backupPath))
                    fileSystem.DeleteFile(backupPath);
                fileSystem.ReplaceFile(temporaryPath, filePath, backupPath);
                TryDeleteBackupFile(backupPath);
            }
            else
            {
                fileSystem.MoveFile(temporaryPath, filePath);
            }

            error = null;
            return true;
        }
        catch (Exception exception)
        {
            TryDeleteTemporaryFile(temporaryPath);
            error = $"완료 기록 저장에 실패했습니다: {filePath}";
            logError?.Invoke(error, exception);
            return false;
        }
    }

    private CompletionDocument CreateDocument()
    {
        var document = new CompletionDocument
        {
            SchemaVersion = CurrentSchemaVersion,
            Records = records
                .OrderBy(record => (int)record.MapKey.Kind)
                .ThenBy(record => record.MapKey.MapId, StringComparer.Ordinal)
                .ThenBy(record => record.Revision, StringComparer.Ordinal)
                .Select(CreateRecord)
                .ToList()
        };
        return document;
    }

    private static CompletionRecord CreateRecord(MapCompletionTarget target)
    {
        var record = new CompletionRecord
        {
            MapKind = target.MapKey.Kind.ToString(),
            MapId = target.MapKey.MapId
        };
        if (target.MapKey.Kind == LobbyMapKind.Official)
            record.CompletionRevision = int.Parse(target.Revision);
        else
            record.ContentHash = target.Revision;
        return record;
    }

    private static bool TryConvert(
        CompletionRecord record,
        out MapCompletionTarget target,
        out string reason)
    {
        target = default;
        reason = null;
        if (record == null ||
            !Enum.TryParse(record.MapKind, false, out LobbyMapKind kind))
        {
            reason = "맵 종류가 올바르지 않습니다.";
            return false;
        }

        bool valid = kind switch
        {
            LobbyMapKind.Official =>
                record.ContentHash == null &&
                MapCompletionTarget.TryCreateOfficial(
                    record.MapId, record.CompletionRevision, out target),
            LobbyMapKind.Custom =>
                record.CompletionRevision == 0 &&
                MapCompletionTarget.TryCreateCustom(
                    record.MapId, record.ContentHash, out target),
            _ => false
        };
        if (!valid)
            reason = kind == LobbyMapKind.Official
                ? "공식 MapId 또는 CompletionRevision이 올바르지 않습니다."
                : "커스텀 MapId 또는 ContentHash가 올바르지 않습니다.";
        return valid;
    }

    private void TryDeleteTemporaryFile(string temporaryPath)
    {
        try
        {
            if (fileSystem.FileExists(temporaryPath))
                fileSystem.DeleteFile(temporaryPath);
        }
        catch (Exception exception)
        {
            logError?.Invoke($"완료 기록 임시 파일을 정리하지 못했습니다: {temporaryPath}", exception);
        }
    }

    private void TryDeleteBackupFile(string backupPath)
    {
        try
        {
            if (fileSystem.FileExists(backupPath))
                fileSystem.DeleteFile(backupPath);
        }
        catch (Exception exception)
        {
            logError?.Invoke($"완료 기록 백업 파일을 정리하지 못했습니다: {backupPath}", exception);
        }
    }

    public sealed class CompletionDocument
    {
        public int SchemaVersion { get; set; }
        public List<CompletionRecord> Records { get; set; }
    }

    public sealed class CompletionRecord
    {
        public string MapKind { get; set; }
        public string MapId { get; set; }
        public int CompletionRevision { get; set; }
        public string ContentHash { get; set; }
    }
}
