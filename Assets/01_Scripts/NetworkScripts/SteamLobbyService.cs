using FlipFriends.Network;
using Steamworks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Steam 로비 생성·참가·초대와 Quantum 로비 연결을 맡는다.
/// </summary>
public sealed class SteamLobbyService : MonoBehaviour
{
    private const string ConnectLobbyArgument = "+connect_lobby";

    private static SteamLobbyService instance;

    // 초대 콜백과 +connect_lobby를 놓치지 않도록 Main 씬의 SteamLobbyService 오브젝트가 일찍 만든다. 지연 생성은 다른 씬에서 바로 시작했을 때를 위한 대비다.
    public static SteamLobbyService Instance => instance != null ? instance : Create();
    public static SteamLobbyService Current => instance;

    private readonly SteamLobbyMatchmaking matchmaking = new SteamLobbyMatchmaking();
    public string LobbyKey => matchmaking.LobbyKey;
    public CSteamID CurrentLobbyId => matchmaking.CurrentLobbyId;
    public string PlayerName { get; private set; }
    public bool IsLobbyRequestPending => matchmaking.IsPending;
    public bool CanCreateLobby => CurrentLobbyId == CSteamID.Nil && !matchmaking.IsPending;

    private static SteamLobbyService Create()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("플레이 중이 아니어서 SteamLobbyService를 만들지 않습니다.");
            return null;
        }

        var owner = new GameObject(nameof(SteamLobbyService));
        DontDestroyOnLoad(owner);
        return owner.AddComponent<SteamLobbyService>();
    }

    private void Awake()
    {
        // Main을 다시 불러올 때마다 씬 오브젝트가 새로 생기므로 이미 살아 있는 인스턴스를 남긴다.
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        if (!SteamManager.Initialized)
        {
            Debug.LogWarning("Steam이 초기화되지 않아 로비 기능을 사용할 수 없습니다.", this);
            return;
        }

        // 호출자가 같은 프레임에 로비를 요청해도 콜백을 받도록 Awake에서 등록한다.
        matchmaking.LobbyCreateFailed += HandleLobbyCreateFailed;
        matchmaking.LobbyEntered += HandleLobbyEntered;
        matchmaking.LobbyJoinFailed += HandleLobbyJoinFailed;
        matchmaking.Initialize();
        PlayerName = SteamFriends.GetFriendPersonaName(SteamUser.GetSteamID());
    }

    private void Start()
    {
        if (instance != this || !SteamManager.Initialized) return;
        string[] args = Environment.GetCommandLineArgs();
        int invite = Array.IndexOf(args, ConnectLobbyArgument);
        if (invite >= 0 && invite + 1 < args.Length && ulong.TryParse(args[invite + 1], out ulong lobbyId))
            JoinLobby(new CSteamID(lobbyId));
    }

    public void HostQuantumLobby(int maxPlayer)
    {
        var flow = QuantumGameFlow.Instance;
        if (flow == null || flow.IsBusy || !flow.CheckLobbyRelease() || !CanCreateLobby) return;
        matchmaking.CreateLobby(maxPlayer);
        flow.ReportLobby("Steam 로비를 만드는 중…");
    }

    public Task JoinQuantumLobbyByCodeAsync(string code)
    {
        var flow = QuantumGameFlow.Instance;
        if (flow == null || flow.IsBusy || !flow.CheckLobbyRelease() || !CanCreateLobby)
            return Task.CompletedTask;

        var join = matchmaking.JoinQuantumLobbyByCodeAsync(code);
        flow.ReportLobby("방을 찾는 중…");
        return join;
    }

    public void JoinLobby(CSteamID lobbyId) => matchmaking.JoinLobby(lobbyId);

    public void LeaveLobby() => matchmaking.LeaveLobby();

    private void HandleLobbyCreateFailed() =>
        QuantumGameFlow.Instance?.ReportLobby("Steam 로비를 만들 수 없습니다. 다시 시도하세요.");

    private void HandleLobbyJoinFailed() =>
        QuantumGameFlow.Instance?.ShowLobbyNotice("방에 들어갈 수 없어요. 방 코드와 방이 열려 있는지 확인해 주세요.");

    private void HandleLobbyEntered(CSteamID lobbyId)
    {
        // 이전 Mirror 빌드가 만든 로비나 다른 형식의 로비는 Quantum 세션으로 이어 붙일 수 없다.
        bool isQuantum = SteamMatchmaking.GetLobbyData(lobbyId, SteamLobbyMatchmaking.QuantumModeKey) == SteamLobbyMatchmaking.QuantumMode;
        if (!isQuantum)
        {
            Debug.LogWarning($"Quantum 로비가 아니어서 나갑니다: lobby={lobbyId}", this);
            matchmaking.LeaveLobby();
            QuantumGameFlow.Instance?.ShowLobbyNotice("지원하지 않는 방입니다. 게임을 최신 버전으로 업데이트했는지 확인해 주세요.");
            return;
        }

        var flow = QuantumGameFlow.Instance;
        if (flow == null || flow.IsBusy || !flow.CheckLobbyRelease())
        {
            matchmaking.LeaveLobby();
            if (flow != null && !flow.IsBusy) flow.ShowLobbyNotice(flow.Message);
            return;
        }
        flow.AttachLobby(new SteamQuantumLobby(this, lobbyId));
    }

    private void OnDestroy()
    {
        if (instance != this) return;
        matchmaking.LobbyCreateFailed -= HandleLobbyCreateFailed;
        matchmaking.LobbyEntered -= HandleLobbyEntered;
        matchmaking.LobbyJoinFailed -= HandleLobbyJoinFailed;
        matchmaking.Dispose();
        instance = null;
    }

    private sealed class SteamQuantumLobby : IQuantumLobby
    {
        private const string StateKey = "ff.q.state";
        private const string ReadyKey = "ff.q.ready";
        private const int MemberNameLength = 12;
        private readonly SteamLobbyService service;
        private readonly CSteamID lobby;

        public SteamQuantumLobby(SteamLobbyService service, CSteamID lobby)
        {
            this.service = service;
            this.lobby = lobby;
            LocalId = SteamUser.GetSteamID().ToString();
        }

        public string LocalId { get; }
        public string Code => service.LobbyKey;
        private bool IsLive => service != null && service.CurrentLobbyId == lobby && SteamUser.BLoggedOn();
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
            return name.Length <= MemberNameLength ? name : name.Substring(0, MemberNameLength);
        }
        public string ReadReady(string member) => ulong.TryParse(member, out ulong id)
            ? SteamMatchmaking.GetLobbyMemberData(lobby, new CSteamID(id), ReadyKey) : null;
        public void WriteReady(string value) => SteamMatchmaking.SetLobbyMemberData(lobby, ReadyKey, value);
        public bool SetJoinable(bool value) => SteamMatchmaking.SetLobbyJoinable(lobby, value);
        public void Leave()
        {
            if (service != null && service.CurrentLobbyId == lobby) service.LeaveLobby();
        }
        public void Invite()
        {
            if (SteamUtils.IsOverlayEnabled()) SteamFriends.ActivateGameOverlayInviteDialog(lobby);
            else QuantumGameFlow.Instance?.ReportLobby("Steam 오버레이를 사용할 수 없습니다. 로비 코드를 전달하세요.");
        }
    }
}
