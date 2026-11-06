using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Photon.Deterministic;
using Photon.Realtime;
using Quantum;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FlipFriends.Network
{
    public sealed class QuantumTestBootstrap : MonoBehaviour
    {
        [SerializeField] private RuntimeConfig runtimeConfig = new RuntimeConfig();
        [SerializeField] private bool isOnline;
        [SerializeField] private uint steamAppId;
        [SerializeField] private string roomName = "flip-friends-quantum-test";
        [SerializeField] private bool useScriptedInput;
        [SerializeField] private FlipFriends.Maps.QuantumCustomMapAsset customMap;
        private readonly Dictionary<EntityRef, GameObject> views = new Dictionary<EntityRef, GameObject>();
        private readonly CancellationTokenSource lifetime = new CancellationTokenSource();
        private QuantumRunner runner;
        private RealtimeClient client;
        private SteamPhotonTicket ticket;
        private int lastVerifiedFrame;
        public int VerifiedFrame => runner?.Game.Frames.Verified?.Number ?? 0;
        public ulong VerifiedChecksum => runner?.Game.Frames.Verified?.CalculateChecksum() ?? 0;
        public RuntimeConfig Config => runtimeConfig;

        public void Configure(RuntimeConfig config) => runtimeConfig = config;
        public void ConfigureCustomMap(FlipFriends.Maps.QuantumCustomMapAsset map)
        {
            customMap = map;
            ValidateCustomMap();
        }

        public void ValidateCustomMap()
        {
            if (customMap != null) customMap.ValidateAndApply(runtimeConfig);
            else if (!string.IsNullOrEmpty(runtimeConfig.CustomMapAgreement))
                throw new InvalidOperationException("커스텀 맵 검증 에셋이 없습니다.");
        }

        private void OnEnable()
        {
            QuantumCallback.Subscribe(this, (CallbackPollInput callback) => PollInput(callback));
        }

        private async void Start()
        {
            try { ValidateCustomMap(); }
            catch (InvalidOperationException error)
            {
                Debug.LogError("[QuantumMap] 시작 중단: " + error.Message);
                return;
            }
            try
            {
                Application.runInBackground = true;
                var session = JsonUtility.FromJson<DeterministicSessionConfig>(
                    JsonUtility.ToJson(QuantumDeterministicSessionConfigAsset.DefaultConfig));
                session.UpdateFPS = 60;
                session.PlayerCount = 2;
                if (isOnline)
                {
                    var settings = new AppSettings(PhotonServerSettings.Global.AppSettings);
                    if (string.IsNullOrWhiteSpace(settings.AppIdQuantum))
                        throw new InvalidOperationException("Quantum 앱 설정이 필요합니다.");
                    ticket = new SteamPhotonTicket();
                    var auth = await ticket.AcquireAsync(steamAppId, lifetime.Token);
                    client = new RealtimeClient();
                    client = await MatchmakingExtensions.ConnectToRoomAsync(new MatchmakingArguments
                    {
                        PhotonSettings = settings, AuthValues = auth, NetworkClient = client,
                        PluginName = "QuantumPlugin", MaxPlayers = 2, RoomName = roomName,
                        PlayerTtlInSeconds = 0
                    });
                    ticket.Dispose();
                    using (new ConnectionServiceScope(client))
                    using (var waiting = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token))
                    {
                        waiting.CancelAfter(30000);
                        while (client.CurrentRoom.PlayerCount < 2)
                            await Task.Delay(100, waiting.Token);
                    }
                    if (client.LocalPlayer.IsMasterClient) client.CurrentRoom.IsOpen = false;
                    if (customMap != null)
                        await QuantumMapAgreement.WaitAsync(client, runtimeConfig.CustomMapAgreement, lifetime.Token);
                }
                lifetime.Token.ThrowIfCancellationRequested();
                var args = new SessionRunner.Arguments
                {
                    RunnerFactory = QuantumRunnerUnityFactory.DefaultFactory,
                    GameParameters = QuantumRunnerUnityFactory.CreateGameParameters,
                    RuntimeConfig = runtimeConfig,
                    SessionConfig = session,
                    GameMode = isOnline ? DeterministicGameMode.Multiplayer : DeterministicGameMode.Local,
                    PlayerCount = 2,
                    ClientId = isOnline ? client.UserId : "flip-friends-offline",
                    Communicator = isOnline ? new QuantumNetworkCommunicator(client) : null,
                    RecordingFlags = RecordingFlags.Input | RecordingFlags.Checksums
                };
                if (isOnline)
                {
                    using (new ConnectionServiceScope(client)) runner = (QuantumRunner)await SessionRunner.StartAsync(args);
                }
                else runner = await QuantumRunner.StartGameAsync(args);
                if (lifetime.IsCancellationRequested) { await runner.ShutdownAsync(); return; }
                runner.Game.AddPlayer(0, new RuntimePlayer());
                if (!isOnline) runner.Game.AddPlayer(1, new RuntimePlayer());
            }
            catch (Exception)
            {
                // 인증 라이브러리 예외에는 티켓 매개변수가 포함될 수 있어 원문을 로그에 남기지 않는다.
                Debug.LogError("[QuantumTest] 시작 실패: 앱 설정·Steam 로그인·인증 공급자와 연결 상태를 확인하세요. 인증 없는 재접속은 수행하지 않습니다.");
                ticket?.Dispose();
                client?.Disconnect();
            }
        }

        private void PollInput(CallbackPollInput callback)
        {
            if (callback.Game != runner?.Game) return;
            var input = new Quantum.Input();
            if (useScriptedInput)
            {
                input.Horizontal = (callback.Frame / 120 + callback.PlayerSlot) % 2 == 0 ? FP._1 : -FP._1;
                input.Jump = callback.Frame % 90 < 10;
            }
            else if (Keyboard.current != null)
            {
                var keyboard = Keyboard.current;
                bool isSecond = callback.PlayerSlot == 1;
                bool left = isSecond ? keyboard.leftArrowKey.isPressed : keyboard.aKey.isPressed;
                bool right = isSecond ? keyboard.rightArrowKey.isPressed : keyboard.dKey.isPressed;
                input.Horizontal = (right ? 1 : 0) - (left ? 1 : 0);
                bool up = isSecond ? keyboard.upArrowKey.isPressed : keyboard.wKey.isPressed;
                bool down = isSecond ? keyboard.downArrowKey.isPressed : keyboard.sKey.isPressed;
                input.Vertical = (up ? 1 : 0) - (down ? 1 : 0);
                input.Jump = isSecond ? keyboard.rightCtrlKey.isPressed : keyboard.spaceKey.isPressed;
            }
            callback.SetInput(input, DeterministicInputFlags.Repeatable);
        }

        private void Update()
        {
            if (runner?.Game.Frames.Predicted == null) return;
            var frame = runner.Game.Frames.Predicted;
            var players = frame.Filter<Transform2D, FlipFriendsPlayer>();
            while (players.Next(out var entity, out var transform, out var player))
            {
                if (!views.TryGetValue(entity, out var view))
                {
                    view = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    view.name = $"Quantum 플레이어 {player.Player}";
                    Destroy(view.GetComponent<Collider>());
                    view.transform.localScale = new Vector3(0.8f, 1, 0.3f);
                    view.GetComponent<Renderer>().material.color = (int)player.Player == 0 ? Color.cyan : Color.yellow;
                    views.Add(entity, view);
                }
                view.transform.position = new Vector3(transform.Position.X.AsFloat, transform.Position.Y.AsFloat, 0);
            }
            var verified = runner.Game.Frames.Verified;
            if (verified != null && verified.Number / 120 > lastVerifiedFrame / 120)
            {
                lastVerifiedFrame = verified.Number;
                Debug.Log($"[QuantumTest] 확정 프레임={verified.Number}, 체크섬={verified.CalculateChecksum()}");
            }
        }

        private void OnDestroy()
        {
            lifetime.Cancel();
            ticket?.Dispose();
            runner?.Shutdown();
            client?.Disconnect();
            foreach (var view in views.Values) if (view != null) Destroy(view);
            lifetime.Dispose();
        }
    }
}
