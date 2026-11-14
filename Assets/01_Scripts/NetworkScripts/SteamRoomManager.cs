using Mirror;
using Steamworks;
using FlipFriends.Network;
using System;
using System.Collections.Generic;
using System.Linq;
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
        var args = Environment.GetCommandLineArgs();
        int invite = Array.IndexOf(args, "+connect_lobby");
        if (invite >= 0 && invite + 1 < args.Length && ulong.TryParse(args[invite + 1], out var lobbyId))
            JoinLobby(new CSteamID(lobbyId));
    }

    private void BindMatchmakingEvents()
    {
        matchmaking.LobbyCreateFailed += HandleLobbyCreateFailed;
        matchmaking.LobbyEntered += HandleLobbyEntered;
        matchmaking.LobbyJoinFailed += HandleLobbyJoinFailed;
    }

    public override void OnApplicationQuit()
    {
        base.OnApplicationQuit();
        SteamAPI.Shutdown();
    }

    public void HostLobby(RoomType roomType, int maxPlayer)
    {
        if (NetworkServer.active || NetworkClient.active || currentLobbyID != CSteamID.Nil || matchmaking.IsPending)
        {
            Debug.LogWarning("이미 서버 또는 클라이언트가 실행 중입니다. 로비 생성을 건너뜁니다.");
            return;
        }

        StartHost();
        matchmaking.CreateLobby(roomType, maxPlayer);
    }

    public void HostQuantumLobby(int maxPlayer)
    {
        var flow = QuantumGameFlow.Instance;
        if (flow == null || flow.IsBusy || !flow.CheckLobbyRelease() ||
            currentLobbyID != CSteamID.Nil || matchmaking.IsPending || NetworkServer.active || NetworkClient.active) return;
        matchmaking.CreateLobby(RoomType.Private, maxPlayer, isQuantum: true);
        flow.ReportLobby("Steam 로비를 만드는 중…");
    }

    public void JoinPrivateLobby(string joinCode)
    {
        matchmaking.JoinPrivateLobby(joinCode);
    }

    public Task JoinQuantumLobbyByCodeAsync(string code)
    {
        var flow = QuantumGameFlow.Instance;
        if (flow == null || flow.IsBusy || !flow.CheckLobbyRelease() ||
            currentLobbyID != CSteamID.Nil || matchmaking.IsPending || NetworkServer.active || NetworkClient.active)
            return Task.CompletedTask;

        var join = matchmaking.JoinQuantumLobbyByCodeAsync(code);
        flow.ReportLobby("방을 찾는 중…");
        return join;
    }

    public bool IsLobbyRequestPending => matchmaking.IsPending;

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
        else QuantumGameFlow.Instance?.ReportLobby("Steam 로비를 만들 수 없습니다. 다시 시도하세요.");
    }

    private void HandleLobbyJoinFailed() =>
        QuantumGameFlow.Instance?.ShowLobbyNotice("방에 들어갈 수 없어요. 방 코드와 방이 열려 있는지 확인해 주세요.");

    private void HandleLobbyEntered(CSteamID lobbyId, string hostAddress)
    {
        if (NetworkServer.active)
        {
            return;
        }

        bool isQuantum = SteamMatchmaking.GetLobbyData(lobbyId, SteamLobbyMatchmaking.QuantumModeKey) == SteamLobbyMatchmaking.QuantumMode;
        if (isQuantum)
        {
            var flow = QuantumGameFlow.Instance;
            if (flow == null || flow.IsBusy || !flow.CheckLobbyRelease())
            {
                matchmaking.LeaveLobby();
                if (flow != null && !flow.IsBusy) flow.ShowLobbyNotice(flow.Message);
                return;
            }
            flow.AttachLobby(new SteamQuantumLobby(this, lobbyId));
            return;
        }

        if (string.IsNullOrEmpty(hostAddress) || !ulong.TryParse(hostAddress, out ulong _))
        {
            Debug.LogError("유효하지 않은 호스트 주소입니다. Steam ID를 확인하세요.");
            matchmaking.LeaveLobby();
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
        matchmaking.LobbyJoinFailed -= HandleLobbyJoinFailed;
        matchmaking.Dispose();
        base.OnDestroy();
        // Main으로 돌아올 때 파괴되는 중복 인스턴스가 살아 있는 인스턴스 참조를 지우면 안 된다.
        if (Instance == this)
            Instance = null;
    }

    private sealed class SteamQuantumLobby : IQuantumLobby
    {
        private const string StateKey = "ff.q.state";
        private const string ReadyKey = "ff.q.ready";
        private readonly SteamRoomManager manager;
        private readonly CSteamID lobby;

        public SteamQuantumLobby(SteamRoomManager manager, CSteamID lobby)
        {
            this.manager = manager;
            this.lobby = lobby;
            LocalId = SteamUser.GetSteamID().ToString();
        }

        public string LocalId { get; }
        public string Code => manager.lobbyKeyStr;
        private bool IsLive => manager.currentLobbyID == lobby && SteamUser.BLoggedOn();
        public string OwnerId
        {
            get
            {
                var owner = IsLive ? SteamMatchmaking.GetLobbyOwner(lobby) : CSteamID.Nil;
                return owner == CSteamID.Nil ? null : owner.ToString();
            }
        }
        public string[] Members => !IsLive ? Array.Empty<string>() :
            Enumerable.Range(0, SteamMatchmaking.GetNumLobbyMembers(lobby))
                .Select(i => SteamMatchmaking.GetLobbyMemberByIndex(lobby, i).ToString())
                .OrderBy(id => id, StringComparer.Ordinal).ToArray();

        public string ReadState() => SteamMatchmaking.GetLobbyData(lobby, StateKey);
        public bool WriteState(string value) => SteamMatchmaking.SetLobbyData(lobby, StateKey, value);
        public string MemberName(string member)
        {
            if (!ulong.TryParse(member, out ulong id)) return "";
            string name = SteamFriends.GetFriendPersonaName(new CSteamID(id));
            return name.Length <= 12 ? name : name.Substring(0, 12);
        }
        public string ReadReady(string member) => ulong.TryParse(member, out ulong id)
            ? SteamMatchmaking.GetLobbyMemberData(lobby, new CSteamID(id), ReadyKey) : null;
        public void WriteReady(string value) => SteamMatchmaking.SetLobbyMemberData(lobby, ReadyKey, value);
        public bool SetJoinable(bool value) => SteamMatchmaking.SetLobbyJoinable(lobby, value);
        public void Leave()
        {
            if (manager.currentLobbyID == lobby) manager.matchmaking.LeaveLobby();
        }
        public void Invite()
        {
            if (SteamUtils.IsOverlayEnabled()) SteamFriends.ActivateGameOverlayInviteDialog(lobby);
            else QuantumGameFlow.Instance?.ReportLobby("Steam 오버레이를 사용할 수 없습니다. 로비 코드를 전달하세요.");
        }
    }
}
