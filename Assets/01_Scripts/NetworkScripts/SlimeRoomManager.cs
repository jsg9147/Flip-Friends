using UnityEngine;
using Mirror;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
// 기본적인 Mirror 네트워크 흐름을 처리하는 RoomManager
public class SlimeRoomManager : NetworkRoomManager
{
    private List<GameObject> lobbyPlayerList;

    public LobbyMapKind currentMapKind = LobbyMapKind.None;
    public string currentMapId = string.Empty;
    public string currentMapContentHash = string.Empty;
    public LobbyMapMetadata RoomMapMetadata { get; private set; }
    public ServerMapSessionStore ServerMapSession { get; } = new();
    public GameplayCompletionSession GameplayCompletionSession { get; private set; }

    private bool shouldReconnectPlayers = false; // 씬 전환 후 플레이어 재연결 플래그
    public override void OnStartHost()
    {
        base.OnStartHost();
        lobbyPlayerList = new List<GameObject>();
    }

    public override void OnRoomServerConnect(NetworkConnectionToClient conn)
    {
        base.OnRoomServerConnect(conn);

        var player = Instantiate(playerPrefab);
        NetworkServer.Spawn(player, conn);

        lobbyPlayerList.Add(player);
    }

    public override void OnStartServer()
    {
        ClearCurrentMap();
        GameplayCompletionSession = null;
        ServerMapSession.Clear();
        base.OnStartServer();
    }

    public override void OnStopServer()
    {
        ClearCurrentMap();
        RoomMapMetadata = null;
        GameplayCompletionSession = null;
        ServerMapSession.Clear();
        base.OnStopServer();
    }

    public override void OnStopClient()
    {
        RoomMapMetadata = null;
        MapSessionCache.Clear();
        base.OnStopClient();
    }

    public override void OnRoomClientSceneChanged()
    {
        base.OnRoomClientSceneChanged();
        Scene activeScene = SceneManager.GetActiveScene();
        if (activeScene.path == RoomScene || activeScene.name == RoomScene)
            MapSessionCache.Clear();
    }

    public override void OnRoomServerAddPlayer(NetworkConnectionToClient conn)
    {
        base.OnRoomServerAddPlayer(conn);
        FindAnyObjectByType<MapSelectionManager>()?
            .ServerInvalidateAvailabilityCheck("새 참여자가 입장했습니다.");
    }

    public override void OnRoomServerDisconnect(NetworkConnectionToClient conn)
    {
        FindAnyObjectByType<MapSelectionManager>()?
            .ServerInvalidateAvailabilityCheck("참여자가 연결을 종료했습니다.");
        base.OnRoomServerDisconnect(conn);
    }

    public override void OnRoomServerPlayersReady()
    {
        MapSelectionManager stageSelectUI = FindAnyObjectByType<MapSelectionManager>();

        if (stageSelectUI != null)
        {
            stageSelectUI.MapSelectScreenSetActive(true);
        }

        foreach (var player in roomSlots)
        {
            if (player.isOwned)
                player.GetComponent<CustomRoomPlayer>().StageSelectionUISetAcitve(true);
        }
    }

    public virtual void StartJoining(string networkAddress)
    {
        this.networkAddress = networkAddress;
        StartClient();
    }

    public virtual void ReturnRoomScene()
    {
        shouldReconnectPlayers = true;
        ClearCurrentMap();
        GameplayCompletionSession = null;
        ServerMapSession.Clear();
        MapSessionCache.Clear();
        ServerChangeScene(RoomScene);
    }

    public void SetCurrentMap(
        LobbyMapKind mapKind,
        string mapId,
        string contentHash)
    {
        currentMapKind = mapKind;
        currentMapId = mapId ?? string.Empty;
        currentMapContentHash = contentHash ?? string.Empty;
    }

    public void ConfigureRoomMap(LobbyMapMetadata metadata)
    {
        RoomMapMetadata = metadata;
    }

    public bool IsConfiguredMapSelection(
        LobbyMapKind mapKind,
        string mapId,
        int minimumPlayersToClear)
    {
        return RoomMapSessionRules.IsConfiguredSelectionValid(
            RoomMapMetadata,
            mapKind,
            mapId,
            minimumPlayersToClear);
    }

    public bool AllowsCustomMapTransfer() =>
        RoomMapSessionRules.AllowsCustomTransfer(RoomMapMetadata);

    [Server]
    public bool TryBeginGameplayCompletionSession(out string error)
    {
        GameplayCompletionSession = null;
        if (RoomMapMetadata == null ||
            !IsConfiguredMapSelection(
                currentMapKind,
                currentMapId,
                RoomMapMetadata.MinimumPlayersToClear))
        {
            error = "서버의 방 맵 설정과 실제 선택이 일치하지 않습니다.";
            return false;
        }
        if (currentMapKind == LobbyMapKind.Custom &&
            !ServerMapSession.TryGet(
                currentMapId, currentMapContentHash, out _))
        {
            error = "서버 커스텀맵 세션 스냅샷을 확인할 수 없습니다.";
            return false;
        }

        var participantConnectionIds = new List<int>();
        foreach (NetworkRoomPlayer roomPlayer in roomSlots)
        {
            if (roomPlayer?.connectionToClient == null || !roomPlayer.readyToBegin)
                continue;

            participantConnectionIds.Add(roomPlayer.connectionToClient.connectionId);
        }

        if (!GameplayCompletionSession.TryCreate(
                currentMapKind,
                currentMapId,
                currentMapContentHash,
                participantConnectionIds,
                out GameplayCompletionSession session))
        {
            error = "완료 판정용 서버 세션 스냅샷을 만들 수 없습니다.";
            return false;
        }

        GameplayCompletionSession = session;
        error = null;
        return true;
    }

    public void ClearCurrentMap()
    {
        SetCurrentMap(LobbyMapKind.None, string.Empty, string.Empty);
    }

    [Server]
    public bool AreAllRoomPlayersReady()
    {
        if (roomSlots.Count == 0) return false;

        foreach (NetworkRoomPlayer roomPlayer in roomSlots)
        {
            if (roomPlayer == null || !roomPlayer.readyToBegin)
                return false;
        }

        return true;
    }

    [Server]
    public int CountReadyRoomPlayers()
    {
        int readyPlayerCount = 0;
        foreach (NetworkRoomPlayer roomPlayer in roomSlots)
        {
            if (roomPlayer != null &&
                roomPlayer.connectionToClient != null &&
                roomPlayer.readyToBegin)
                readyPlayerCount++;
        }

        return readyPlayerCount;
    }

    public void DestroyAllLobbyPlayers()
    {
        foreach (GameObject player in lobbyPlayerList)
        {
            if (player != null)
            {
                NetworkServer.Destroy(player);
            }
        }
        lobbyPlayerList.Clear();
    }

    public override void OnRoomServerSceneChanged(string sceneName)
    {
        base.OnRoomServerSceneChanged(sceneName);
        FindAnyObjectByType<MapSelectionManager>()?
            .ServerInvalidateAvailabilityCheck($"씬이 전환되었습니다: {sceneName}");

        if (sceneName.Contains("GameRoom"))
        {
            ClearCurrentMap();
            GameplayCompletionSession = null;
            ServerMapSession.Clear();
        }

        if (sceneName.Contains("GameRoom") && shouldReconnectPlayers)
        {
            shouldReconnectPlayers = false;
            foreach (var roomPlayer in roomSlots)
            {
                var player = Instantiate(playerPrefab);
                NetworkServer.Spawn(player, roomPlayer.netIdentity.connectionToClient);

                lobbyPlayerList.Add(player);
            }
        }
    }
}
