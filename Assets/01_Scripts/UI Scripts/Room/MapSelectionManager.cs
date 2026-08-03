using System;
using System.Collections;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

public enum LobbyMapAvailabilityState
{
    Idle,
    Waiting,
    Ready,
    Missing,
    HashMismatch,
    TransferPending,
    TransferTimedOut,
    InvalidChunk,
    HashVerificationFailed,
    MapValidationFailed,
    TimedOut,
    InvalidResponse,
    SelectionChanged
}

public class MapSelectionManager : NetworkBehaviour
{
    private const float DefaultAvailabilityTimeoutSeconds = 10f;
    private const float DefaultTransferTimeoutSeconds = 30f;
    private const float DefaultSelectionUploadTimeoutSeconds = 12f;
    private const int DefaultChunkBytes = 24 * 1024;
    private const int DefaultMaximumMapBytes = 512 * 1024;
    private const int DefaultChunksPerFrame = 2;
    private const int DefaultBytesPerFrame = 48 * 1024;

    [SerializeField] private GameObject mapSelectScreen;
    [SerializeField] private StageSelectBtnEvent stageSelectBtnEvent;
    [SerializeField] private MapEditorPalette mapEditorPalette;
    [SerializeField] private OfficialMapCatalog officialMapCatalog;
    [SerializeField, Min(1f)]
    private float availabilityTimeoutSeconds = DefaultAvailabilityTimeoutSeconds;
    [SerializeField, Min(1f)]
    private float transferTimeoutSeconds = DefaultTransferTimeoutSeconds;
    [SerializeField, Min(1f)]
    private float selectionUploadTimeoutSeconds = DefaultSelectionUploadTimeoutSeconds;
    [SerializeField, Range(8192, 32768)]
    private int transferChunkBytes = DefaultChunkBytes;
    [SerializeField, Range(32768, DefaultMaximumMapBytes)]
    private int maximumMapBytes = DefaultMaximumMapBytes;
    [SerializeField, Min(1)]
    private int maximumChunksPerFrame = DefaultChunksPerFrame;
    [SerializeField, Min(8192)]
    private int maximumTransferBytesPerFrame = DefaultBytesPerFrame;

    [SyncVar(hook = nameof(OnMapKindChanged))]
    private LobbyMapKind selectedMapKind;
    [SyncVar(hook = nameof(OnMapIdChanged))]
    private string selectedMapId = string.Empty;
    [SyncVar(hook = nameof(OnMapNameChanged))]
    private string selectedMapName = string.Empty;
    [SyncVar(hook = nameof(OnAuthorNameChanged))]
    private string selectedAuthorName = string.Empty;
    [SyncVar(hook = nameof(OnVersionChanged))]
    private string selectedVersion = string.Empty;
    [SyncVar(hook = nameof(OnWarningStateChanged))]
    private bool selectedMapHasWarnings;
    [SyncVar(hook = nameof(OnByteCountChanged))]
    private int selectedMapByteCount;
    [SyncVar(hook = nameof(OnMinimumPlayersChanged))]
    private int selectedMinimumPlayersToClear;
    [SyncVar(hook = nameof(OnAvailabilityStateChanged))]
    private LobbyMapAvailabilityState availabilityState;
    [SyncVar(hook = nameof(OnAvailabilityMessageChanged))]
    private string availabilityMessage = string.Empty;
    [SyncVar] private bool selectionLocked;

    public event Action SelectionChanged;
    public CustomRoomPlayer RoomPlayer { get; private set; }
    public MapEditorPalette Palette => mapEditorPalette;
    public bool IsLocalHost =>
        RoomPlayer != null && RoomPlayer.isLocalPlayer && RoomPlayer.index == 0;
    public LobbyMapKind SelectedMapKind => selectedMapKind;
    public string SelectedMapName => selectedMapName;
    public string SelectedAuthorName => selectedAuthorName;
    public string SelectedVersion => selectedVersion;
    public bool SelectedMapHasWarnings => selectedMapHasWarnings;
    public int SelectedMapByteCount => selectedMapByteCount;
    public int SelectedMinimumPlayersToClear => selectedMinimumPlayersToClear;
    public LobbyMapAvailabilityState AvailabilityState => availabilityState;
    public string AvailabilityMessage => availabilityMessage;
    public bool IsSelectionLocked => selectionLocked;

    private readonly Dictionary<NetworkConnectionToClient, ParticipantTransferState>
        availabilityResponses = new();
    private Coroutine availabilityTimeoutCoroutine;
    private Coroutine transferTimeoutCoroutine;
    private Coroutine transferQueueCoroutine;
    private Coroutine selectionUploadTimeoutCoroutine;
    private CustomRoomPlayer startRequester;
    private uint availabilityGeneration;
    private string availabilityMapId = string.Empty;
    private MapSessionSnapshot sessionSnapshot;
    private NetworkConnectionToClient selectionUploadConnection;
    private MapChunkAssembler selectionUploadAssembler;
    private bool selectionUploadHasWarnings;
    private readonly SelectionUploadLease selectionUploadLease = new();
    private readonly RoundRobinTransferQueue<PendingChunkTransfer> transferQueue = new();

    private void Start()
    {
        RefreshSelectionUI();
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        ServerApplyPendingRoomSelection();
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        RefreshSelectionUI();
    }

    public void BindRoomPlayer(CustomRoomPlayer roomPlayer)
    {
        if (roomPlayer == null || !roomPlayer.isLocalPlayer) return;

        RoomPlayer = roomPlayer;
        RefreshSelectionUI();
    }

    public void MapSelectScreenSetActive(bool isActive)
    {
        if (mapSelectScreen == null) return;

        mapSelectScreen.SetActive(isActive);
        if (isActive && stageSelectBtnEvent != null)
            stageSelectBtnEvent.ButtonInit();
        RefreshSelectionUI();
    }

    public void SelectOfficialMap(string officialMapId)
    {
        if (!CanLocalPlayerChangeSelection()) return;

        RoomPlayer.CmdSelectOfficialMap(officialMapId);
    }

    public void SelectCustomMap(SavedMapListEntry entry)
    {
        if (!CanLocalPlayerChangeSelection() || entry == null || !entry.CanSelect) return;

        RoomPlayer.UploadCustomMapSelection(entry);
    }

    public void ClearCustomMapSelection()
    {
        if (!CanLocalPlayerChangeSelection()) return;

        RoomPlayer.CmdClearMapSelection();
    }

    public void StartSelectedMap()
    {
        if (!CanLocalPlayerChangeSelection()) return;

        RoomPlayer.CmdStartSelectedMap();
    }

    [Server]
    public void ServerSelectOfficial(string officialMapId)
    {
        if (selectionLocked) return;
        if (!TryGetOfficialMap(officialMapId, out OfficialMapEntry entry)) return;
        if (!TryValidateConfiguredSelection(
                LobbyMapKind.Official,
                entry.MapId,
                entry.MinimumPlayersToClear))
            return;

        ServerInvalidateAvailabilityCheck("공식맵 선택으로 변경되었습니다.");
        SetAvailabilityStatus(LobbyMapAvailabilityState.Idle, string.Empty);
        sessionSnapshot = null;
        ClearServerMapSession();
        selectedMapKind = LobbyMapKind.Official;
        selectedMapId = entry.MapId;
        selectedMapName = entry.DisplayName;
        selectedAuthorName = entry.AuthorName;
        selectedVersion = entry.Version;
        selectedMapHasWarnings = false;
        selectedMapByteCount = 0;
        selectedMinimumPlayersToClear = entry.MinimumPlayersToClear;
        ApplySelectionToRoomManager();
    }

    [Server]
    public void ServerSelectCustom(
        string mapId,
        string mapName,
        string authorName,
        string version,
        int byteCount,
        bool hasWarnings,
        string json)
    {
        if (selectionLocked) return;
        if (!MapData.TryNormalizeMapId(mapId, out string normalizedMapId))
        {
            Debug.LogWarning("형식이 올바르지 않은 MapId의 선택 요청을 거부했습니다.");
            return;
        }

        MapData mapData = MapDataRepository.FromJson(json);
        if (mapData == null || mapData.mapId != normalizedMapId)
        {
            Debug.LogWarning("MapId와 JSON 데이터가 일치하지 않는 선택 요청을 거부했습니다.");
            return;
        }
        if (!TryValidateConfiguredSelection(
                LobbyMapKind.Custom,
                normalizedMapId,
                mapData.minimumPlayersToClear))
            return;

        ServerInvalidateAvailabilityCheck("커스텀 맵 선택이 변경되었습니다.");
        sessionSnapshot = null;
        if (!MapSessionSnapshot.TryCreate(
                availabilityGeneration,
                json,
                maximumMapBytes,
                mapEditorPalette,
                out MapSessionSnapshot snapshot,
                out string snapshotError))
        {
            Debug.LogWarning($"커스텀 맵 세션 스냅샷 생성을 거부했습니다: {snapshotError}");
            return;
        }

        sessionSnapshot = snapshot;
        ClearServerMapSession();
        SetAvailabilityStatus(LobbyMapAvailabilityState.Idle, string.Empty);
        selectedMapKind = LobbyMapKind.Custom;
        selectedMapId = normalizedMapId;
        selectedMapName = mapData.mapName;
        selectedAuthorName = string.IsNullOrWhiteSpace(mapData.authorName)
            ? authorName
            : mapData.authorName;
        selectedVersion = mapData.version;
        selectedMapHasWarnings = hasWarnings;
        selectedMapByteCount = byteCount;
        selectedMinimumPlayersToClear = mapData.minimumPlayersToClear;
        ApplySelectionToRoomManager();
    }

    [Server]
    public void ServerBeginCustomSelectionUpload(
        CustomRoomPlayer requester,
        string transferId,
        string mapId,
        string contentHash,
        int byteLength,
        int chunkCount,
        bool hasWarnings)
    {
        ClearSelectionUpload();
        if (!ServerAllowsCustomMapTransfer() ||
            requester == null || !requester.ServerIsRoomHost() ||
            requester.connectionToClient == null ||
            !MapData.TryNormalizeMapId(mapId, out string normalizedMapId) ||
            normalizedMapId != mapId ||
            string.IsNullOrEmpty(transferId) ||
            contentHash?.Length != 64 ||
            byteLength <= 0 || byteLength > maximumMapBytes ||
            chunkCount <= 0 || chunkCount > 64 ||
            chunkCount != Mathf.CeilToInt(byteLength / (float)transferChunkBytes))
        {
            Debug.LogWarning("커스텀 맵 선택 업로드 manifest를 거부했습니다.");
            return;
        }

        selectionUploadConnection = requester.connectionToClient;
        selectionUploadHasWarnings = hasWarnings;
        selectionUploadAssembler = new MapChunkAssembler(
            0, transferId, mapId, contentHash, byteLength, chunkCount);
        selectionUploadLease.Begin(transferId, mapId, contentHash);
        selectionUploadTimeoutCoroutine = StartCoroutine(
            SelectionUploadTimeout(requester, transferId));
    }

    [Server]
    public void ServerReceiveCustomSelectionChunk(
        CustomRoomPlayer requester,
        string transferId,
        string mapId,
        string contentHash,
        int chunkCount,
        int chunkIndex,
        byte[] payload)
    {
        if (!ServerAllowsCustomMapTransfer() ||
            requester == null ||
            requester.connectionToClient != selectionUploadConnection ||
            selectionUploadAssembler == null ||
            !selectionUploadLease.Matches(transferId, mapId, contentHash) ||
            !selectionUploadAssembler.Matches(
                0, transferId, mapId, contentHash, chunkCount))
        {
            Debug.LogWarning("현재 선택 업로드와 일치하지 않는 청크를 무시했습니다.");
            return;
        }
        if (!selectionUploadAssembler.TryAdd(
                chunkIndex, payload, maximumMapBytes, out string chunkError))
        {
            Debug.LogWarning($"커스텀 맵 선택 청크를 거부했습니다: {chunkError}");
            ClearSelectionUpload();
            return;
        }
        if (!selectionUploadAssembler.IsComplete) return;
        if (!selectionUploadAssembler.TryAssemble(
                out byte[] content, out string assembleError))
        {
            Debug.LogWarning($"커스텀 맵 선택 재조립에 실패했습니다: {assembleError}");
            ClearSelectionUpload();
            return;
        }

        string json;
        try
        {
            json = new System.Text.UTF8Encoding(false, true).GetString(content);
        }
        catch (System.Text.DecoderFallbackException exception)
        {
            Debug.LogWarning($"커스텀 맵 선택 UTF-8을 거부했습니다: {exception.Message}");
            ClearSelectionUpload();
            return;
        }

        MapData data = MapDataRepository.FromJson(json);
        if (data == null || data.mapId != mapId)
        {
            Debug.LogWarning("재조립한 선택 맵의 MapId가 manifest와 일치하지 않습니다.");
            ClearSelectionUpload();
            return;
        }

        bool hasWarnings = selectionUploadHasWarnings;
        ClearSelectionUpload();
        ServerSelectCustom(
            data.mapId, data.mapName, data.authorName, data.version,
            content.Length, hasWarnings, json);
    }

    private void ClearSelectionUpload()
    {
        if (selectionUploadTimeoutCoroutine != null)
            StopCoroutine(selectionUploadTimeoutCoroutine);
        selectionUploadTimeoutCoroutine = null;
        selectionUploadConnection = null;
        selectionUploadAssembler = null;
        selectionUploadHasWarnings = false;
        selectionUploadLease.Invalidate();
    }

    [Server]
    public void ServerClearSelection()
    {
        if (selectionLocked) return;
        ServerInvalidateAvailabilityCheck("맵 선택이 해제되었습니다.");
        SetAvailabilityStatus(LobbyMapAvailabilityState.Idle, string.Empty);
        sessionSnapshot = null;
        ClearServerMapSession();
        selectedMapKind = LobbyMapKind.None;
        selectedMapId = string.Empty;
        selectedMapName = string.Empty;
        selectedAuthorName = string.Empty;
        selectedVersion = string.Empty;
        selectedMapHasWarnings = false;
        selectedMapByteCount = 0;
        selectedMinimumPlayersToClear = 0;
        ApplySelectionToRoomManager();
    }

    [Server]
    private void ServerApplyPendingRoomSelection()
    {
        if (NetworkManager.singleton is not SteamRoomManager roomManager ||
            roomManager.PendingMapSelection == null)
        {
            Debug.LogError("방 생성 전에 선택한 맵 정보를 찾을 수 없습니다.");
            return;
        }

        PendingRoomMapSelection pending = roomManager.PendingMapSelection;
        if (pending.Kind == LobbyMapKind.Official)
            ServerSelectOfficial(pending.MapId);
        else if (pending.Kind == LobbyMapKind.Custom)
            ServerSelectPendingCustom(pending.CustomMapJson);
        else
            Debug.LogError($"지원하지 않는 사전 선택 맵 종류입니다: {pending.Kind}");

        selectionLocked = selectedMapKind == pending.Kind &&
                          selectedMapId == pending.MapId &&
                          selectedMinimumPlayersToClear == pending.MinimumPlayersToClear;
        if (!selectionLocked)
            Debug.LogError("방 생성 전 맵 선택을 GameRoom에 적용하지 못했습니다.");
    }

    [Server]
    private void ServerSelectPendingCustom(string json)
    {
        MapData data = MapDataRepository.FromJson(json);
        if (data == null)
        {
            Debug.LogError("방 생성 전에 선택한 커스텀맵을 다시 검증하지 못했습니다.");
            return;
        }

        MapValidationReport report = new MapDataValidator(mapEditorPalette).Validate(data);
        if (report.HasErrors)
        {
            Debug.LogError("방 생성 전에 선택한 커스텀맵의 플레이 가능성 검증에 실패했습니다.");
            return;
        }

        int byteCount = System.Text.Encoding.UTF8.GetByteCount(json);
        ServerSelectCustom(
            data.mapId,
            data.mapName,
            data.authorName,
            data.version,
            byteCount,
            report.HasWarnings,
            json);
    }

    [Server]
    public void ServerRequestStart(CustomRoomPlayer requester)
    {
        if (!TryValidateStartRequest(requester, out string error))
        {
            requester?.TargetShowMapSelectionError(error);
            return;
        }

        if (selectedMapKind == LobbyMapKind.Official)
        {
            ApplySelectionToRoomManager();
            StartGameplay();
            return;
        }

        BeginAvailabilityCheck(requester);
    }

    [Server]
    public void ServerReceiveManifest(
        CustomRoomPlayer responder,
        uint generation,
        string mapId,
        string transferId,
        string contentHash,
        MapContentAvailability availability)
    {
        if (!ServerAllowsCustomMapTransfer() ||
            responder == null || sessionSnapshot == null ||
            availabilityTimeoutCoroutine == null)
            return;
        if (generation != availabilityGeneration)
        {
            Debug.LogWarning(
                $"이전 맵 보유 검사 응답을 무시했습니다: player={responder.playerName}, " +
                $"generation={generation}");
            return;
        }

        if (mapId != availabilityMapId ||
            transferId != sessionSnapshot.TransferId ||
            contentHash != sessionSnapshot.ContentHash)
        {
            FailAvailabilityCheck(
                $"잘못된 MapId 응답으로 게임 시작을 보류했습니다: {responder.playerName}",
                MapTransferFailure.MapValidationFailed);
            return;
        }

        NetworkConnectionToClient connection = responder.connectionToClient;
        if (connection == null || !availabilityResponses.ContainsKey(connection))
            return;
        if (availabilityResponses[connection] != ParticipantTransferState.ManifestPending)
        {
            Debug.LogWarning($"중복 맵 보유 응답을 무시했습니다: {responder.playerName}");
            return;
        }

        if (availability == MapContentAvailability.Available)
        {
            availabilityResponses[connection] = ParticipantTransferState.Ready;
        }
        else if (availability == MapContentAvailability.Missing ||
                 availability == MapContentAvailability.HashMismatch)
        {
            availabilityResponses[connection] = ParticipantTransferState.TransferPending;
        }
        else
        {
            FailAvailabilityCheck(
                $"'{responder.playerName}' 플레이어의 로컬 맵 데이터가 올바르지 않습니다.",
                MapTransferFailure.MapValidationFailed);
            return;
        }

        if (!AllManifestResponsesReceived()) return;

        StopAvailabilityTimeout();
        if (AllParticipantsHaveMap())
        {
            CompleteAvailabilityCheck();
            return;
        }

        SetAvailabilityStatus(
            LobbyMapAvailabilityState.TransferPending,
            "필요한 참여자에게 커스텀 맵을 전송하고 있습니다.");
        SendSnapshotToPendingParticipants();
    }

    [Server]
    public void ServerReceiveTransferResult(
        CustomRoomPlayer responder,
        uint generation,
        string transferId,
        string mapId,
        string contentHash,
        MapTransferFailure failure)
    {
        if (!ServerAllowsCustomMapTransfer() ||
            !MatchesCurrentTransfer(
                responder, generation, transferId, mapId, contentHash,
                out NetworkConnectionToClient connection))
            return;
        if (availabilityResponses[connection] != ParticipantTransferState.TransferPending)
        {
            Debug.LogWarning($"중복 또는 늦은 맵 전송 응답을 무시했습니다: {responder.playerName}");
            return;
        }
        if (failure != MapTransferFailure.None)
        {
            FailAvailabilityCheck(
                $"'{responder.playerName}'의 맵 전송 검증에 실패했습니다: {failure}",
                failure);
            return;
        }

        availabilityResponses[connection] = ParticipantTransferState.Ready;
        if (AllParticipantsHaveMap())
            CompleteAvailabilityCheck();
    }

    [Server]
    public void ServerInvalidateAvailabilityCheck(string reason)
    {
        availabilityGeneration++;
        ClearSelectionUpload();
        if (availabilityTimeoutCoroutine == null)
        {
            ClearAvailabilityState();
            return;
        }

        SetAvailabilityStatus(
            LobbyMapAvailabilityState.SelectionChanged,
            $"게임 시작 검사가 취소되었습니다. {reason}");
        Debug.LogWarning($"커스텀 맵 보유 검사를 취소했습니다: {reason}");
        startRequester?.TargetShowMapSelectionError(availabilityMessage);
        ClearAvailabilityState();
    }

    private void ApplySelectionToRoomManager()
    {
        if (NetworkManager.singleton is not SlimeRoomManager roomManager) return;

        string contentHash = selectedMapKind == LobbyMapKind.Custom && sessionSnapshot != null
            ? sessionSnapshot.ContentHash
            : string.Empty;
        roomManager.SetCurrentMap(selectedMapKind, selectedMapId, contentHash);
    }

    private void ClearServerMapSession()
    {
        if (NetworkManager.singleton is SlimeRoomManager roomManager)
            roomManager.ServerMapSession.Clear();
    }

    [Server]
    private bool TryValidateStartRequest(
        CustomRoomPlayer requester,
        out string error)
    {
        if (requester == null || !requester.ServerIsRoomHost())
        {
            error = "방장만 게임 시작을 요청할 수 있습니다.";
            return false;
        }

        if (!TryValidateServerStartState(out error))
        {
            return false;
        }

        if (selectedMapKind == LobbyMapKind.Official &&
            !TryGetOfficialMap(selectedMapId, out _))
        {
            error = "선택된 공식맵 상태가 올바르지 않습니다. 맵을 다시 선택하세요.";
            return false;
        }

        if (selectedMapKind == LobbyMapKind.Custom &&
            (!MapData.TryNormalizeMapId(selectedMapId, out string normalizedMapId) ||
             normalizedMapId != selectedMapId ||
             sessionSnapshot == null ||
             sessionSnapshot.MapId != selectedMapId))
        {
            error = "선택된 커스텀 맵 상태가 올바르지 않습니다. 맵을 다시 선택하세요.";
            return false;
        }

        return true;
    }

    [Server]
    private void BeginAvailabilityCheck(CustomRoomPlayer requester)
    {
        if (!ServerAllowsCustomMapTransfer())
        {
            requester?.TargetShowMapSelectionError(
                "커스텀맵 전송은 CustomOnly 방에서만 허용됩니다.");
            return;
        }
        ServerInvalidateAvailabilityCheck("새 게임 시작 검사를 시작합니다.");
        startRequester = requester;
        availabilityMapId = selectedMapId;
        if (sessionSnapshot == null ||
            sessionSnapshot.MapId != availabilityMapId)
        {
            requester.TargetShowMapSelectionError(
                "선택 시점의 세션 맵 스냅샷을 찾을 수 없습니다.");
            ClearAvailabilityState();
            return;
        }
        SetAvailabilityStatus(
            LobbyMapAvailabilityState.Waiting,
            "참여자들의 커스텀 맵 보유 여부를 확인하고 있습니다.");
        CollectParticipants();
        if (availabilityResponses.Count == 0)
        {
            requester.TargetShowMapSelectionError("검사할 활성 참여자가 없습니다.");
            ClearAvailabilityState();
            return;
        }

        requester.TargetShowMapSelectionMessage(availabilityMessage);
        availabilityTimeoutCoroutine = StartCoroutine(
            AvailabilityTimeout(availabilityGeneration, availabilityMapId));
        RequestManifestFromParticipants();
    }

    [Server]
    private void CollectParticipants()
    {
        if (NetworkManager.singleton is not SlimeRoomManager roomManager) return;

        foreach (NetworkRoomPlayer roomPlayer in roomManager.roomSlots)
        {
            if (roomPlayer is not CustomRoomPlayer customPlayer ||
                customPlayer.connectionToClient == null)
                continue;

            availabilityResponses.TryAdd(
                customPlayer.connectionToClient,
                ParticipantTransferState.ManifestPending);
        }
    }

    [Server]
    private void RequestManifestFromParticipants()
    {
        if (!ServerAllowsCustomMapTransfer()) return;
        foreach (NetworkConnectionToClient connection in availabilityResponses.Keys)
        {
            if (connection.identity == null ||
                !connection.identity.TryGetComponent(out CustomRoomPlayer player))
            {
                FailAvailabilityCheck(
                    "참여자 연결에서 로비 플레이어를 찾을 수 없어 시작을 보류했습니다.",
                    MapTransferFailure.MapValidationFailed);
                return;
            }

            int chunkCount = GetChunkCount();
            player.TargetRequestMapManifest(
                connection,
                availabilityGeneration,
                sessionSnapshot.TransferId,
                availabilityMapId,
                sessionSnapshot.ContentHash,
                sessionSnapshot.ByteLength,
                chunkCount);
        }
    }

    private IEnumerator AvailabilityTimeout(uint generation, string mapId)
    {
        yield return new WaitForSecondsRealtime(availabilityTimeoutSeconds);
        if (!isServer || generation != availabilityGeneration ||
            mapId != availabilityMapId)
            yield break;

        FailAvailabilityCheck(
            "일부 참여자의 맵 보유 응답이 없어 게임 시작 시간이 초과되었습니다.",
            MapTransferFailure.TransferTimedOut);
    }

    private IEnumerator TransferTimeout(uint generation, string transferId)
    {
        yield return new WaitForSecondsRealtime(transferTimeoutSeconds);
        if (!isServer || sessionSnapshot == null ||
            generation != availabilityGeneration ||
            transferId != sessionSnapshot.TransferId)
            yield break;

        FailAvailabilityCheck(
            "커스텀 맵 청크 수신 완료 응답 시간이 초과되었습니다.",
            MapTransferFailure.TransferTimedOut);
    }

    private IEnumerator SelectionUploadTimeout(
        CustomRoomPlayer requester,
        string transferId)
    {
        yield return new WaitForSecondsRealtime(selectionUploadTimeoutSeconds);
        if (!isServer || selectionUploadAssembler == null ||
            !selectionUploadLease.IsActive ||
            transferId != selectionUploadLease.TransferId)
            yield break;

        string message = "커스텀 맵 선택 업로드 시간이 초과되었습니다. 맵을 다시 선택하세요.";
        Debug.LogWarning(
            $"커스텀 맵 선택 업로드를 취소했습니다: transferId={transferId}, " +
            $"timeout={selectionUploadTimeoutSeconds}초");
        ClearSelectionUpload();
        requester?.TargetShowMapSelectionError(message);
        SetAvailabilityStatus(LobbyMapAvailabilityState.TimedOut, message);
    }

    [Server]
    private void SendSnapshotToPendingParticipants()
    {
        if (!ServerAllowsCustomMapTransfer()) return;
        int chunkCount = GetChunkCount();
        foreach (KeyValuePair<NetworkConnectionToClient, ParticipantTransferState> pair
                 in availabilityResponses)
        {
            if (pair.Value != ParticipantTransferState.TransferPending) continue;
            if (pair.Key.identity == null ||
                !pair.Key.identity.TryGetComponent(out CustomRoomPlayer player))
            {
                FailAvailabilityCheck(
                    "전송 대상 로비 플레이어를 찾을 수 없습니다.",
                    MapTransferFailure.MapValidationFailed);
                return;
            }

            transferQueue.Enqueue(new PendingChunkTransfer(
                pair.Key, player, availabilityGeneration,
                sessionSnapshot.TransferId, sessionSnapshot.MapId,
                sessionSnapshot.ContentHash, sessionSnapshot.ByteLength,
                chunkCount));
        }

        if (transferQueue.Count > 0)
            transferQueueCoroutine = StartCoroutine(ProcessTransferQueue());
    }

    private IEnumerator ProcessTransferQueue()
    {
        bool timeoutStarted = false;
        while (transferQueue.Count > 0)
        {
            int sentChunks = 0;
            int sentBytes = 0;
            int candidates = transferQueue.Count;
            while (candidates-- > 0 &&
                   sentChunks < maximumChunksPerFrame &&
                   sentBytes < maximumTransferBytesPerFrame)
            {
                if (!transferQueue.TryDequeue(out PendingChunkTransfer pending))
                    break;
                if (!IsCurrentTransferTarget(pending))
                    continue;

                byte[] payload = CreateTransferPayload(pending.NextChunkIndex);
                if (sentChunks > 0 &&
                    sentBytes + payload.Length > maximumTransferBytesPerFrame)
                {
                    transferQueue.Enqueue(pending);
                    continue;
                }

                if (!timeoutStarted)
                {
                    timeoutStarted = true;
                    transferTimeoutCoroutine = StartCoroutine(
                        TransferTimeout(pending.Generation, pending.TransferId));
                }

                try
                {
                    pending.Player.TargetReceiveMapChunk(
                        pending.Connection,
                        pending.Generation,
                        pending.TransferId,
                        pending.MapId,
                        pending.ContentHash,
                        pending.ByteLength,
                        pending.ChunkCount,
                        pending.NextChunkIndex,
                        payload);
                }
                catch (Exception exception)
                {
                    Debug.LogWarning(
                        $"커스텀 맵 청크 전송 호출을 건너뛰었습니다: " +
                        $"connection={pending.Connection.connectionId}, " +
                        $"transferId={pending.TransferId}, reason={exception.Message}");
                    continue;
                }

                pending.NextChunkIndex++;
                sentChunks++;
                sentBytes += payload.Length;
                if (pending.NextChunkIndex < pending.ChunkCount)
                    transferQueue.Enqueue(pending);
            }

            yield return null;
        }

        transferQueueCoroutine = null;
    }

    private byte[] CreateTransferPayload(int chunkIndex)
    {
        int offset = chunkIndex * transferChunkBytes;
        int count = Mathf.Min(
            transferChunkBytes, sessionSnapshot.ByteLength - offset);
        byte[] payload = new byte[count];
        Buffer.BlockCopy(sessionSnapshot.Content, offset, payload, 0, count);
        return payload;
    }

    private bool IsCurrentTransferTarget(PendingChunkTransfer pending)
    {
        return isServer &&
               sessionSnapshot != null &&
               pending.Connection != null &&
               pending.Connection.isReady &&
               pending.Player != null &&
               pending.Player.connectionToClient == pending.Connection &&
               availabilityResponses.TryGetValue(
                   pending.Connection, out ParticipantTransferState state) &&
               state == ParticipantTransferState.TransferPending &&
               pending.Generation == availabilityGeneration &&
               pending.TransferId == sessionSnapshot.TransferId &&
               pending.MapId == sessionSnapshot.MapId &&
               pending.ContentHash == sessionSnapshot.ContentHash;
    }

    private int GetChunkCount() =>
        Mathf.CeilToInt(sessionSnapshot.ByteLength / (float)transferChunkBytes);

    private bool AllManifestResponsesReceived()
    {
        foreach (ParticipantTransferState state in availabilityResponses.Values)
        {
            if (state == ParticipantTransferState.ManifestPending)
                return false;
        }

        return availabilityResponses.Count > 0;
    }

    private void StopAvailabilityTimeout()
    {
        if (availabilityTimeoutCoroutine == null) return;

        StopCoroutine(availabilityTimeoutCoroutine);
        availabilityTimeoutCoroutine = null;
    }

    private bool MatchesCurrentTransfer(
        CustomRoomPlayer responder,
        uint generation,
        string transferId,
        string mapId,
        string contentHash,
        out NetworkConnectionToClient connection)
    {
        connection = responder?.connectionToClient;
        bool matches = sessionSnapshot != null &&
                       connection != null &&
                       availabilityResponses.ContainsKey(connection) &&
                       generation == availabilityGeneration &&
                       transferId == sessionSnapshot.TransferId &&
                       mapId == sessionSnapshot.MapId &&
                       contentHash == sessionSnapshot.ContentHash;
        if (!matches)
        {
            Debug.LogWarning(
                $"현재 전송과 일치하지 않는 완료 응답을 무시했습니다: " +
                $"generation={generation}, transferId={transferId}, mapId={mapId}");
        }

        return matches;
    }

    [Server]
    private void CompleteAvailabilityCheck()
    {
        string cacheError = "SlimeRoomManager를 찾을 수 없습니다.";
        if (NetworkManager.singleton is not SlimeRoomManager roomManager ||
            !roomManager.ServerMapSession.TryStore(
                sessionSnapshot, mapEditorPalette, out cacheError))
        {
            FailAvailabilityCheck(
                $"서버 세션 캐시를 확정하지 못했습니다: {cacheError}",
                MapTransferFailure.MapValidationFailed);
            return;
        }

        SetAvailabilityStatus(
            LobbyMapAvailabilityState.Ready,
            "모든 참여자가 선택된 커스텀 맵을 보유하고 있습니다.");
        ApplySelectionToRoomManager();
        ClearAvailabilityState();
        StartGameplay();
    }

    public override void OnStopServer()
    {
        ServerInvalidateAvailabilityCheck("서버가 종료되었습니다.");
        sessionSnapshot = null;
        base.OnStopServer();
    }

    [Server]
    private void FailAvailabilityCheck(
        string message,
        MapTransferFailure failureState)
    {
        SetAvailabilityStatus(ToLobbyState(failureState), message);
        Debug.LogWarning(
            $"커스텀 맵 게임 시작 보류: state={failureState}, " +
            $"mapId={availabilityMapId}, generation={availabilityGeneration}, " +
            $"reason={message}");
        startRequester?.TargetShowMapSelectionError(message);
        ClearAvailabilityState();
    }

    [Server]
    private void StartGameplay()
    {
        if (NetworkManager.singleton is not SlimeRoomManager roomManager) return;
        if (!TryValidateServerStartState(out string error))
        {
            Debug.LogWarning($"게임 시작 직전 서버 검증에 실패했습니다: {error}");
            return;
        }
        if (!roomManager.TryBeginGameplayCompletionSession(out error))
        {
            Debug.LogError($"완료 판정용 서버 세션을 확정하지 못했습니다: {error}");
            startRequester?.TargetShowMapSelectionError(error);
            return;
        }

        ServerInvalidateAvailabilityCheck("게임 씬으로 전환합니다.");
        roomManager.ServerChangeScene(roomManager.GameplayScene);
    }

    private bool AllParticipantsHaveMap()
    {
        foreach (ParticipantTransferState response in availabilityResponses.Values)
        {
            if (response != ParticipantTransferState.Ready)
                return false;
        }

        return availabilityResponses.Count > 0;
    }

    private void ClearAvailabilityState()
    {
        if (availabilityTimeoutCoroutine != null)
            StopCoroutine(availabilityTimeoutCoroutine);
        availabilityTimeoutCoroutine = null;
        if (transferTimeoutCoroutine != null)
            StopCoroutine(transferTimeoutCoroutine);
        transferTimeoutCoroutine = null;
        if (transferQueueCoroutine != null)
            StopCoroutine(transferQueueCoroutine);
        transferQueueCoroutine = null;
        transferQueue.Clear();
        availabilityResponses.Clear();
        availabilityMapId = string.Empty;
        startRequester = null;
    }

    [Server]
    private void SetAvailabilityStatus(
        LobbyMapAvailabilityState state,
        string message)
    {
        availabilityState = state;
        availabilityMessage = message ?? string.Empty;
    }

    private LobbyMapAvailabilityState ToLobbyState(
        MapTransferFailure response)
    {
        return response switch
        {
            MapTransferFailure.TransferTimedOut =>
                LobbyMapAvailabilityState.TransferTimedOut,
            MapTransferFailure.InvalidChunk =>
                LobbyMapAvailabilityState.InvalidChunk,
            MapTransferFailure.HashVerificationFailed =>
                LobbyMapAvailabilityState.HashVerificationFailed,
            MapTransferFailure.MapValidationFailed =>
                LobbyMapAvailabilityState.MapValidationFailed,
            _ => LobbyMapAvailabilityState.Idle
        };
    }

    private bool TryGetOfficialMap(string officialMapId, out OfficialMapEntry entry)
    {
        entry = null;
        if (officialMapCatalog == null)
        {
            Debug.LogError("OfficialMapCatalog가 MapSelectionManager에 연결되지 않았습니다.");
            return false;
        }
        if (officialMapCatalog.TryGet(officialMapId, out entry)) return true;

        Debug.LogWarning($"유효하지 않은 공식 MapId 선택 요청을 거부했습니다: {officialMapId}");
        return false;
    }

    [Server]
    private bool TryValidateConfiguredSelection(
        LobbyMapKind mapKind,
        string mapId,
        int minimumPlayersToClear)
    {
        if (NetworkManager.singleton is SlimeRoomManager roomManager &&
            roomManager.IsConfiguredMapSelection(
                mapKind, mapId, minimumPlayersToClear))
            return true;

        Debug.LogWarning(
            $"방 정책과 일치하지 않는 맵 선택을 거부했습니다: " +
            $"kind={mapKind}, mapId={mapId}, minimumPlayers={minimumPlayersToClear}");
        return false;
    }

    [Server]
    private bool TryValidateServerStartState(out string error)
    {
        if (NetworkManager.singleton is not SlimeRoomManager roomManager ||
            !roomManager.AreAllRoomPlayersReady())
        {
            error = "모든 참여자가 준비된 상태에서만 게임을 시작할 수 있습니다.";
            return false;
        }
        if (selectedMapKind == LobbyMapKind.None)
        {
            error = "플레이할 맵을 먼저 선택하세요.";
            return false;
        }

        int readyPlayerCount = roomManager.CountReadyRoomPlayers();
        if (!RoomMapSessionRules.CanStart(
                roomManager.RoomMapMetadata,
                selectedMapKind,
                selectedMapId,
                selectedMinimumPlayersToClear,
                readyPlayerCount))
        {
            error =
                $"선택 맵은 준비 완료 참가자 {selectedMinimumPlayersToClear}명 이상이 필요합니다. " +
                $"현재 준비 인원: {readyPlayerCount}명";
            return false;
        }

        error = null;
        return true;
    }

    private bool ServerAllowsCustomMapTransfer()
    {
        bool allowed = NetworkManager.singleton is SlimeRoomManager roomManager &&
                       roomManager.AllowsCustomMapTransfer();
        if (!allowed)
            Debug.LogWarning("CustomOnly 방이 아닌 세션의 커스텀맵 전송 요청을 거부했습니다.");
        return allowed;
    }

    private bool CanLocalPlayerChangeSelection()
    {
        if (IsLocalHost) return true;

        Debug.LogWarning("맵 선택은 방장만 변경할 수 있습니다.");
        return false;
    }

    private void RefreshSelectionUI()
    {
        SelectionChanged?.Invoke();
    }

    private void OnMapKindChanged(LobbyMapKind oldValue, LobbyMapKind newValue) =>
        RefreshSelectionUI();

    private void OnMapIdChanged(string oldValue, string newValue) =>
        RefreshSelectionUI();

    private void OnMapNameChanged(string oldValue, string newValue) =>
        RefreshSelectionUI();

    private void OnAuthorNameChanged(string oldValue, string newValue) =>
        RefreshSelectionUI();

    private void OnVersionChanged(string oldValue, string newValue) =>
        RefreshSelectionUI();

    private void OnWarningStateChanged(bool oldValue, bool newValue) =>
        RefreshSelectionUI();

    private void OnByteCountChanged(int oldValue, int newValue) =>
        RefreshSelectionUI();

    private void OnMinimumPlayersChanged(int oldValue, int newValue) =>
        RefreshSelectionUI();

    private void OnAvailabilityStateChanged(
        LobbyMapAvailabilityState oldValue,
        LobbyMapAvailabilityState newValue) =>
        RefreshSelectionUI();

    private void OnAvailabilityMessageChanged(string oldValue, string newValue) =>
        RefreshSelectionUI();

    private enum ParticipantTransferState
    {
        ManifestPending,
        TransferPending,
        Ready
    }

    private sealed class PendingChunkTransfer
    {
        public NetworkConnectionToClient Connection { get; }
        public CustomRoomPlayer Player { get; }
        public uint Generation { get; }
        public string TransferId { get; }
        public string MapId { get; }
        public string ContentHash { get; }
        public int ByteLength { get; }
        public int ChunkCount { get; }
        public int NextChunkIndex { get; set; }

        public PendingChunkTransfer(
            NetworkConnectionToClient connection,
            CustomRoomPlayer player,
            uint generation,
            string transferId,
            string mapId,
            string contentHash,
            int byteLength,
            int chunkCount)
        {
            Connection = connection;
            Player = player;
            Generation = generation;
            TransferId = transferId;
            MapId = mapId;
            ContentHash = contentHash;
            ByteLength = byteLength;
            ChunkCount = chunkCount;
        }
    }
}
