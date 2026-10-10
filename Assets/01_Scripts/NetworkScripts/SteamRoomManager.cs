using Mirror;
using Steamworks;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

public class SteamRoomManager : SlimeRoomManager
{
    public static SteamRoomManager Instance { get; private set; }

    private readonly SteamLobbyMatchmaking matchmaking = new SteamLobbyMatchmaking();

    public List<SteamLobbyInfo> lobbyInfos => matchmaking.LobbyInfos;
    public string lobbyKeyStr => matchmaking.LobbyKey;
    public CSteamID currentLobbyID => matchmaking.CurrentLobbyId;

    public string playerName { get; private set; }

    public override void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("중복된 SteamRoomManager가 존재하여 파괴됩니다.");
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (!SteamAPI.Init())
        {
            Debug.LogError("SteamAPI 초기화 실패");
            Application.Quit();
            return;
        }

        base.Awake();
    }

    public override void Start()
    {
        base.Start();
        BindMatchmakingEvents();
        matchmaking.Initialize();
        playerName = SteamFriends.GetFriendPersonaName(SteamUser.GetSteamID());
    }

    private void BindMatchmakingEvents()
    {
        matchmaking.LobbyCreateFailed += HandleLobbyCreateFailed;
        matchmaking.LobbyEntered += HandleLobbyEntered;
    }

    public override void OnApplicationQuit()
    {
        base.OnApplicationQuit();
        SteamAPI.Shutdown();
    }

    public void HostLobby(RoomType roomType, int maxPlayer)
    {
        if (NetworkServer.active || NetworkClient.active)
        {
            Debug.LogWarning("이미 서버 또는 클라이언트가 실행 중입니다. 로비 생성을 건너뜁니다.");
            return;
        }

        StartHost();
        matchmaking.CreateLobby(roomType, maxPlayer);
    }

    public void JoinPrivateLobby(string joinCode)
    {
        matchmaking.JoinPrivateLobby(joinCode);
    }

    public void JoinLobby(CSteamID joinId)
    {
        matchmaking.JoinLobby(joinId);
    }

    public Task<List<SteamLobbyInfo>> GetLobbyListAsync()
    {
        return matchmaking.GetLobbyListAsync();
    }

    public void LeaveLobby()
    {
        matchmaking.LeaveLobby();
        StopNetworkSession();
    }

    // 게임 진행 중에는 Mirror가 새 연결을 끊는다. Steam 로비에 들어온 뒤 끊기면
    // 로비 멤버로만 남으므로 GameRoom에 있을 때만 입장을 받는다.
    public override void OnRoomServerSceneChanged(string sceneName)
    {
        base.OnRoomServerSceneChanged(sceneName);
        matchmaking.SetLobbyJoinable(Utils.IsSceneActive(RoomScene));
    }

    private void HandleLobbyCreateFailed()
    {
        // StartHost 이후 CreateLobby 실패 시 고아 호스트 세션을 정리
        if (NetworkServer.active)
        {
            StopHost();
        }
    }

    private void HandleLobbyEntered(CSteamID lobbyId, string hostAddress)
    {
        if (NetworkServer.active)
        {
            return;
        }

        if (string.IsNullOrEmpty(hostAddress) || !ulong.TryParse(hostAddress, out ulong _))
        {
            Debug.LogError("유효하지 않은 호스트 주소입니다. Steam ID를 확인하세요.");
            return;
        }

        networkAddress = hostAddress;
        StartClient();
    }

    private void StopNetworkSession()
    {
        if (NetworkServer.active)
        {
            StopHost();
            return;
        }

        if (NetworkClient.active)
        {
            StopClient();
        }
    }

    public override void OnDestroy()
    {
        matchmaking.LobbyCreateFailed -= HandleLobbyCreateFailed;
        matchmaking.LobbyEntered -= HandleLobbyEntered;
        base.OnDestroy();
        // Main으로 돌아올 때 파괴되는 중복 인스턴스가 살아 있는 인스턴스 참조를 지우면 안 된다.
        if (Instance == this)
            Instance = null;
    }
}
