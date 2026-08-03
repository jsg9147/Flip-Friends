using System;
using System.Collections.Generic;

public enum LobbyMapKind
{
    None = 0,
    Official = 1,
    Custom = 2
}

public enum RoomMapPolicy
{
    OfficialOnly = 0,
    CustomOnly = 1
}

public readonly struct MapPlayRequirements : IEquatable<MapPlayRequirements>
{
    public const int MinimumSupportedPlayers = 1;
    public const int MaximumSupportedPlayers = 4;

    public int MinimumPlayersToClear { get; }

    private MapPlayRequirements(int minimumPlayersToClear)
    {
        MinimumPlayersToClear = minimumPlayersToClear;
    }

    public static bool TryCreate(
        int minimumPlayersToClear,
        out MapPlayRequirements requirements)
    {
        requirements = default;
        if (!IsValidMinimumPlayers(minimumPlayersToClear)) return false;

        requirements = new MapPlayRequirements(minimumPlayersToClear);
        return true;
    }

    public static bool IsValidMinimumPlayers(int value) =>
        value >= MinimumSupportedPlayers && value <= MaximumSupportedPlayers;

    public bool Equals(MapPlayRequirements other) =>
        MinimumPlayersToClear == other.MinimumPlayersToClear;

    public override bool Equals(object obj) =>
        obj is MapPlayRequirements other && Equals(other);

    public override int GetHashCode() => MinimumPlayersToClear;
}

public static class RoomMapPolicyExtensions
{
    public static bool Allows(this RoomMapPolicy policy, LobbyMapKind mapKind)
    {
        return policy switch
        {
            RoomMapPolicy.OfficialOnly => mapKind == LobbyMapKind.Official,
            RoomMapPolicy.CustomOnly => mapKind == LobbyMapKind.Custom,
            _ => false
        };
    }
}

public readonly struct MapKey : IEquatable<MapKey>
{
    public LobbyMapKind Kind { get; }
    public string MapId { get; }
    public bool IsValid => Kind != LobbyMapKind.None && !string.IsNullOrEmpty(MapId);

    private MapKey(LobbyMapKind kind, string mapId)
    {
        Kind = kind;
        MapId = mapId;
    }

    public static bool TryCreate(LobbyMapKind kind, string mapId, out MapKey mapKey)
    {
        mapKey = default;
        if (!TryNormalize(kind, mapId, out string normalizedMapId))
            return false;

        mapKey = new MapKey(kind, normalizedMapId);
        return true;
    }

    public bool Equals(MapKey other) =>
        Kind == other.Kind && string.Equals(MapId, other.MapId, StringComparison.Ordinal);

    public override bool Equals(object obj) => obj is MapKey other && Equals(other);

    public override int GetHashCode() => HashCode.Combine((int)Kind, MapId);

    public override string ToString() => IsValid ? $"{Kind}:{MapId}" : "Invalid";

    public static bool operator ==(MapKey left, MapKey right) => left.Equals(right);

    public static bool operator !=(MapKey left, MapKey right) => !left.Equals(right);

    private static bool TryNormalize(
        LobbyMapKind kind,
        string mapId,
        out string normalizedMapId)
    {
        return kind switch
        {
            LobbyMapKind.Official => OfficialMapId.TryNormalize(mapId, out normalizedMapId),
            LobbyMapKind.Custom => CustomMapId.TryNormalize(mapId, out normalizedMapId),
            _ => FailNormalization(out normalizedMapId)
        };
    }

    private static bool FailNormalization(out string normalizedMapId)
    {
        normalizedMapId = null;
        return false;
    }
}

public static class OfficialMapId
{
    private const string Prefix = "official.";
    private const int MaximumLength = 64;

    public static bool TryNormalize(string value, out string normalizedMapId)
    {
        normalizedMapId = value?.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(normalizedMapId) ||
            normalizedMapId.Length > MaximumLength ||
            !normalizedMapId.StartsWith(Prefix, StringComparison.Ordinal))
            return false;

        string[] segments = normalizedMapId.Split('.');
        foreach (string segment in segments)
        {
            if (!IsValidSegment(segment))
                return false;
        }

        return true;
    }

    private static bool IsValidSegment(string segment)
    {
        if (string.IsNullOrEmpty(segment) ||
            segment[0] == '-' ||
            segment[segment.Length - 1] == '-')
            return false;

        foreach (char character in segment)
        {
            bool isLowercaseLetter = character >= 'a' && character <= 'z';
            bool isDigit = character >= '0' && character <= '9';
            if (!isLowercaseLetter && !isDigit && character != '-')
                return false;
        }

        return true;
    }
}

public static class CustomMapId
{
    public static bool TryNormalize(string value, out string normalizedMapId)
    {
        normalizedMapId = null;
        if (!Guid.TryParseExact(value, "N", out Guid mapId))
            return false;

        normalizedMapId = mapId.ToString("N");
        return true;
    }
}

public sealed class OfficialMapDescriptor
{
    public string MapId { get; }
    public string DisplayName { get; }
    public string AuthorName { get; }
    public string Version { get; }
    public int MinimumPlayersToClear { get; }
    public int CompletionRevision { get; }

    public OfficialMapDescriptor(
        string mapId,
        string displayName,
        string authorName,
        string version,
        int minimumPlayersToClear,
        int completionRevision)
    {
        MapId = mapId;
        DisplayName = displayName;
        AuthorName = authorName;
        Version = version;
        MinimumPlayersToClear = minimumPlayersToClear;
        CompletionRevision = completionRevision;
    }
}

public static class OfficialMapCatalogRules
{
    public static IReadOnlyList<string> Validate(
        IReadOnlyList<OfficialMapDescriptor> descriptors)
    {
        var errors = new List<string>();
        var registeredIds = new HashSet<string>(StringComparer.Ordinal);
        if (descriptors == null)
        {
            errors.Add("공식맵 목록이 없습니다.");
            return errors;
        }

        for (int index = 0; index < descriptors.Count; index++)
            ValidateDescriptor(descriptors[index], index, registeredIds, errors);

        return errors;
    }

    private static void ValidateDescriptor(
        OfficialMapDescriptor descriptor,
        int index,
        HashSet<string> registeredIds,
        List<string> errors)
    {
        if (descriptor == null)
        {
            errors.Add($"공식맵 {index}번 항목이 null입니다.");
            return;
        }

        ValidateMapId(descriptor.MapId, index, registeredIds, errors);
        ValidateMetadata(descriptor, index, errors);
        ValidateRequirements(descriptor, index, errors);
    }

    private static void ValidateMapId(
        string mapId,
        int index,
        HashSet<string> registeredIds,
        List<string> errors)
    {
        if (!OfficialMapId.TryNormalize(mapId, out string normalizedMapId) ||
            normalizedMapId != mapId)
        {
            errors.Add($"공식맵 {index}번 ID가 정규 형식이 아닙니다: {mapId}");
            return;
        }

        if (!registeredIds.Add(mapId))
            errors.Add($"중복된 공식맵 ID가 있습니다: {mapId}");
    }

    private static void ValidateMetadata(
        OfficialMapDescriptor descriptor,
        int index,
        List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(descriptor.DisplayName))
            errors.Add($"공식맵 {index}번 표시 이름이 비어 있습니다.");
        if (string.IsNullOrWhiteSpace(descriptor.AuthorName))
            errors.Add($"공식맵 {index}번 제작자 이름이 비어 있습니다.");
        if (string.IsNullOrWhiteSpace(descriptor.Version))
            errors.Add($"공식맵 {index}번 버전이 비어 있습니다.");
    }

    private static void ValidateRequirements(
        OfficialMapDescriptor descriptor,
        int index,
        List<string> errors)
    {
        if (!MapPlayRequirements.IsValidMinimumPlayers(
                descriptor.MinimumPlayersToClear))
        {
            errors.Add($"공식맵 {index}번 최소 클리어 인원은 1~4명이어야 합니다.");
        }
        if (descriptor.CompletionRevision < 1)
            errors.Add($"공식맵 {index}번 완료 리비전은 1 이상이어야 합니다.");
    }
}

public static class RoomCreationRules
{
    public static bool HasEnoughCapacity(int maximumPlayers, int minimumPlayersToClear)
    {
        return MapPlayRequirements.IsValidMinimumPlayers(maximumPlayers) &&
               MapPlayRequirements.IsValidMinimumPlayers(minimumPlayersToClear) &&
               maximumPlayers >= minimumPlayersToClear;
    }

    public static RoomCreationEligibility Evaluate(
        bool isPublicRoom,
        int maximumPlayers,
        LobbyMapMetadata metadata,
        MapCompletionTarget completionTarget,
        CurrentRevisionVerificationState verificationState)
    {
        if (metadata == null || !completionTarget.IsValid ||
            metadata.MapKey != completionTarget.MapKey)
        {
            return RoomCreationEligibility.Blocked(
                RoomCreationBlockReason.InvalidMapSelection);
        }
        if (!HasEnoughCapacity(maximumPlayers, metadata.MinimumPlayersToClear))
        {
            return RoomCreationEligibility.Blocked(
                RoomCreationBlockReason.InsufficientCapacity);
        }
        return EvaluateCompletionGate(isPublicRoom, verificationState);
    }

    private static RoomCreationEligibility EvaluateCompletionGate(
        bool isPublicRoom,
        CurrentRevisionVerificationState verificationState)
    {
        if (!isPublicRoom) return RoomCreationEligibility.Allowed;
        if (verificationState == CurrentRevisionVerificationState.Unavailable)
        {
            return RoomCreationEligibility.Blocked(
                RoomCreationBlockReason.CompletionStatusUnavailable);
        }
        if (verificationState != CurrentRevisionVerificationState.Completed)
        {
            return RoomCreationEligibility.Blocked(
                RoomCreationBlockReason.CurrentRevisionNotCompleted);
        }

        return RoomCreationEligibility.Allowed;
    }
}

public enum RoomCreationBlockReason
{
    None,
    InvalidMapSelection,
    InsufficientCapacity,
    CompletionStatusUnavailable,
    CurrentRevisionNotCompleted
}

public readonly struct RoomCreationEligibility
{
    public static RoomCreationEligibility Allowed =>
        new(true, RoomCreationBlockReason.None);

    public bool CanCreate { get; }
    public RoomCreationBlockReason BlockReason { get; }

    private RoomCreationEligibility(bool canCreate, RoomCreationBlockReason blockReason)
    {
        CanCreate = canCreate;
        BlockReason = blockReason;
    }

    public static RoomCreationEligibility Blocked(RoomCreationBlockReason reason) =>
        new(false, reason);
}

public sealed class LobbyMapMetadata
{
    public const int MaximumDisplayTextLength = 64;

    public RoomMapPolicy Policy { get; }
    public MapKey MapKey { get; }
    public string DisplayName { get; }
    public string AuthorName { get; }
    public string Version { get; }
    public int MinimumPlayersToClear { get; }

    private LobbyMapMetadata(
        RoomMapPolicy policy,
        MapKey mapKey,
        string displayName,
        string authorName,
        string version,
        int minimumPlayersToClear)
    {
        Policy = policy;
        MapKey = mapKey;
        DisplayName = displayName;
        AuthorName = authorName;
        Version = version;
        MinimumPlayersToClear = minimumPlayersToClear;
    }

    public static bool TryCreate(
        RoomMapPolicy policy,
        LobbyMapKind mapKind,
        string mapId,
        string displayName,
        string authorName,
        string version,
        int minimumPlayersToClear,
        out LobbyMapMetadata metadata)
    {
        metadata = null;
        if (!policy.Allows(mapKind) ||
            !MapKey.TryCreate(mapKind, mapId, out MapKey mapKey) ||
            mapKey.MapId != mapId ||
            !MapPlayRequirements.IsValidMinimumPlayers(minimumPlayersToClear) ||
            string.IsNullOrWhiteSpace(displayName))
            return false;

        metadata = new LobbyMapMetadata(
            policy,
            mapKey,
            NormalizeDisplayText(displayName),
            NormalizeDisplayText(authorName),
            NormalizeDisplayText(version),
            minimumPlayersToClear);
        return true;
    }

    private static string NormalizeDisplayText(string value)
    {
        string normalized = value?.Trim() ?? string.Empty;
        return normalized.Length <= MaximumDisplayTextLength
            ? normalized
            : normalized.Substring(0, MaximumDisplayTextLength);
    }
}

public readonly struct LobbyMapFilter
{
    public static LobbyMapFilter All => new(LobbyMapKind.None, 0);

    public LobbyMapKind MapKind { get; }
    public int MaximumMinimumPlayers { get; }

    public LobbyMapFilter(LobbyMapKind mapKind, int maximumMinimumPlayers)
    {
        MapKind = mapKind;
        MaximumMinimumPlayers = maximumMinimumPlayers;
    }

    public bool Matches(LobbyMapMetadata metadata)
    {
        if (metadata == null) return false;
        if (MapKind != LobbyMapKind.None && metadata.MapKey.Kind != MapKind)
            return false;

        return MaximumMinimumPlayers == 0 ||
               MapPlayRequirements.IsValidMinimumPlayers(MaximumMinimumPlayers) &&
               metadata.MinimumPlayersToClear <= MaximumMinimumPlayers;
    }
}

public static class RoomMapSessionRules
{
    public static bool IsConfiguredSelectionValid(
        LobbyMapMetadata roomMetadata,
        LobbyMapKind selectedKind,
        string selectedMapId,
        int selectedMinimumPlayers)
    {
        if (roomMetadata == null ||
            !MapKey.TryCreate(selectedKind, selectedMapId, out MapKey selectedKey))
            return false;

        return roomMetadata.Policy.Allows(selectedKind) &&
               roomMetadata.MapKey == selectedKey &&
               roomMetadata.MinimumPlayersToClear == selectedMinimumPlayers;
    }

    public static bool CanStart(
        LobbyMapMetadata roomMetadata,
        LobbyMapKind selectedKind,
        string selectedMapId,
        int selectedMinimumPlayers,
        int readyPlayerCount)
    {
        return IsConfiguredSelectionValid(
                   roomMetadata, selectedKind, selectedMapId, selectedMinimumPlayers) &&
               readyPlayerCount >= selectedMinimumPlayers;
    }

    public static bool AllowsCustomTransfer(LobbyMapMetadata roomMetadata) =>
        roomMetadata != null &&
        roomMetadata.Policy == RoomMapPolicy.CustomOnly &&
        roomMetadata.MapKey.Kind == LobbyMapKind.Custom;
}
