public sealed class PendingRoomMapSelection
{
    public RoomMapPolicy Policy { get; }
    public LobbyMapKind Kind { get; }
    public string MapId { get; }
    public string CustomMapJson { get; }
    public int MinimumPlayersToClear { get; }
    public string DisplayName { get; }
    public string AuthorName { get; }
    public string Version { get; }
    public MapKey MapKey { get; }
    public MapCompletionTarget CompletionTarget { get; }

    private PendingRoomMapSelection(
        RoomMapPolicy policy,
        LobbyMapKind kind,
        string mapId,
        string customMapJson,
        int minimumPlayersToClear,
        string displayName,
        string authorName,
        string version,
        MapCompletionTarget completionTarget)
    {
        Policy = policy;
        Kind = kind;
        MapId = mapId;
        CustomMapJson = customMapJson;
        MinimumPlayersToClear = minimumPlayersToClear;
        DisplayName = displayName;
        AuthorName = authorName;
        Version = version;
        MapKey.TryCreate(kind, mapId, out MapKey mapKey);
        MapKey = mapKey;
        CompletionTarget = completionTarget;
    }

    public static bool TryCreateOfficial(
        OfficialMapEntry entry,
        out PendingRoomMapSelection selection)
    {
        selection = null;
        if (entry == null ||
            !OfficialMapId.TryNormalize(entry.MapId, out string normalizedMapId) ||
            normalizedMapId != entry.MapId ||
            !MapPlayRequirements.IsValidMinimumPlayers(entry.MinimumPlayersToClear) ||
            !MapCompletionTarget.TryCreateOfficial(
                entry.MapId, entry.CompletionRevision, out MapCompletionTarget target))
            return false;

        selection = new PendingRoomMapSelection(
            RoomMapPolicy.OfficialOnly,
            LobbyMapKind.Official,
            entry.MapId,
            string.Empty,
            entry.MinimumPlayersToClear,
            entry.DisplayName,
            entry.AuthorName,
            entry.Version,
            target);
        return true;
    }

    public static bool TryCreateCustom(
        SavedMapListEntry entry,
        out PendingRoomMapSelection selection)
    {
        selection = null;
        if (entry == null || !entry.CanSelect || string.IsNullOrEmpty(entry.Json) ||
            !CustomMapId.TryNormalize(entry.MapId, out string normalizedMapId) ||
            normalizedMapId != entry.MapId ||
            !MapPlayRequirements.IsValidMinimumPlayers(entry.MinimumPlayersToClear) ||
            !MapCompletionTarget.TryCreateCustom(
                entry.MapId, entry.ContentHash, out MapCompletionTarget target))
            return false;

        selection = new PendingRoomMapSelection(
            RoomMapPolicy.CustomOnly,
            LobbyMapKind.Custom,
            entry.MapId,
            entry.Json,
            entry.MinimumPlayersToClear,
            entry.MapName,
            entry.AuthorName,
            entry.Version,
            target);
        return true;
    }

    public bool TryCreateLobbyMetadata(out LobbyMapMetadata metadata) =>
        LobbyMapMetadata.TryCreate(
            Policy,
            Kind,
            MapId,
            DisplayName,
            AuthorName,
            Version,
            MinimumPlayersToClear,
            out metadata);
}
