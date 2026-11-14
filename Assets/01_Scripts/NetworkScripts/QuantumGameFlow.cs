using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FlipFriends.Maps;
using Photon.Realtime;
using Quantum;
using Steamworks;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;

namespace FlipFriends.Network
{
    public enum QuantumGameState { Ready, Starting, Playing, Stopping, Failed }

    public sealed class QuantumGameFlow : MonoBehaviour, IQuantumLobbyGame
    {
        public const string PlayScene = "QuantumPlay";
        [SerializeField] private QuantumCustomMapAsset[] supportedMaps;
        [SerializeField] private uint steamAppId;
        [SerializeField] private string releasePhotonAppId;
        public static QuantumGameFlow Instance { get; private set; }
        public QuantumGameState State { get; private set; }
        public bool IsBusy => State == QuantumGameState.Starting || State == QuantumGameState.Playing || State == QuantumGameState.Stopping;
        public string Message { get; private set; } = "맵과 참가 인원을 선택하고 장치 배정을 확인하세요.";
        public QuantumCustomMapAsset[] SupportedMaps => supportedMaps;
        // 공식 스테이지는 기존 사용례 뒤의 선택 번호를 쓴다. 기존 사용례 번호(0~5)는 바꾸지 않는다.
        public IReadOnlyList<QuantumOfficialStageAsset> OfficialStages { get; private set; } = Array.Empty<QuantumOfficialStageAsset>();
        public int MapCount => supportedMaps.Length + OfficialStages.Count;
        public int OfficialMapIndex(int stage) => supportedMaps.Length + stage;
        public IQuantumMapContent SelectedContent => SelectedMap < supportedMaps.Length ? supportedMaps[SelectedMap] : OfficialStages[SelectedMap - supportedMaps.Length];
        public string SavedMapName { get; private set; }
        private string savedFileName;
        private string catalogCursor;
        private MapEditorPalette SavedMapPalette => supportedMaps.FirstOrDefault(map => map != null && map.Palette != null)?.Palette;
        private const string SavedRoomPrefix = "ff-saved-";
        private const string SteamInviteArgument = "+ffquantum";
        private string hostRoomCode;
        private string pendingSteamInvite;
        private Callback<GameRichPresenceJoinRequested_t> steamInvite;
        public bool CanInviteSavedMapFriends => State == QuantumGameState.Starting && hostRoomCode != null;
        public bool HasSteamInvite => pendingSteamInvite != null;
        public bool HasRejectedSavedMap { get; private set; }
        public int SelectedMap { get; private set; }
        public int PlayerCount { get; private set; } = 2;
        public int KeyboardSlots { get; private set; } = 2;
        public bool HasLocalDevices => (KeyboardSlots == 0 || Keyboard.current != null) &&
            Gamepad.all.Count >= PlayerCount - KeyboardSlots;
        public QuantumGameSession Session { get; private set; }
        public QuantumMapConversion Content { get; private set; }
        public event Action Changed;
        private bool isDestroyed;
        private bool isSceneTransition;
        private Task operation;
        private Task returning;
        private string editorReturnScene;
        internal QuantumLobby Lobby { get; private set; }
        public bool ShouldShowLobbyScreen { get; private set; }
        private double nextLobbyPoll;

        internal void AttachLobby(IQuantumLobby transport, bool isDevelopment = false, Func<double> clock = null)
        {
            Lobby = new QuantumLobby(transport, this, isDevelopment, clock);
            ReportLobby("Steam 로비에 참가했습니다.");
            Lobby.Tick();
            if (Lobby.IsOwner && Lobby.MapName == null && OfficialStages.Count > 0)
                SelectMap(SelectedMap >= supportedMaps.Length ? SelectedMap : OfficialMapIndex(0));
            ShowLobbyScreen();
        }

        internal void DetachLobby(QuantumLobby lobby)
        {
            if (Lobby == lobby) Lobby = null;
            ShowLobbyScreen();
        }

        private void ShowLobbyScreen()
        {
            ShouldShowLobbyScreen = true;
            if (SceneManager.GetActiveScene().name == "Main")
                FindAnyObjectByType<ScreenNavigator>()?.Open("quantum-prepare", false);
        }

        public void AcknowledgeLobbyScreen() => ShouldShowLobbyScreen = false;

        private void Update()
        {
            if (Lobby == null || Time.realtimeSinceStartupAsDouble < nextLobbyPoll) return;
            nextLobbyPoll = Time.realtimeSinceStartupAsDouble + 0.1;
            Lobby.Tick();
        }

        bool IQuantumLobbyGame.CheckLobbyRelease() => CheckLobbyRelease();
        int IQuantumLobbyGame.LobbyMapMinimum(string map) => LobbyMapMinimum(map);
        void IQuantumLobbyGame.ReportLobby(string message) => ReportLobby(message);
        void IQuantumLobbyGame.RefreshLobby() => RefreshLobby();
        void IQuantumLobbyGame.DetachLobby(QuantumLobby lobby) => DetachLobby(lobby);
        Task IQuantumLobbyGame.ReturnFromLobbyAsync(string message) => ReturnFromLobbyAsync(message);
        Task IQuantumLobbyGame.StartLobbyAsync(QuantumLobbyState start, string localId, bool isOwner, MapSessionSnapshot source,
            bool isDevelopment, Action<string> regionReady) => StartLobbyAsync(start, localId, isOwner, source, isDevelopment, regionReady);

        internal void RefreshLobby() => Changed?.Invoke();

        internal void ReportLobby(string message)
        {
            Message = message;
            RefreshLobby();
        }

        internal void ShowLobbyNotice(string message)
        {
            if (IsBusy) return;
            ReportLobby(message);
            ShowLobbyScreen();
        }

        internal bool CheckLobbyRelease()
        {
            string reason = !QuantumGameSession.IsReleaseApp(releasePhotonAppId) || steamAppId == 0 || steamAppId == 480
                ? "출시 Steam 앱과 별도 출시 Photon 소유권 인증 설정이 필요합니다."
                : !SteamManager.Initialized || !SteamUser.BLoggedOn() || SteamUtils.GetAppID().m_AppId != steamAppId
                    ? "Steam 로그인과 출시 앱 실행 상태를 확인하세요."
                    : null;
            if (reason == null) return true;
            if (!IsBusy) SetState(QuantumGameState.Failed, reason);
            return false;
        }

        private QuantumOfficialStageAsset FindLobbyStage(string map) =>
            OfficialStages.FirstOrDefault(stage => "official:" + stage.StageId == map);

        internal int LobbyMapMinimum(string map) => map.StartsWith("saved:", StringComparison.Ordinal)
            ? 1 : FindLobbyStage(map)?.MinimumPlayers ?? 0;

        internal Task StartLobbyAsync(QuantumLobbyState start, string localId, bool isOwner, MapSessionSnapshot source,
            bool isDevelopment, Action<string> regionReady)
        {
            if (IsBusy || (!isDevelopment && !CheckLobbyRelease())) return Task.CompletedTask;
            SetState(QuantumGameState.Starting, "Photon 인증 및 참가자 콘텐츠 검증 중…");
            return operation = StartCoreAsync(true, lobbyStart: start, lobbySource: source,
                lobbyLocalId: localId, isLobbyOwner: isOwner, isDevelopment: isDevelopment, regionReady: regionReady);
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            var interactionMap = Resources.Load<QuantumCustomMapAsset>("FlipFriendsInteractionMapManifest");
            if (interactionMap != null && !supportedMaps.Contains(interactionMap)) supportedMaps = supportedMaps.Concat(new[] { interactionMap }).ToArray();
            var respawnMap = Resources.Load<QuantumCustomMapAsset>("FlipFriendsRespawnMapManifest");
            if (respawnMap != null && !supportedMaps.Contains(respawnMap)) supportedMaps = supportedMaps.Concat(new[] { respawnMap }).ToArray();
            var deviceMap = Resources.Load<QuantumCustomMapAsset>("FlipFriendsDeviceMapManifest");
            if (deviceMap != null && !supportedMaps.Contains(deviceMap)) supportedMaps = supportedMaps.Concat(new[] { deviceMap }).ToArray();
            var tilemap = Resources.Load<QuantumCustomMapAsset>("FlipFriendsTilemapMapManifest");
            if (tilemap != null && !supportedMaps.Contains(tilemap)) supportedMaps = supportedMaps.Concat(new[] { tilemap }).ToArray();
            try { OfficialStages = QuantumOfficialStageCatalog.LoadStages(); }
            catch (InvalidOperationException error) { Debug.LogException(error, this); }
            DontDestroyOnLoad(gameObject);
            SceneManager.sceneLoaded += OnSceneLoaded;
            Application.runInBackground = true;
        }

        private void Start()
        {
            if (!QuantumGameSession.IsReleaseApp(releasePhotonAppId) || steamAppId == 0 || steamAppId == 480 ||
                !SteamManager.Initialized || SteamUtils.GetAppID().m_AppId != steamAppId) return;

            steamInvite = Callback<GameRichPresenceJoinRequested_t>.Create(invite =>
            {
                string prefix = SteamInviteArgument + " ";
                if (invite.m_rgchConnect.StartsWith(prefix, StringComparison.Ordinal))
                    QueueSteamInvite(invite.m_rgchConnect.Substring(prefix.Length));
            });

            var args = Environment.GetCommandLineArgs();
            int argument = Array.IndexOf(args, SteamInviteArgument);
            if (argument >= 0 && argument + 1 < args.Length) QueueSteamInvite(args[argument + 1]);
        }

        private void QueueSteamInvite(string code)
        {
            if (IsBusy || !TryParseRoomCode(code, out _, out _)) return;
            pendingSteamInvite = code;
            SetState(QuantumGameState.Ready, "Steam 초대가 도착했습니다. 준비 화면에서 참가할 수 있습니다.");
        }

        public void InviteSavedMapFriends()
        {
            if (!CanInviteSavedMapFriends) return;
            if (!SteamManager.Initialized || !SteamUtils.IsOverlayEnabled())
            {
                SetState(QuantumGameState.Starting, "Steam 오버레이를 사용할 수 없습니다. 복사된 방 코드를 전달하세요.");
                return;
            }
            SteamFriends.ActivateGameOverlayInviteDialogConnectString(SteamInviteArgument + " " + hostRoomCode);
        }

        public void SelectMap(int index)
        {
            if (State == QuantumGameState.Starting || State == QuantumGameState.Playing || State == QuantumGameState.Stopping) return;
            if (index < 0 || index >= MapCount) return;
            if (Lobby != null && (!Lobby.IsOwner || Lobby.IsLocked || index < supportedMaps.Length)) return;
            ClearSavedSelection();
            SelectedMap = index;
            if (Lobby != null)
            {
                var stage = OfficialStages[index - supportedMaps.Length];
                Lobby.Select("official:" + stage.StageId, stage.DisplayName);
            }
            SetState(QuantumGameState.Ready, "선택: " + SelectedContent.DisplayName);
        }

        public void SelectNextSavedMap()
        {
            if (IsBusy) return;
            if (Lobby != null && (!Lobby.IsOwner || Lobby.IsLocked)) return;
            try
            {
                var palette = SavedMapPalette;
                if (palette == null) throw new InvalidOperationException("맵 에디터 팔레트가 연결되지 않았습니다.");
                var entries = new SavedMapCatalog(palette, isQuantum: true).GetEntries();
                if (entries.Count == 0) throw new InvalidOperationException("저장된 커스텀 맵이 없습니다.");
                int current = entries.ToList().FindIndex(entry => entry.FileName == catalogCursor);
                var selected = entries[(current + 1) % entries.Count];
                catalogCursor = selected.FileName;
                var snapshot = ReadSavedMap(selected, palette, out _);
                HasRejectedSavedMap = false;
                savedFileName = selected.FileName;
                SavedMapName = selected.MapName;
                Lobby?.Select("saved:" + snapshot.ContentHash, SavedMapName, snapshot);
                SetState(QuantumGameState.Ready, "선택: " + SavedMapName);
            }
            catch (InvalidOperationException error)
            {
                Lobby?.Select(null, null);
                HasRejectedSavedMap = true;
                savedFileName = null;
                SavedMapName = null;
                Debug.LogWarning(error.Message, this);
                SetState(QuantumGameState.Failed, error.Message);
            }
        }

        private static MapSessionSnapshot ReadSavedMap(SavedMapListEntry entry, MapEditorPalette palette,
            out QuantumMapConversion conversion)
        {
            if (entry == null) throw new InvalidOperationException("선택한 저장 맵을 찾을 수 없습니다.");
            if (!entry.CanSelect) throw new InvalidOperationException(entry.MapName + "\n" + entry.Message);
            if (!MapSessionSnapshot.TryCreate(0, entry.Json, MapSessionSnapshot.MaximumContentBytes, palette, out var snapshot, out string error))
                throw new InvalidOperationException(error);
            conversion = QuantumSavedMapTransfer.Validate(snapshot.MapId, snapshot.ContentHash, snapshot.Content, palette);
            return snapshot;
        }

        public Task JoinSavedMapAsync(string code)
        {
            if (Lobby != null) return Task.CompletedTask;
            if (IsBusy) return operation ?? Task.CompletedTask;
            if (!TryParseRoomCode(pendingSteamInvite ?? code, out string region, out var roomId))
            {
                SetState(QuantumGameState.Failed, "친구가 보낸 방 코드를 확인해 주세요.");
                return Task.CompletedTask;
            }
            pendingSteamInvite = null;
            ClearSavedSelection();
            SetState(QuantumGameState.Starting, "Steam 인증 및 친구 방 입장 중…");
            return operation = StartCoreAsync(true, joinRoom: SavedRoomPrefix + roomId.ToString("N"), joinRegion: region);
        }

        private static bool TryParseRoomCode(string code, out string region, out Guid roomId)
        {
            var parts = code?.Trim().Split(':');
            roomId = Guid.Empty;
            region = parts?.Length == 2 ? parts[0] : null;
            return region != null && region.Length >= 2 && region.Length <= 8 &&
                region.All(character => character >= 'a' && character <= 'z') &&
                Guid.TryParseExact(parts[1], "N", out roomId) && roomId != Guid.Empty;
        }

        public void ClearSavedSelection()
        {
            savedFileName = null;
            SavedMapName = null;
            catalogCursor = null;
            HasRejectedSavedMap = false;
        }

        public void CycleParticipants()
        {
            if (State == QuantumGameState.Starting || State == QuantumGameState.Playing || State == QuantumGameState.Stopping) return;
            if (KeyboardSlots > 0) KeyboardSlots--;
            else
            {
                PlayerCount = PlayerCount % QuantumGameContent.MaxPlayers + 1;
                KeyboardSlots = Math.Min(2, PlayerCount);
            }
            Changed?.Invoke();
        }

        public Task StartAsync(bool isOnline)
        {
            if (Lobby != null) return Task.CompletedTask;
            if (IsBusy)
                return operation ?? Task.CompletedTask;
            if (HasRejectedSavedMap) return Task.CompletedTask;
            pendingSteamInvite = null;
            SetState(QuantumGameState.Starting, isOnline ? "Steam 인증 및 Photon 연결 중… 취소할 수 있습니다." : "콘텐츠 검증 및 Quantum 시작 중…");
            return operation = StartCoreAsync(isOnline);
        }

        internal Task StartEditorTestAsync(QuantumMapConversion conversion, string returnScene)
        {
            editorReturnScene = returnScene;
            SetState(QuantumGameState.Starting, "Quantum 테스트 플레이 시작 중…");
            return operation = StartCoreAsync(false, conversion);
        }

        private async Task StartCoreAsync(bool isOnline, QuantumMapConversion editorContent = null, string joinRoom = null,
            string joinRegion = null, QuantumLobbyState lobbyStart = null, MapSessionSnapshot lobbySource = null,
            string lobbyLocalId = null, bool isLobbyOwner = false, bool isDevelopment = false, Action<string> regionReady = null)
        {
            try
            {
                bool isEditorTest = editorContent != null;
                var config = new RuntimeConfig { FlipFriendsPlayerCount = lobbyStart?.Members.Length ?? (isEditorTest ? 1 : PlayerCount) };
                var palette = SavedMapPalette;
                bool isSavedMap = lobbyStart != null ? lobbyStart.Map.StartsWith("saved:", StringComparison.Ordinal)
                    : !isEditorTest && (savedFileName != null || joinRoom != null);
                MapSessionSnapshot snapshot = null;
                if (isSavedMap)
                {
                    if (palette == null) throw new InvalidOperationException("맵 에디터 팔레트가 연결되지 않았습니다.");
                    if (lobbyStart != null)
                    {
                        snapshot = isLobbyOwner ? lobbySource : null;
                        if (snapshot != null)
                            Content = QuantumSavedMapTransfer.Validate(snapshot.MapId, snapshot.ContentHash, snapshot.Content, palette);
                    }
                    else if (joinRoom == null)
                    {
                        var entry = new SavedMapCatalog(palette, isQuantum: true).GetEntries()
                            .FirstOrDefault(item => item.FileName == savedFileName);
                        snapshot = ReadSavedMap(entry, palette, out var savedConversion);
                        Content = savedConversion;
                    }
                }
                else
                {
                    IQuantumMapContent map = lobbyStart == null ? SelectedContent : FindLobbyStage(lobbyStart.Map);
                    if (!isEditorTest && map == null) throw new InvalidOperationException("확정된 공식 맵을 찾을 수 없습니다.");
                    Content = editorContent ?? QuantumGameContent.Prepare(map, config);
                }
                int keyboards = isEditorTest || lobbyStart != null ? (Keyboard.current != null ? 1 : 0) : KeyboardSlots;
                bool hasDevices = isEditorTest ? keyboards == 1 || Gamepad.all.Count > 0 : HasLocalDevices;
                if (!isOnline && !hasDevices) throw new InvalidOperationException("배정할 키보드 또는 게임패드를 연결하세요.");
                Session = new QuantumGameSession { KeyboardCount = keyboards, IsInputEnabled = false };
                if (isEditorTest)
                    await Session.StartEditorTestAsync(config, editorContent);
                else if (isSavedMap && !isOnline)
                    await Session.StartSavedMapAsync(config, Content, snapshot.ContentHash);
                else if (isOnline)
                {
                    var settings = new AppSettings(PhotonServerSettings.Global.AppSettings) { AppIdQuantum = releasePhotonAppId };
                    if (lobbyStart != null)
                        await Session.StartLobbyAsync(config, settings, steamAppId, lobbyStart, lobbyLocalId, isLobbyOwner,
                            isDevelopment, palette, snapshot, Content, regionReady, conversion => Content = conversion,
                            () => ReportLobby(isLobbyOwner ? "친구들과 맵을 공유하는 중…" : "맵 받는 중…"));
                    else if (isSavedMap)
                    {
                        if (joinRegion != null) settings.FixedRegion = joinRegion;
                        string code = Guid.NewGuid().ToString("N");
                        await Session.StartSavedMapOnlineAsync(config, settings, steamAppId, joinRoom ?? SavedRoomPrefix + code,
                            palette, snapshot, Content, () =>
                            {
                                if (joinRoom == null)
                                {
                                    hostRoomCode = Session.Client.CurrentRegion.Split('/')[0] + ":" + code;
                                    GUIUtility.systemCopyBuffer = hostRoomCode;
                                }
                                SetState(QuantumGameState.Starting, joinRoom == null
                                    ? "방 코드를 복사했습니다. 친구에게 전달하세요. 참가자 맵 확인 중…"
                                    : "방장의 맵을 확인하는 중…");
                            }, conversion =>
                            {
                                hostRoomCode = null;
                                Content = conversion;
                                SetState(QuantumGameState.Starting, "참가자 콘텐츠 합의 및 Quantum 시작 중…");
                            });
                    }
                    else await Session.StartOnlineAsync(config, settings, steamAppId, "ff-release-" + config.CustomMapAgreement);
                }
                else await Session.StartLocalAsync(config);
                if (isDestroyed || State != QuantumGameState.Starting) return;
                if (!Application.CanStreamedLevelBeLoaded(PlayScene))
                    throw new InvalidOperationException("QuantumPlay 씬을 불러올 수 없습니다.");
                isSceneTransition = true;
                await SceneManager.LoadSceneAsync(PlayScene);
                isSceneTransition = false;
                if (isDestroyed || State != QuantumGameState.Starting) return;
                Session.IsInputEnabled = true;
                SetState(QuantumGameState.Playing, "도착점에서 위를 입력하세요. 메뉴로 종료할 수 있습니다.");
            }
            catch (Exception error)
            {
                if (Session != null) await Session.StopAsync();
                Session = null;
                Content = null;
                if (State != QuantumGameState.Stopping && !isDestroyed)
                {
                    if (editorReturnScene != null)
                    {
                        await RestoreEditorAsync();
                        editorReturnScene = null;
                    }
                    string reason = error is InvalidOperationException ? error.Message : "연결 또는 시작에 실패했습니다. 인증 설정과 연결 상태를 확인하고 재시도하세요.";
                    Debug.LogWarning("[Quantum] 시작 실패: " + error.GetType().Name);
                    SetState(QuantumGameState.Failed, reason);
                }
            }
            finally
            {
                hostRoomCode = null;
                isSceneTransition = false;
            }
        }

        public Task ReturnAsync(string message) => Lobby != null ? Lobby.ReturnAsync(message) : ReturnFromLobbyAsync(message);

        internal Task ReturnFromLobbyAsync(string message) => State == QuantumGameState.Stopping
            ? returning ?? Task.CompletedTask
            : returning = ReturnCoreAsync(message);

        private async Task ReturnCoreAsync(string message)
        {
            hostRoomCode = null;
            pendingSteamInvite = null;
            SetState(QuantumGameState.Stopping, "세션 정리 중…");
            var previous = operation;
            var session = Session;
            if (session != null) await session.StopAsync();
            if (previous != null) await previous;
            Session = null;
            Content = null;
            if (isDestroyed) return;
            if (editorReturnScene != null)
            {
                await RestoreEditorAsync();
                editorReturnScene = null;
            }
            else if (SceneManager.GetActiveScene().name != "Main")
            {
                isSceneTransition = true;
                await SceneManager.LoadSceneAsync("Main");
                isSceneTransition = false;
            }
            ClearSavedSelection();
            SetState(QuantumGameState.Ready, message);
            if (Lobby != null) ShowLobbyScreen();
        }

        private async Task RestoreEditorAsync()
        {
            if (SceneManager.GetActiveScene().path != editorReturnScene)
            {
                isSceneTransition = true;
                await SceneManager.LoadSceneAsync(editorReturnScene);
                isSceneTransition = false;
            }
            MapEditorDraftStore.Clear();
        }

        private async void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!isSceneTransition && !IsBusy && scene.name != "Main") ClearSavedSelection();
            if (isSceneTransition || (State != QuantumGameState.Playing && State != QuantumGameState.Starting) || scene.name == PlayScene) return;
            try { await ReturnAsync("씬 이동으로 Quantum 세션을 종료했습니다."); }
            catch (Exception error) { Debug.LogException(error); }
        }

        private void SetState(QuantumGameState state, string message)
        {
            State = state;
            Message = message;
            Changed?.Invoke();
        }

        private async void OnDestroy()
        {
            if (Instance != this) return;
            isDestroyed = true;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            Instance = null;
            Changed = null;
            Lobby?.Close();
            Lobby = null;
            steamInvite?.Dispose();
            steamInvite = null;
            try { if (Session != null) await Session.StopAsync(); }
            catch (Exception error) { Debug.LogException(error); }
        }
    }
}
