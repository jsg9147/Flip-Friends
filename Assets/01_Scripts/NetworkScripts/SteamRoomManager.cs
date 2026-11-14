using Mirror;
using Steamworks;
using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 기존 Mirror GameRoom 경로만 맡는다. Steam 로비 자체는 SteamLobbyService가 소유한다.
/// </summary>
public class SteamRoomManager : SlimeRoomManager
{
    public static SteamRoomManager Instance { get; private set; }

    private SteamLobbyService lobbyService;
    private Func<bool> isMirrorSessionActive;

    public List<SteamLobbyInfo> lobbyInfos => lobbyService != null ? lobbyService.LobbyInfos : new List<SteamLobbyInfo>();
    public string lobbyKeyStr => lobbyService != null ? lobbyService.LobbyKey : null;
    public CSteamID currentLobbyID => lobbyService != null ? lobbyService.CurrentLobbyId : CSteamID.Nil;
    public string playerName => lobbyService != null ? lobbyService.PlayerName : null;

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
        lobbyService = SteamLobbyService.Instance;
        if (lobbyService == null) return;
        isMirrorSessionActive = () => NetworkServer.active || NetworkClient.active;
        lobbyService.RegisterExternalSession(isMirrorSessionActive);
        lobbyService.ExternalLobbyCreateFailed += HandleLobbyCreateFailed;
        lobbyService.ExternalLobbyEntered += HandleLobbyEntered;
    }

    public override void OnApplicationQuit()
    {
        base.OnApplicationQuit();
        SteamAPI.Shutdown();
    }

    public void HostLobby(RoomType roomType, int maxPlayer)
    {
        if (lobbyService == null || NetworkServer.active || NetworkClient.active || !lobbyService.CanCreateLobby)
        {
            Debug.LogWarning("이미 서버·클라이언트가 실행 중이거나 로비 요청이 진행 중입니다. 로비 생성을 건너뜁니다.");
            return;
        }

        StartHost();
        lobbyService.CreateExternalLobby(roomType, maxPlayer);
    }

    public void LeaveLobby()
    {
        lobbyService?.LeaveLobby();
        StopNetworkSession();
    }

    // 게임 진행 중에는 Mirror가 새 연결을 끊는다. Steam 로비에 들어온 뒤 끊기면
    // 로비 멤버로만 남으므로 GameRoom에 있을 때만 입장을 받는다.
    public override void OnRoomServerSceneChanged(string sceneName)
    {
        base.OnRoomServerSceneChanged(sceneName);
        lobbyService?.SetLobbyJoinable(Utils.IsSceneActive(RoomScene));
    }

    // StartHost 이후 CreateLobby 실패 시 고아 호스트 세션을 정리
    private void HandleLobbyCreateFailed()
    {
        if (NetworkServer.active)
            StopHost();
    }

    private void HandleLobbyEntered(CSteamID lobbyId, string hostAddress)
    {
        // 방장은 StartHost 뒤 자기 로비에 들어온 것이므로 접속할 필요가 없다.
        if (NetworkServer.active)
            return;

        if (string.IsNullOrEmpty(hostAddress) || !ulong.TryParse(hostAddress, out ulong _))
        {
            Debug.LogError($"유효하지 않은 호스트 주소입니다. Steam ID를 확인하세요: lobby={lobbyId}");
            lobbyService.LeaveLobby();
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
        if (lobbyService != null)
        {
            lobbyService.ExternalLobbyCreateFailed -= HandleLobbyCreateFailed;
            lobbyService.ExternalLobbyEntered -= HandleLobbyEntered;
            lobbyService.UnregisterExternalSession(isMirrorSessionActive);
        }
        base.OnDestroy();
        // Main으로 돌아올 때 파괴되는 중복 인스턴스가 살아 있는 인스턴스 참조를 지우면 안 된다.
        if (Instance == this)
            Instance = null;
    }
}
