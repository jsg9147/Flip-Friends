using UnityEngine;
using Mirror;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
// 기본적인 Mirror 네트워크 흐름을 처리하는 RoomManager
public class SlimeRoomManager : NetworkRoomManager
{
    private List<GameObject> lobbyPlayerList;

    public string currentBuiltInMapId = string.Empty;

    public string currentMapId = string.Empty;
    public string currentMapContentHash = string.Empty;
    public ServerMapSessionStore ServerMapSession { get; } = new();

    private bool shouldReconnectPlayers = false; // 씬 전환 후 플레이어 재연결 플래그

    private MapSessionSnapshot testPlaySnapshot;
    private MapEditorPalette testPlayPalette;
    private string testPlayReturnScene;
    private string offlineSceneBeforeTestPlay;
    private bool isTestPlayEnding;

    public bool IsTestPlaying => testPlaySnapshot != null;

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
        if (IsTestPlaying)
            StartCoroutine(ReturnFromTestPlayAfterStop());
    }

    // 에디터에서 편집 중인 맵을 혼자 플레이한다. Steam 로비는 만들지 않는다.
    public bool TryStartTestPlay(
        MapSessionSnapshot snapshot,
        MapEditorPalette palette,
        string returnScene,
        out string error)
    {
        if (NetworkServer.active || NetworkClient.active)
        {
            error = "이미 네트워크 세션이 실행 중이라 테스트 플레이를 시작할 수 없습니다.";
            return false;
        }

        if (snapshot == null || palette == null || string.IsNullOrEmpty(returnScene))
        {
            error = "테스트 플레이에 필요한 맵, 팔레트 또는 복귀 씬이 없습니다.";
            return false;
        }

        testPlaySnapshot = snapshot;
        testPlayPalette = palette;
        testPlayReturnScene = returnScene;
        offlineSceneBeforeTestPlay = offlineScene;
        isTestPlayEnding = false;
        // offline 씬이 있으면 Mirror가 종료 시 매니저를 DDOL에서 빼고 Main으로 보낸다.
        // 에디터로 돌아가야 하므로 테스트 플레이 동안 비워 둔다.
        offlineScene = string.Empty;
        StartHost();
        if (!NetworkServer.active)
        {
            ClearTestPlayState();
            error = "테스트 플레이 호스트를 시작하지 못했습니다. Steam 로그인 상태를 확인하세요.";
            return false;
        }

        error = null;
        return true;
    }

    public void EndTestPlay()
    {
        if (!IsTestPlaying || isTestPlayEnding) return;

        isTestPlayEnding = true;
        // Command 처리 도중 서버를 내리지 않도록 다음 프레임에 종료한다.
        StartCoroutine(StopTestPlayHostNextFrame());
    }

    public override void OnServerAddPlayer(NetworkConnectionToClient conn)
    {
        base.OnServerAddPlayer(conn);
        if (IsTestPlaying && conn.identity != null && Utils.IsSceneActive(RoomScene))
            StartCoroutine(BeginTestPlayGameplayNextFrame());
    }

    private IEnumerator BeginTestPlayGameplayNextFrame()
    {
        // AddPlayer 메시지를 처리하는 도중에 씬을 바꾸지 않도록 한 프레임 미룬다.
        yield return null;
        if (!IsTestPlaying || isTestPlayEnding || !NetworkServer.active) yield break;

        if (!ServerMapSession.TryStore(testPlaySnapshot, testPlayPalette, out string error))
        {
            Debug.LogError($"테스트 플레이 맵을 서버 세션에 넣지 못했습니다: {error}", this);
            EndTestPlay();
            yield break;
        }

        currentBuiltInMapId = string.Empty;
        currentMapId = testPlaySnapshot.MapId;
        currentMapContentHash = testPlaySnapshot.ContentHash;
        ServerChangeScene(GameplayScene);
    }

    private IEnumerator StopTestPlayHostNextFrame()
    {
        yield return null;
        if (NetworkServer.active)
            StopHost();
    }

    private IEnumerator ReturnFromTestPlayAfterStop()
    {
        // StopServer가 offline 씬 처리를 끝낸 뒤에 원래 값을 되돌려야 한다.
        yield return null;
        string returnScene = testPlayReturnScene;
        ClearTestPlayState();
        SceneManager.LoadScene(returnScene);
    }

    private void ClearTestPlayState()
    {
        offlineScene = offlineSceneBeforeTestPlay;
        testPlaySnapshot = null;
        testPlayPalette = null;
        testPlayReturnScene = null;
        offlineSceneBeforeTestPlay = null;
        isTestPlayEnding = false;
        currentBuiltInMapId = string.Empty;
        currentMapId = string.Empty;
        currentMapContentHash = string.Empty;
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
        if (IsTestPlaying)
        {
            EndTestPlay();
            return;
        }

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
