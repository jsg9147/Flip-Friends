using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using Mirror;
using Steamworks;
using System.Collections;
using Unity.Collections.LowLevel.Unsafe;

public class CustomRoomPlayer : NetworkRoomPlayer
{
    private const int MaximumChunkCount = 64;
    private const int MaximumMapBytes = 512 * 1024;
    private const int SelectionChunkBytes = 24 * 1024;
    [SyncVar(hook = nameof(OnNameChanged))]
    public string playerName = "No Name";

    [SyncVar(hook = nameof(OnColorChange))] public Vector4 playerColor;

    private PlayerController2D playerController;

    private GameObject notReadyText;

    private bool gameStart;
    private MapChunkAssembler chunkAssembler;

    public override void OnClientEnterRoom()
    {
        base.OnClientEnterRoom();
        Init();
    }

    private void OnEnable()
    {
        if (InputManager.instance != null)
        {
            InputManager.instance.OnSubmitEvent += OnReady;
        }
    }
    public override void OnDisable()
    {
        if (InputManager.instance != null)
        {
            InputManager.instance.OnSubmitEvent -= OnReady;
        }
        chunkAssembler = null;
        base.OnDisable();
    }

    public void Init()
    {
        if (isLocalPlayer)
        {
            gameStart = false;
            SteamRoomManager roomManager = NetworkManager.singleton as SteamRoomManager;
            if (roomManager != null && NetworkServer.active)
            {
                CmdSetPlayerName(roomManager.playerName);
            }

            CmdSetPlayerColor(new Vector4(PlayerPrefs.GetFloat("Red", 0.3f), PlayerPrefs.GetFloat("Green", 1.0f), PlayerPrefs.GetFloat("Blue", 1.0f), 1f));
            notReadyText = GameObject.Find("NotReadyText");

            MapSelectionManager mapSelectionManager = FindAnyObjectByType<MapSelectionManager>();
            if(mapSelectionManager != null)
                mapSelectionManager.BindRoomPlayer(this);

            foreach (var lobbyPlayer in FindObjectsByType<PlayerController2D>())
            {
                if (lobbyPlayer.isOwned)
                {
                    playerController = lobbyPlayer;
                }
            }
        }
    }

    [Command]
    void CmdSetPlayerName(string newName)
    {
        playerName = newName; // �������� �÷��̾� �̸� ����
    }

    [Command]
    void CmdSetPlayerColor(Vector4 color)
    {
        playerColor = color;
    }

    void OnNameChanged(string oldName, string newName)
    {
        if (playerController != null)
        {
            playerController.CmdSetPlayerName(newName);
        }
        else
        {
            foreach (var lobbyPlayer in FindObjectsByType<PlayerController2D>())
            {
                if (isLocalPlayer && lobbyPlayer.isOwned)
                {
                    playerController = lobbyPlayer;
                    lobbyPlayer.CmdSetPlayerName(playerName);
                }
            }
        }
    }

    void OnColorChange(Vector4 oldColor, Vector4 newColor)
    {
        if (playerController != null)
        {
            playerController.CmdSetPlayerColor(newColor);
        }
        else
        {
            foreach (var lobbyPlayer in FindObjectsByType<PlayerController2D>())
            {
                if (isLocalPlayer && lobbyPlayer.isOwned)
                {
                    playerController = lobbyPlayer;
                    lobbyPlayer.CmdSetPlayerColor(newColor);
                }
            }
        }
    }


    private void OnReady()
    {
        if (gameStart)
            return;

        if (isLocalPlayer)
        {
            CmdChangeReadyState(!readyToBegin);

            if (isLocalPlayer && notReadyText != null)
            {
                notReadyText.gameObject.SetActive(!readyToBegin);
            }
        }
    }

    public override void ReadyStateChanged(bool oldReadyState, bool newReadyState)
    {
        base.ReadyStateChanged(oldReadyState, newReadyState);
        ReadySpriteChanged(newReadyState);
    }

    void ReadySpriteChanged(bool isReady)
    {
        if (playerController != null && isLocalPlayer)
        {
            playerController.CmdSetPlayerReady(isReady);
        }
    }

    public void StageSelectionUISetAcitve(bool isActive)
    {
        gameStart = isActive;
        FindAnyObjectByType<MapSelectionManager>().MapSelectScreenSetActive(isActive);
        RpcStageSelectUIOn(isActive);
    }

    [ClientRpc]
    private void RpcStageSelectUIOn(bool isActive)
    {
        FindAnyObjectByType<MapSelectionManager>().MapSelectScreenSetActive(isActive);
        if(isOwned)
            playerController.gameObject.SetActive(!isActive);
    }

    [Command]
    public void CmdSelectBuiltInMap(int stage)
    {
        if (!ServerIsRoomHost()) return;

        FindAnyObjectByType<MapSelectionManager>()?.ServerSelectBuiltIn(stage);
    }

    public void UploadCustomMapSelection(SavedMapListEntry entry)
    {
        if (!isOwned || entry == null || string.IsNullOrEmpty(entry.Json)) return;

        MapData data = MapDataRepository.FromJson(entry.Json);
        if (data == null || data.mapId != entry.MapId)
        {
            Debug.LogWarning("선택할 커스텀 맵을 정규화하지 못했습니다.");
            return;
        }
        string canonicalJson = MapDataRepository.ToJson(data);
        if (canonicalJson == null) return;

        byte[] content = System.Text.Encoding.UTF8.GetBytes(canonicalJson);
        if (content.Length == 0 || content.Length > MaximumMapBytes) return;

        string transferId = System.Guid.NewGuid().ToString("N");
        string contentHash = MapContentHash.Compute(content);
        int chunkCount = Mathf.CeilToInt(content.Length / (float)SelectionChunkBytes);
        CmdBeginCustomMapSelection(
            transferId, data.mapId, contentHash, content.Length,
            chunkCount, entry.ValidationReport.HasWarnings);
        for (int index = 0; index < chunkCount; index++)
        {
            int offset = index * SelectionChunkBytes;
            int count = Mathf.Min(SelectionChunkBytes, content.Length - offset);
            byte[] payload = new byte[count];
            System.Buffer.BlockCopy(content, offset, payload, 0, count);
            CmdUploadCustomMapSelectionChunk(
                transferId, data.mapId, contentHash, chunkCount, index, payload);
        }
    }

    [Command]
    private void CmdBeginCustomMapSelection(
        string transferId,
        string mapId,
        string contentHash,
        int byteLength,
        int chunkCount,
        bool hasWarnings)
    {
        if (!ServerIsRoomHost()) return;

        FindAnyObjectByType<MapSelectionManager>()?
            .ServerBeginCustomSelectionUpload(
                this, transferId, mapId, contentHash,
                byteLength, chunkCount, hasWarnings);
    }

    [Command]
    private void CmdUploadCustomMapSelectionChunk(
        string transferId,
        string mapId,
        string contentHash,
        int chunkCount,
        int chunkIndex,
        byte[] payload)
    {
        if (!ServerIsRoomHost()) return;

        FindAnyObjectByType<MapSelectionManager>()?
            .ServerReceiveCustomSelectionChunk(
                this, transferId, mapId, contentHash,
                chunkCount, chunkIndex, payload);
    }

    [Command]
    public void CmdClearMapSelection()
    {
        if (!ServerIsRoomHost()) return;

        FindAnyObjectByType<MapSelectionManager>()?.ServerClearSelection();
    }

    [Command]
    public void CmdStartSelectedMap()
    {
        if (!ServerIsRoomHost()) return;

        MapSelectionManager selectionManager = FindAnyObjectByType<MapSelectionManager>();
        if (selectionManager == null)
        {
            TargetShowMapSelectionError("맵 선택 관리자를 찾을 수 없습니다.");
            return;
        }

        selectionManager.ServerRequestStart(this);
    }

    [TargetRpc]
    public void TargetRequestMapManifest(
        NetworkConnectionToClient target,
        uint generation,
        string transferId,
        string mapId,
        string contentHash,
        int byteLength,
        int chunkCount)
    {
        chunkAssembler = null;
        MapContentAvailability availability = InspectLocalContent(
            mapId, contentHash, byteLength);
        CmdReportMapManifest(
            generation, transferId, mapId, contentHash, availability);
    }

    [Command]
    private void CmdReportMapManifest(
        uint generation,
        string transferId,
        string mapId,
        string contentHash,
        MapContentAvailability availability)
    {
        FindAnyObjectByType<MapSelectionManager>()?.ServerReceiveManifest(
            this, generation, mapId, transferId, contentHash, availability);
    }

    [TargetRpc]
    public void TargetReceiveMapChunk(
        NetworkConnectionToClient target,
        uint generation,
        string transferId,
        string mapId,
        string contentHash,
        int byteLength,
        int chunkCount,
        int chunkIndex,
        byte[] payload)
    {
        if (!TryAcceptChunkHeader(
                generation, transferId, mapId, contentHash,
                byteLength, chunkCount, chunkIndex, payload))
            return;

        if (!chunkAssembler.TryAdd(
                chunkIndex, payload, MaximumMapBytes, out string chunkError))
        {
            ReportTransferFailure(
                generation, transferId, mapId, contentHash,
                MapTransferFailure.InvalidChunk, chunkError);
            return;
        }
        if (!chunkAssembler.IsComplete) return;

        CompleteChunkTransfer(generation, transferId, mapId, contentHash);
    }

    [Command]
    private void CmdReportMapTransfer(
        uint generation,
        string transferId,
        string mapId,
        string contentHash,
        MapTransferFailure failure)
    {
        FindAnyObjectByType<MapSelectionManager>()?.ServerReceiveTransferResult(
            this, generation, transferId, mapId, contentHash, failure);
    }

    private MapContentAvailability InspectLocalContent(
        string mapId,
        string contentHash,
        int byteLength)
    {
        if (MapSessionCache.TryGet(mapId, contentHash, out _))
            return MapContentAvailability.Available;
        if (!MapDataRepository.TryLoadById(
                mapId, out _, out MapData data, out string json,
                out MapRepositoryFailure failure))
        {
            return failure.Kind == MapRepositoryFailureKind.MissingFile
                ? MapContentAvailability.Missing
                : MapContentAvailability.InvalidLocalData;
        }

        string canonicalJson = MapDataRepository.ToJson(data);
        if (canonicalJson == null) return MapContentAvailability.InvalidLocalData;

        byte[] bytes = System.Text.Encoding.UTF8.GetBytes(canonicalJson);
        if (bytes.Length == byteLength && MapContentHash.Compute(bytes) == contentHash)
        {
            MapSessionCache.TryStore(
                mapId, contentHash, bytes,
                FindAnyObjectByType<MapSelectionManager>()?.Palette,
                out _);
            return MapContentAvailability.Available;
        }

        Debug.LogWarning(
            $"로컬 맵 콘텐츠가 manifest와 다릅니다: mapId={mapId}, " +
            $"localBytes={bytes.Length}, manifestBytes={byteLength}");
        return MapContentAvailability.HashMismatch;
    }

    private bool TryAcceptChunkHeader(
        uint generation,
        string transferId,
        string mapId,
        string contentHash,
        int byteLength,
        int chunkCount,
        int chunkIndex,
        byte[] payload)
    {
        bool valid = generation > 0 &&
                     !string.IsNullOrEmpty(transferId) &&
                     MapData.TryNormalizeMapId(mapId, out string normalizedMapId) &&
                     normalizedMapId == mapId &&
                     contentHash?.Length == 64 &&
                     byteLength > 0 && byteLength <= MaximumMapBytes &&
                     chunkCount > 0 && chunkCount <= MaximumChunkCount &&
                     chunkIndex >= 0 && chunkIndex < chunkCount &&
                     payload != null && payload.Length > 0;
        if (!valid)
        {
            ReportTransferFailure(
                generation, transferId, mapId, contentHash,
                MapTransferFailure.InvalidChunk, "청크 헤더 범위가 올바르지 않습니다.");
            return false;
        }

        if (chunkAssembler == null)
        {
            chunkAssembler = new MapChunkAssembler(
                generation, transferId, mapId, contentHash, byteLength, chunkCount);
        }
        if (chunkAssembler.Matches(
                generation, transferId, mapId, contentHash, chunkCount))
            return true;

        ReportTransferFailure(
            generation, transferId, mapId, contentHash,
            MapTransferFailure.SelectionChanged, "현재 manifest와 다른 청크를 거부했습니다.");
        return false;
    }

    private void CompleteChunkTransfer(
        uint generation,
        string transferId,
        string mapId,
        string contentHash)
    {
        if (!chunkAssembler.TryAssemble(out byte[] content, out string error))
        {
            ReportTransferFailure(
                generation, transferId, mapId, contentHash,
                MapTransferFailure.HashVerificationFailed, error);
            return;
        }

        MapEditorPalette palette =
            FindAnyObjectByType<MapSelectionManager>()?.Palette;
        if (!MapSessionCache.TryStore(mapId, contentHash, content, palette, out error))
        {
            ReportTransferFailure(
                generation, transferId, mapId, contentHash,
                MapTransferFailure.MapValidationFailed, error);
            return;
        }

        chunkAssembler = null;
        CmdReportMapTransfer(
            generation, transferId, mapId, contentHash, MapTransferFailure.None);
    }

    private void ReportTransferFailure(
        uint generation,
        string transferId,
        string mapId,
        string contentHash,
        MapTransferFailure failure,
        string reason)
    {
        chunkAssembler = null;
        Debug.LogWarning(
            $"커스텀 맵 청크를 거부했습니다: state={failure}, mapId={mapId}, " +
            $"transferId={transferId}, reason={reason}");
        CmdReportMapTransfer(
            generation, transferId, mapId, contentHash, failure);
    }

    [TargetRpc]
    public void TargetShowMapSelectionError(string message)
    {
        Debug.LogWarning(message ?? "맵 선택 상태를 확인할 수 없습니다.");
        FindAnyObjectByType<MapListUI>()?.ShowSelectionMessage(message, true);
    }

    [TargetRpc]
    public void TargetShowMapSelectionMessage(string message)
    {
        FindAnyObjectByType<MapListUI>()?.ShowSelectionMessage(message, false);
    }

    [Server]
    public bool ServerIsRoomHost()
    {
        bool isHost = index == 0;
        if (!isHost)
            Debug.LogWarning($"비방장 플레이어의 맵 선택 변경을 차단했습니다: {playerName}");
        return isHost;
    }
}
