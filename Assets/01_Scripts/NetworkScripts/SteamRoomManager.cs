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
    public PendingRoomMapSelection PendingMapSelection { get; private set; }
    public LobbyMapMetadata CurrentLobbyMapMetadata { get; private set; }

    public bool TrySetPendingMapSelection(PendingRoomMapSelection selection)
    {
        if (selection == null ||
            !selection.TryCreateLobbyMetadata(out _))
        {
            Debug.LogWarning("방 생성에 사용할 맵 정책 또는 선택 정보가 올바르지 않습니다.");
            return false;
        }

        PendingMapSelection = selection;
        return true;
    }

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

        if (PendingMapSelection == null ||
            !PendingMapSelection.TryCreateLobbyMetadata(out LobbyMapMetadata metadata) ||
            !CanCreateLobby(
                roomType,
                maxPlayer,
                metadata,
                PendingMapSelection.CompletionTarget))
        {
            return;
        }

        CurrentLobbyMapMetadata = metadata;
        ConfigureRoomMap(metadata);
        StartHost();
        if (!matchmaking.CreateLobby(
                roomType,
                maxPlayer,
                metadata,
                PendingMapSelection.CompletionTarget))
        {
            StopHost();
            CurrentLobbyMapMetadata = null;
        }
    }

    private static bool CanCreateLobby(
        RoomType roomType,
        int maxPlayer,
        LobbyMapMetadata metadata,
        MapCompletionTarget completionTarget)
    {
        RoomCreationEligibility eligibility =
            MapCompletionProgress.EvaluateRoomCreation(
                roomType == RoomType.Public,
                maxPlayer,
                metadata,
                completionTarget);
        if (eligibility.CanCreate) return true;

        Debug.LogWarning(
            $"Steam 호스트 생성을 거부했습니다: reason={eligibility.BlockReason}, " +
            $"roomType={roomType}, map={metadata?.MapKey}");
        return false;
    }

    public void JoinPrivateLobby(string joinCode)
    {
        matchmaking.JoinPrivateLobby(joinCode);
    }

    public void JoinLobby(CSteamID joinId)
    {
        matchmaking.JoinLobby(joinId);
    }

    public Task<List<SteamLobbyInfo>> GetLobbyListAsync(
        LobbyMapFilter? filter = null)
    {
        return matchmaking.GetLobbyListAsync(filter);
    }

    public void LeaveLobby()
    {
        matchmaking.LeaveLobby();
        StopNetworkSession();
        PendingMapSelection = null;
        CurrentLobbyMapMetadata = null;
    }

    private void HandleLobbyCreateFailed()
    {
        // StartHost 이후 CreateLobby 실패 시 고아 호스트 세션을 정리
        if (NetworkServer.active)
        {
            StopHost();
        }
        PendingMapSelection = null;
        CurrentLobbyMapMetadata = null;
    }

    private void HandleLobbyEntered(CSteamID lobbyId, string hostAddress)
    {
        SteamLobbyInfo lobbyInfo = new SteamLobbyInfo(lobbyId);
        CurrentLobbyMapMetadata = lobbyInfo.MapMetadata;
        ConfigureRoomMap(CurrentLobbyMapMetadata);

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
        Instance = null;
    }
}
