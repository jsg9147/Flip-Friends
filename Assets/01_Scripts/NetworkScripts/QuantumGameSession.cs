using System;
using System.Threading;
using System.Threading.Tasks;
using Photon.Deterministic;
using Photon.Realtime;
using Quantum;
using UnityEngine;

namespace FlipFriends.Network
{
    public sealed class QuantumGameSession
    {
        private readonly CancellationTokenSource lifetime = new();
        private IDisposable inputSubscription;
        private SteamPhotonTicket ticket;
        private Task starting;
        private Task stopping;
        private bool hasStarted;
        public QuantumRunner Runner { get; private set; }
        public RealtimeClient Client { get; private set; }
        public bool HasInputSubscription => inputSubscription != null;
        public static int ActiveInputSubscriptions { get; private set; }
        public Func<int, int, Quantum.Input> InputSource { get; set; }

        public Task StartLocalAsync(RuntimeConfig config) => StartOnce(() => StartRunnerAsync(config, false));

        public Task StartOnlineAsync(RuntimeConfig config, AppSettings settings, uint steamAppId, string room)
            => StartOnce(async () =>
            {
                if (string.IsNullOrWhiteSpace(settings?.AppIdQuantum) || settings.AppIdQuantum == DevelopmentAppId)
                    throw new InvalidOperationException("출시 Photon 앱과 Steam 소유권 인증 공급자가 준비되지 않았습니다. 오프라인 개발 실행을 사용하세요.");
                ticket = new SteamPhotonTicket();
                var auth = await ticket.AcquireAsync(steamAppId, lifetime.Token);
                var versionedSettings = new AppSettings(settings) { AppVersion = QuantumGameContent.NetworkVersion };
                await ConnectAsync(versionedSettings, room, auth, null, false);
                ticket.Dispose();
                ticket = null;
                await StartRunnerAsync(config, true);
            });

        public const string DevelopmentAppId = "d07ddd19-754f-4cc0-853c-b83c7e52027f";

#if UNITY_EDITOR
        // 플레이어 빌드에서는 이 진입점 자체가 없고 출시 버튼도 이 메서드를 호출하지 않는다.
        public Task StartDevelopmentAsync(RuntimeConfig config, string room, bool joinOnly)
            => StartOnce(async () =>
            {
                await ConnectAsync(new AppSettings
                {
                    AppIdQuantum = DevelopmentAppId, AppVersion = QuantumGameContent.NetworkVersion, FixedRegion = "asia", UseNameServer = true
                }, room, null, Guid.NewGuid().ToString("N"), joinOnly);
                await StartRunnerAsync(config, true);
            });
#endif

        private Task StartOnce(Func<Task> operation)
        {
            if (hasStarted || stopping != null) throw new InvalidOperationException("세션을 중복 시작할 수 없습니다.");
            hasStarted = true;
            starting = operation();
            return starting;
        }

        private async Task ConnectAsync(AppSettings settings, string room, AuthenticationValues auth, string userId, bool joinOnly)
        {
            Client = new RealtimeClient();
            var asyncConfig = AsyncConfig.CreateUnityAsyncConfig();
            asyncConfig.CancellationToken = lifetime.Token;
            await MatchmakingExtensions.ConnectToRoomAsync(new MatchmakingArguments
            {
                NetworkClient = Client, PhotonSettings = settings, AuthValues = auth, UserId = userId,
                RoomName = room, PluginName = "QuantumPlugin", MaxPlayers = 2, CanOnlyJoin = joinOnly,
                PlayerTtlInSeconds = 0, AsyncConfig = asyncConfig
            });
        }

        private async Task StartRunnerAsync(RuntimeConfig config, bool isOnline)
        {
            lifetime.Token.ThrowIfCancellationRequested();
            if (isOnline)
            {
                await QuantumMapAgreement.WaitAsync(Client, config.CustomMapAgreement, lifetime.Token);
                if (Client.LocalPlayer.IsMasterClient) Client.CurrentRoom.IsOpen = false;
            }
            var session = JsonUtility.FromJson<DeterministicSessionConfig>(JsonUtility.ToJson(QuantumDeterministicSessionConfigAsset.DefaultConfig));
            session.UpdateFPS = 60;
            session.PlayerCount = 2;
            var args = new SessionRunner.Arguments
            {
                RunnerId = "FF_GAME_" + Guid.NewGuid().ToString("N"),
                RunnerFactory = QuantumRunnerUnityFactory.DefaultFactory,
                GameParameters = QuantumRunnerUnityFactory.CreateGameParameters,
                RuntimeConfig = config, SessionConfig = session, PlayerCount = 2,
                GameMode = isOnline ? DeterministicGameMode.Multiplayer : DeterministicGameMode.Local,
                ClientId = isOnline ? Client.UserId : "ff-local",
                Communicator = isOnline ? new QuantumNetworkCommunicator(Client) : null,
                RecordingFlags = RecordingFlags.Input | RecordingFlags.Checksums,
                CancellationToken = lifetime.Token
            };
            if (isOnline)
            {
                using (new ConnectionServiceScope(Client)) Runner = (QuantumRunner)await SessionRunner.StartAsync(args);
            }
            else Runner = await QuantumRunner.StartGameAsync(args);
            lifetime.Token.ThrowIfCancellationRequested();
            inputSubscription = QuantumCallback.SubscribeManual<CallbackPollInput>(callback =>
            {
                if (callback.Game != Runner?.Game) return;
                callback.SetInput(InputSource?.Invoke(callback.Frame, callback.PlayerSlot) ?? QuantumPlayerInput.Read(callback.PlayerSlot),
                    DeterministicInputFlags.Repeatable);
            });
            ActiveInputSubscriptions++;
            Runner.Game.AddPlayer(0, new RuntimePlayer());
            if (!isOnline) Runner.Game.AddPlayer(1, new RuntimePlayer());
        }

        public Task StopAsync()
        {
            if (stopping != null) return stopping;
            lifetime.Cancel();
            return stopping = StopCoreAsync();
        }

        private async Task StopCoreAsync()
        {
            if (starting != null)
            {
                try { await starting; }
                catch (Exception error)
                {
                    // 인증 예외의 원문에는 티켓이 들어갈 수 있어 형식만 기록한다.
                    Debug.LogWarning("[Quantum] 시작 중단 후 정리: " + error.GetType().Name);
                }
            }
            if (inputSubscription != null)
            {
                inputSubscription.Dispose();
                ActiveInputSubscriptions--;
            }
            inputSubscription = null;
            ticket?.Dispose();
            ticket = null;
            try
            {
                if (Runner != null) await Runner.ShutdownAsync();
            }
            finally
            {
                Runner = null;
                if (Client != null)
                {
                    Client.Disconnect();
                    var deadline = DateTime.UtcNow.AddSeconds(3);
                    while (Client.IsConnected && DateTime.UtcNow < deadline)
                    {
                        Client.Service();
                        await Task.Delay(10);
                    }
                    if (Client.IsConnected) Debug.LogWarning("[Quantum] Photon 종료 응답 제한 시간을 넘겼습니다.");
                    Client = null;
                }
                lifetime.Dispose();
            }
        }
    }
}
