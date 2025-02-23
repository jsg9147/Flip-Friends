using UnityEngine;
using Mirror;
using System.Collections.Generic;
// 기본적인 Mirror 네트워크 기능을 처리하는 RoomManager
public class SlimeRoomManager : NetworkRoomManager
{
    private List<GameObject> lobbyPlayerList;

    public int currentStage = 0;

    private bool shouldReconnectPlayers = false; // 씬 변경 후 재실행 플래그
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
        ServerChangeScene(RoomScene);
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