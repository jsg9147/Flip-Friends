using UnityEngine;
using Mirror;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
// 기본적인 Mirror 네트워크 흐름을 처리하는 RoomManager
public class SlimeRoomManager : NetworkRoomManager
{
    private List<GameObject> lobbyPlayerList;

    public int currentStage = 0;

    public string currentMapId = string.Empty;
    public string currentMapContentHash = string.Empty;
    public ServerMapSessionStore ServerMapSession { get; } = new();

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
        ServerMapSession.Clear();
        base.OnStartServer();
    }

    public override void OnStopServer()
    {
        ServerMapSession.Clear();
        base.OnStopServer();
    }

    public override void OnStopClient()
    {
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
        currentMapId = string.Empty;
        currentMapContentHash = string.Empty;
        ServerMapSession.Clear();
        MapSessionCache.Clear();
        ServerChangeScene(RoomScene);
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
