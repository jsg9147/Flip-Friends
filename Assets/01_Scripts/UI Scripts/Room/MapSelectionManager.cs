using System;
using System.Collections;
using System.Collections.Generic;
using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public enum LobbyMapKind
{
    None,
    BuiltIn,
    Custom
}

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
    private const int DefaultMaximumMapBytes = MapSessionSnapshot.MaximumContentBytes;
    private const int DefaultChunksPerFrame = 2;
    private const int DefaultBytesPerFrame = 48 * 1024;

    [SerializeField] private GameObject mapSelectScreen;
    [SerializeField] private StageSelectBtnEvent stageSelectBtnEvent;
    [SerializeField] private MapEditorPalette mapEditorPalette;
    [SerializeField] private TMP_Text selectedMapText;
    [SerializeField] private TMP_Text selectionStatusText;
    [SerializeField] private BuiltInMapCatalog builtInMapCatalog;
    // i번째 버튼은 카탈로그의 i번째 기본 맵을 고른다. 카탈로그에 없는 버튼은 누를 수 없다.
    [SerializeField] private Button[] mapButtons;
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
    // 기본 맵이면 BuiltInMapId, 커스텀 맵이면 GUID N 형식 MapId다. 종류는 selectedMapKind로 구분한다.
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
    [SyncVar(hook = nameof(OnAvailabilityStateChanged))]
    private LobbyMapAvailabilityState availabilityState;
    [SyncVar(hook = nameof(OnAvailabilityMessageChanged))]
    private string availabilityMessage = string.Empty;

    public event Action SelectionChanged;
    public CustomRoomPlayer RoomPlayer { get; private set; }
    public MapEditorPalette Palette => mapEditorPalette;
    public bool IsLocalHost =>
        RoomPlayer != null && RoomPlayer.isLocalPlayer && RoomPlayer.index == 0;
    public LobbyMapKind SelectedMapKind => selectedMapKind;
    public string SelectionSummary => BuildSelectedMapText();
    public string SelectionStatus => BuildStatusText();

    private MapAvailabilityCheck<NetworkConnectionToClient> availabilityCheck;
    private Coroutine transferQueueCoroutine;
    private CustomRoomPlayer startRequester;
    private uint availabilityGeneration;
    private string availabilityMapId = string.Empty;
    private MapSessionSnapshot sessionSnapshot;
    private CustomRoomPlayer selectionUploadRequester;
    private NetworkConnectionToClient selectionUploadConnection;
    private MapChunkAssembler selectionUploadAssembler;
    private bool selectionUploadHasWarnings;
    private readonly SelectionUploadLease selectionUploadLease = new();
    private readonly RoundRobinTransferQueue<PendingChunkTransfer> transferQueue = new();

    private static double Now => Time.realtimeSinceStartupAsDouble;

    private void Awake()
    {
        availabilityCheck = new MapAvailabilityCheck<NetworkConnectionToClient>(
            availabilityTimeoutSeconds, transferTimeoutSeconds);
    }

    private void Start()
    {
        AddButtonEvents();
        RefreshSelectionUI();
    }

    // 제한 시간은 Coroutine 대신 서버 프레임마다 상태 객체의 마감 시각과 비교한다.
    private void Update()
    {
        if (!isServer) return;

        double now = Now;
        if (selectionUploadLease.HasExpired(now))
            HandleSelectionUploadTimeout();
        ApplyAvailabilityStep(availabilityCheck.Tick(now), null);
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

    public void SelectFirstLocalMapListItem(Transform listContainer)
    {
        if (stageSelectBtnEvent == null) return;

        stageSelectBtnEvent.SelectFirstValidIn(listContainer);
    }

    public void SelectBuiltInMapAt(int buttonIndex)
    {
        if (!CanLocalPlayerChangeSelection()) return;

        BuiltInMapCatalog.Entry entry = builtInMapCatalog != null
            ? builtInMapCatalog.GetAt(buttonIndex)
            : null;
        if (entry == null)
        {
            Debug.LogWarning($"카탈로그에 없는 기본 맵 버튼입니다: index={buttonIndex}", this);
            return;
        }

        RoomPlayer.CmdSelectBuiltInMap(entry.MapId);
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
    public void ServerSelectBuiltIn(string mapId)
    {
        if (!TryGetBuiltInMap(mapId, out BuiltInMapCatalog.Entry entry)) return;

        ServerInvalidateAvailabilityCheck("기본 맵 선택으로 변경되었습니다.");
        SetAvailabilityStatus(LobbyMapAvailabilityState.Idle, string.Empty);
        sessionSnapshot = null;
        selectedMapKind = LobbyMapKind.BuiltIn;
        selectedMapId = entry.MapId;
        selectedMapName = entry.DisplayName;
        selectedAuthorName = "Flip Friends";
        selectedVersion = string.Empty;
        selectedMapHasWarnings = false;
        selectedMapByteCount = 0;
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
        if (requester == null || !requester.ServerIsRoomHost() ||
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

        selectionUploadRequester = requester;
        selectionUploadConnection = requester.connectionToClient;
        selectionUploadHasWarnings = hasWarnings;
        selectionUploadAssembler = new MapChunkAssembler(
            0, transferId, mapId, contentHash, byteLength, chunkCount);
        selectionUploadLease.Begin(
            transferId, mapId, contentHash, Now + selectionUploadTimeoutSeconds);
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
        if (requester == null ||
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
        selectionUploadRequester = null;
        selectionUploadConnection = null;
        selectionUploadAssembler = null;
        selectionUploadHasWarnings = false;
        selectionUploadLease.Invalidate();
    }

    [Server]
    public void ServerClearSelection()
    {
        ServerInvalidateAvailabilityCheck("맵 선택이 해제되었습니다.");
        SetAvailabilityStatus(LobbyMapAvailabilityState.Idle, string.Empty);
        sessionSnapshot = null;
        selectedMapKind = LobbyMapKind.None;
        selectedMapId = string.Empty;
        selectedMapName = string.Empty;
        selectedAuthorName = string.Empty;
        selectedVersion = string.Empty;
        selectedMapHasWarnings = false;
        selectedMapByteCount = 0;
        ApplySelectionToRoomManager();
    }

    [Server]
    public void ServerRequestStart(CustomRoomPlayer requester)
    {
        if (!TryValidateStartRequest(requester, out string error))
        {
            requester?.TargetShowMapSelectionError(error);
            return;
        }

        if (selectedMapKind == LobbyMapKind.BuiltIn)
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
        if (responder == null || sessionSnapshot == null) return;

        MapAvailabilityStep step = availabilityCheck.ReceiveManifest(
            responder.connectionToClient, generation, mapId, transferId,
            contentHash, availability);
        if (step.Kind == MapAvailabilityStepKind.Ignored)
        {
            Debug.LogWarning(
                $"이전 또는 중복 맵 보유 응답을 무시했습니다: player={responder.playerName}, " +
                $"generation={generation}");
            return;
        }

        ApplyAvailabilityStep(step, responder);
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
        if (responder == null) return;

        MapAvailabilityStep step = availabilityCheck.ReceiveTransferResult(
            responder.connectionToClient, generation, transferId, mapId,
            contentHash, failure);
        if (step.Kind == MapAvailabilityStepKind.Ignored)
        {
            Debug.LogWarning(
                $"현재 전송과 일치하지 않거나 늦은 완료 응답을 무시했습니다: " +
                $"player={responder.playerName}, generation={generation}, " +
                $"transferId={transferId}, mapId={mapId}");
            return;
        }

        ApplyAvailabilityStep(step, responder);
    }

    [Server]
    public void ServerInvalidateAvailabilityCheck(string reason)
    {
        availabilityGeneration++;
        ClearSelectionUpload();
        // manifest 대기뿐 아니라 청크 전송 중에도 취소 사실을 알린다.
        if (!availabilityCheck.IsActive)
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

        roomManager.currentBuiltInMapId = selectedMapKind == LobbyMapKind.BuiltIn
            ? selectedMapId
            : string.Empty;
        roomManager.currentMapId = selectedMapKind == LobbyMapKind.Custom
            ? selectedMapId
            : string.Empty;
        roomManager.currentMapContentHash =
            selectedMapKind == LobbyMapKind.Custom && sessionSnapshot != null
                ? sessionSnapshot.ContentHash
                : string.Empty;
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

        if (selectedMapKind == LobbyMapKind.Custom &&
            (!MapData.TryNormalizeMapId(selectedMapId, out string normalizedMapId) ||
             normalizedMapId != selectedMapId ||
             sessionSnapshot == null ||
             sessionSnapshot.MapId != selectedMapId))
        {
            error = "선택된 커스텀 맵 상태가 올바르지 않습니다. 맵을 다시 선택하세요.";
            return false;
        }

        error = null;
        return true;
    }

    [Server]
    private void BeginAvailabilityCheck(CustomRoomPlayer requester)
    {
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
        if (!availabilityCheck.Begin(
                availabilityGeneration,
                availabilityMapId,
                sessionSnapshot.TransferId,
                sessionSnapshot.ContentHash,
                CollectParticipantConnections(),
                Now))
        {
            requester.TargetShowMapSelectionError("검사할 활성 참여자가 없습니다.");
            ClearAvailabilityState();
            return;
        }

        requester.TargetShowMapSelectionMessage(availabilityMessage);
        RequestManifestFromParticipants();
    }

    [Server]
    private List<NetworkConnectionToClient> CollectParticipantConnections()
    {
        var connections = new List<NetworkConnectionToClient>();
        if (NetworkManager.singleton is not SlimeRoomManager roomManager)
            return connections;

        foreach (NetworkRoomPlayer roomPlayer in roomManager.roomSlots)
        {
            if (roomPlayer is CustomRoomPlayer customPlayer &&
                customPlayer.connectionToClient != null)
                connections.Add(customPlayer.connectionToClient);
        }

        return connections;
    }

    [Server]
    private void RequestManifestFromParticipants()
    {
        var connections =
            new List<NetworkConnectionToClient>(availabilityCheck.Participants);
        foreach (NetworkConnectionToClient connection in connections)
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

    [Server]
    private void ApplyAvailabilityStep(
        MapAvailabilityStep step,
        CustomRoomPlayer responder)
    {
        switch (step.Kind)
        {
            case MapAvailabilityStepKind.TransferRequired:
                SetAvailabilityStatus(
                    LobbyMapAvailabilityState.TransferPending,
                    "필요한 참여자에게 커스텀 맵을 전송하고 있습니다.");
                SendSnapshotToPendingParticipants();
                break;
            case MapAvailabilityStepKind.Completed:
                CompleteAvailabilityCheck();
                break;
            case MapAvailabilityStepKind.Failed:
                FailAvailabilityCheck(BuildFailureMessage(step, responder), step.Failure);
                break;
        }
    }

    private static string BuildFailureMessage(
        MapAvailabilityStep step,
        CustomRoomPlayer responder)
    {
        string playerName = responder != null ? responder.playerName : "알 수 없음";
        return step.Reason switch
        {
            MapAvailabilityFailureReason.IdentityMismatch =>
                $"잘못된 MapId 응답으로 게임 시작을 보류했습니다: {playerName}",
            MapAvailabilityFailureReason.InvalidLocalData =>
                $"'{playerName}' 플레이어의 로컬 맵 데이터가 올바르지 않습니다.",
            MapAvailabilityFailureReason.ParticipantReportedFailure =>
                $"'{playerName}'의 맵 전송 검증에 실패했습니다: {step.Failure}",
            MapAvailabilityFailureReason.ManifestTimedOut =>
                "일부 참여자의 맵 보유 응답이 없어 게임 시작 시간이 초과되었습니다.",
            MapAvailabilityFailureReason.TransferTimedOut =>
                "커스텀 맵 청크 수신 완료 응답 시간이 초과되었습니다.",
            _ => $"커스텀 맵 보유 검사에 실패했습니다: {step.Failure}"
        };
    }

    [Server]
    private void HandleSelectionUploadTimeout()
    {
        CustomRoomPlayer requester = selectionUploadRequester;
        string transferId = selectionUploadLease.TransferId;
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
        int chunkCount = GetChunkCount();
        foreach (NetworkConnectionToClient connection in
                 availabilityCheck.GetTransferPendingParticipants())
        {
            if (connection.identity == null ||
                !connection.identity.TryGetComponent(out CustomRoomPlayer player))
            {
                FailAvailabilityCheck(
                    "전송 대상 로비 플레이어를 찾을 수 없습니다.",
                    MapTransferFailure.MapValidationFailed);
                return;
            }

            transferQueue.Enqueue(new PendingChunkTransfer(
                connection, player, availabilityGeneration,
                sessionSnapshot.TransferId, sessionSnapshot.MapId,
                sessionSnapshot.ContentHash, sessionSnapshot.ByteLength,
                chunkCount));
        }

        if (transferQueue.Count > 0)
            transferQueueCoroutine = StartCoroutine(ProcessTransferQueue());
    }

    private IEnumerator ProcessTransferQueue()
    {
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

                availabilityCheck.NotifyChunkSent(Now);
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
               availabilityCheck.IsTransferPending(pending.Connection) &&
               pending.Generation == availabilityGeneration &&
               pending.TransferId == sessionSnapshot.TransferId &&
               pending.MapId == sessionSnapshot.MapId &&
               pending.ContentHash == sessionSnapshot.ContentHash;
    }

    private int GetChunkCount() =>
        Mathf.CeilToInt(sessionSnapshot.ByteLength / (float)transferChunkBytes);

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

        ServerInvalidateAvailabilityCheck("게임 씬으로 전환합니다.");
        roomManager.ServerChangeScene(roomManager.GameplayScene);
    }

    private void ClearAvailabilityState()
    {
        if (transferQueueCoroutine != null)
            StopCoroutine(transferQueueCoroutine);
        transferQueueCoroutine = null;
        transferQueue.Clear();
        availabilityCheck.Cancel();
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

    private bool TryGetBuiltInMap(string mapId, out BuiltInMapCatalog.Entry entry)
    {
        entry = null;
        if (builtInMapCatalog == null)
        {
            Debug.LogError("BuiltInMapCatalog가 MapSelectionManager에 연결되어 있지 않습니다.", this);
            return false;
        }
        if (BuiltInMapId.IsValid(mapId) && builtInMapCatalog.TryGet(mapId, out entry))
            return true;

        Debug.LogWarning($"카탈로그에 없는 기본 맵 선택 요청을 거부했습니다: mapId={mapId}", this);
        return false;
    }

    private bool CanLocalPlayerChangeSelection()
    {
        if (IsLocalHost) return true;

        Debug.LogWarning("맵 선택은 방장만 변경할 수 있습니다.");
        return false;
    }

    private void AddButtonEvents()
    {
        if (mapButtons == null) return;

        for (int index = 0; index < mapButtons.Length; index++)
        {
            int buttonIndex = index;
            mapButtons[index].onClick.AddListener(() => SelectBuiltInMapAt(buttonIndex));
        }
    }

    private void RefreshSelectionUI()
    {
        if (selectedMapText != null)
            selectedMapText.text = BuildSelectedMapText();
        if (selectionStatusText != null)
            selectionStatusText.text = BuildStatusText();
        SetMapButtonsInteractable(IsLocalHost);
        SelectionChanged?.Invoke();
    }

    private string BuildSelectedMapText()
    {
        if (selectedMapKind == LobbyMapKind.None)
            return "선택된 맵 없음";

        string version = string.IsNullOrEmpty(selectedVersion)
            ? string.Empty
            : $" / v{selectedVersion}";
        return $"{selectedMapName}\n제작자: {selectedAuthorName}{version}";
    }

    private string BuildStatusText()
    {
        if (availabilityState != LobbyMapAvailabilityState.Idle &&
            !string.IsNullOrEmpty(availabilityMessage))
            return availabilityMessage;
        if (!IsLocalHost)
            return "방장만 맵 선택을 변경할 수 있습니다.";
        if (selectedMapKind == LobbyMapKind.None)
            return "기본 스테이지 또는 커스텀 맵을 선택하세요.";
        if (selectedMapKind == LobbyMapKind.Custom && selectedMapHasWarnings)
            return $"경고가 있는 맵입니다. 선택 가능 / {selectedMapByteCount} bytes";

        return "게임을 시작할 수 있습니다.";
    }

    private void SetMapButtonsInteractable(bool interactable)
    {
        if (mapButtons == null) return;

        for (int index = 0; index < mapButtons.Length; index++)
        {
            if (mapButtons[index] != null)
                mapButtons[index].interactable = interactable && HasBuiltInMapAt(index);
        }
    }

    private bool HasBuiltInMapAt(int index) =>
        builtInMapCatalog != null && builtInMapCatalog.GetAt(index)?.StagePrefab != null;

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

    private void OnAvailabilityStateChanged(
        LobbyMapAvailabilityState oldValue,
        LobbyMapAvailabilityState newValue) =>
        RefreshSelectionUI();

    private void OnAvailabilityMessageChanged(string oldValue, string newValue) =>
        RefreshSelectionUI();

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
