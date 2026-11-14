using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FlipFriends.Maps;
using Photon.Client;
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
        private QuantumPlayerInput playerInput;
        public int KeyboardCount { get; set; } = 2;
        public bool IsInputEnabled { get; set; } = true;
        private SteamPhotonTicket ticket;
        private Task starting;
        private Task stopping;
        private bool hasStarted;
        private Map dynamicMap;
        private string[] lobbyMembers;
        private string lobbyLocalId;
        public QuantumRunner Runner { get; private set; }
        public RealtimeClient Client { get; private set; }
        public int PlayerCount { get; private set; }
        public bool HasInputSubscription => inputSubscription != null;
        public static int ActiveInputSubscriptions { get; private set; }
        public Func<int, int, Quantum.Input> InputSource { get; set; }

        public Task StartLocalAsync(RuntimeConfig config) => StartOnce(() => StartRunnerAsync(config, false));

        internal Task StartEditorTestAsync(RuntimeConfig config, QuantumMapConversion conversion)
            => StartOnce(() => StartDynamicAsync(config, conversion, "editor:" + conversion.ContentHash, false));

        internal Task StartSavedMapAsync(RuntimeConfig config, QuantumMapConversion conversion, string hash)
            => StartOnce(() => StartDynamicAsync(config, conversion, SavedMapAgreement(conversion, hash), false));

        private static string SavedMapAgreement(QuantumMapConversion conversion, string hash) =>
            $"saved:1:{MapData.CurrentVersion}:{QuantumMapConverter.ConversionVersion}:{QuantumMapConverter.PaletteVersion}:{hash}:{conversion.ContentHash}";

        private async Task StartDynamicAsync(RuntimeConfig config, QuantumMapConversion conversion, string agreement, bool isOnline)
        {
            QuantumGameContent.ApplySimulationConfig(config);
            QuantumGameContent.ApplyGoals(config, conversion);
            conversion.ApplyFallBoundary(config);

            // 세션 안에서만 쓰는 맵이므로 전역 DB와 공식 베이크를 수정할 필요가 없다.
            using var assets = new DynamicAssetDB(DotNetRunnerFactory.CreateNativeAllocator());
            dynamicMap = ScriptableObject.CreateInstance<Map>();
            dynamicMap.StaticColliders2D = conversion.Boxes.Select((box, index) => new MapStaticCollider2D
            {
                Position = box.Center,
                BoxExtents = box.Size / 2,
                ShapeType = Shape2DType.Box,
                StaticData = new StaticColliderData { ColliderIndex = index }
            }).ToArray();
            assets.AddAsset(dynamicMap, new AssetGuid(1, AssetGuidType.DynamicExplicit));
            conversion.ApplyTo(config, dynamicMap, agreement);
            if (isOnline) QuantumGameContent.AppendProductAgreement(config, conversion, 1);
            await StartRunnerAsync(config, isOnline, assets);
        }

        internal Task StartSavedMapOnlineAsync(RuntimeConfig config, AppSettings settings, uint steamAppId,
            string room, MapEditorPalette palette, MapSessionSnapshot source, QuantumMapConversion conversion,
            Action connected, Action<QuantumMapConversion> prepared)
            => StartOnce(async () =>
            {
                await ConnectReleaseAsync(config.FlipFriendsPlayerCount, settings, steamAppId, room, source == null, isVisible: false);
                await StartSharedMapAsync(config, palette, source, conversion, connected, prepared);
            });

        private async Task StartSharedMapAsync(RuntimeConfig config, MapEditorPalette palette, MapSessionSnapshot source,
            QuantumMapConversion conversion, Action connected, Action<QuantumMapConversion> prepared, string expectedHash = null)
        {
            if (source == null) config.FlipFriendsPlayerCount = Client.CurrentRoom.MaxPlayers;
            if (config.FlipFriendsPlayerCount < 1 || config.FlipFriendsPlayerCount > QuantumGameContent.MaxPlayers)
                throw new InvalidOperationException("친구 방의 참가 인원이 올바르지 않습니다.");

            bool hasDeparture = false;
            void OnRoomEvent(EventData message)
            {
                if (message.Code != EventCode.Leave) return;
                hasDeparture = true;
                lifetime.Cancel();
            }

            Client.EventReceived += OnRoomEvent;
            try
            {
                var transfer = new QuantumSavedMapTransfer(Client, palette, source);
                var result = await transfer.RunAsync(conversion, config.FlipFriendsPlayerCount, connected, lifetime.Token);
                if (expectedHash != null) QuantumMapAgreement.RequireMatch(expectedHash, result.Hash);
                prepared?.Invoke(result.Conversion);
                await StartDynamicAsync(config, result.Conversion, SavedMapAgreement(result.Conversion, result.Hash), true);
            }
            catch (OperationCanceledException) when (hasDeparture)
            {
                throw new InvalidOperationException("친구 맵 시작 전에 참가자가 방을 나갔습니다.");
            }
            finally
            {
                Client.EventReceived -= OnRoomEvent;
            }
        }

        public Task StartOnlineAsync(RuntimeConfig config, AppSettings settings, uint steamAppId, string room)
            => StartOnce(async () =>
            {
                await ConnectReleaseAsync(config.FlipFriendsPlayerCount, settings, steamAppId, room, false, isVisible: true);
                await StartRunnerAsync(config, true);
            });

        internal static bool IsReleaseApp(string appId) =>
            Guid.TryParse(appId, out var id) && id != Guid.Empty && id != Guid.Parse(DevelopmentAppId);

        private async Task ConnectReleaseAsync(int count, AppSettings settings, uint steamAppId, string room, bool joinOnly, bool isVisible)
        {
            if (!IsReleaseApp(settings?.AppIdQuantum))
                throw new InvalidOperationException("출시 Photon 앱과 Steam 소유권 인증 공급자가 준비되지 않았습니다. 오프라인 개발 실행을 사용하세요.");
            ticket = new SteamPhotonTicket();
            var auth = await ticket.AcquireAsync(steamAppId, lifetime.Token);
            var versionedSettings = new AppSettings(settings) { AppVersion = QuantumGameContent.NetworkVersion };
            await ConnectAsync(count, versionedSettings, room, auth, null, joinOnly, isVisible);
            ticket.Dispose();
            ticket = null;
        }

        internal Task StartLobbyAsync(RuntimeConfig config, AppSettings settings, uint steamAppId, QuantumLobbyState start,
            string localId, bool isOwner, bool isDevelopment, MapEditorPalette palette, MapSessionSnapshot source,
            QuantumMapConversion conversion, Action<string> regionReady, Action<QuantumMapConversion> prepared,
            Action onMapSharing = null)
            => StartOnce(async () =>
            {
                lobbyMembers = start.Members;
                lobbyLocalId = localId;
                bool isSaved = start.Map.StartsWith("saved:", StringComparison.Ordinal);
#if UNITY_EDITOR
                if (isDevelopment)
                    await ConnectAsync(lobbyMembers.Length, DevelopmentSettings(), start.Room, null, localId, !isOwner, false);
                else
#endif
                {
                    if (!isOwner) settings.FixedRegion = start.Region;
                    await ConnectReleaseAsync(lobbyMembers.Length, settings, steamAppId, start.Room, !isOwner, false);
                }
                if (!string.Equals(Client.UserId, localId, StringComparison.Ordinal) || Client.CurrentRoom.MaxPlayers != lobbyMembers.Length)
                    throw new InvalidOperationException("Photon 인증 계정 또는 방 인원이 Steam 로비와 다릅니다.");
                if (Client.CurrentRoom.GetPlayer(Client.CurrentRoom.MasterClientId)?.UserId != start.Owner)
                    throw new InvalidOperationException("Photon 방장이 확정된 Steam 방장과 다릅니다.");
                if (isOwner) regionReady(Client.CurrentRegion.Split('/')[0]);
                if (isSaved)
                {
                    onMapSharing?.Invoke();
                    await StartSharedMapAsync(config, palette, source, conversion, null, prepared, start.Map.Substring(6));
                }
                else
                    await StartRunnerAsync(config, true);
            });

        public const string DevelopmentAppId = "d07ddd19-754f-4cc0-853c-b83c7e52027f";

#if UNITY_EDITOR
        public Task StartSavedMapDevelopmentAsync(RuntimeConfig config, string room, MapEditorPalette palette,
            MapSessionSnapshot source, Action connected = null)
            => StartOnce(async () =>
            {
                var conversion = source == null ? null : QuantumSavedMapTransfer.Validate(source.MapId, source.ContentHash, source.Content, palette);
                await ConnectAsync(config.FlipFriendsPlayerCount, DevelopmentSettings(), room, null, Guid.NewGuid().ToString("N"), source == null, false);
                await StartSharedMapAsync(config, palette, source, conversion, connected, null);
            });

        private static AppSettings DevelopmentSettings() => new AppSettings
        {
            AppIdQuantum = DevelopmentAppId, AppVersion = QuantumGameContent.NetworkVersion, FixedRegion = "asia", UseNameServer = true
        };

        // 플레이어 빌드에서는 이 진입점 자체가 없고 출시 버튼도 이 메서드를 호출하지 않는다.
        public Task StartDevelopmentAsync(RuntimeConfig config, string room, bool joinOnly)
            => StartOnce(async () =>
            {
                await ConnectAsync(config.FlipFriendsPlayerCount, DevelopmentSettings(), room, null, Guid.NewGuid().ToString("N"), joinOnly);
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

        private async Task ConnectAsync(int playerCount, AppSettings settings, string room, AuthenticationValues auth, string userId, bool joinOnly, bool isVisible = true)
        {
            Client = new RealtimeClient();
            var asyncConfig = AsyncConfig.CreateUnityAsyncConfig();
            asyncConfig.CancellationToken = lifetime.Token;
            await MatchmakingExtensions.ConnectToRoomAsync(new MatchmakingArguments
            {
                NetworkClient = Client, PhotonSettings = settings, AuthValues = auth, UserId = userId,
                RoomName = room, PluginName = "QuantumPlugin", MaxPlayers = playerCount, CanOnlyJoin = joinOnly,
                IsRoomVisible = isVisible, PlayerTtlInSeconds = 0, AsyncConfig = asyncConfig,
                ExpectedUsers = lobbyMembers?.Where(id => id != lobbyLocalId).ToArray()
            });
        }

        private async Task StartRunnerAsync(RuntimeConfig config, bool isOnline, DynamicAssetDB assets = null)
        {
            lifetime.Token.ThrowIfCancellationRequested();
            PlayerCount = config.FlipFriendsPlayerCount;
            if (PlayerCount < 1 || PlayerCount > QuantumGameContent.MaxPlayers)
                throw new InvalidOperationException("Quantum 참가 인원 범위를 벗어났습니다.");
            if (isOnline)
            {
                await QuantumMapAgreement.WaitAsync(Client, config.CustomMapAgreement, PlayerCount, lifetime.Token,
                    closeRoom: assets != null || lobbyMembers != null, expectedUsers: lobbyMembers);
                if (assets == null && lobbyMembers == null && Client.LocalPlayer.IsMasterClient) Client.CurrentRoom.IsOpen = false;
            }
            var session = JsonUtility.FromJson<DeterministicSessionConfig>(JsonUtility.ToJson(QuantumDeterministicSessionConfigAsset.DefaultConfig));
            session.UpdateFPS = 60;
            session.PlayerCount = PlayerCount;
            var args = new SessionRunner.Arguments
            {
                RunnerId = "FF_GAME_" + Guid.NewGuid().ToString("N"),
                RunnerFactory = QuantumRunnerUnityFactory.DefaultFactory,
                GameParameters = QuantumRunnerUnityFactory.CreateGameParameters,
                RuntimeConfig = config, SessionConfig = session, PlayerCount = session.PlayerCount,
                GameMode = isOnline ? DeterministicGameMode.Multiplayer : DeterministicGameMode.Local,
                ClientId = isOnline ? Client.UserId : "ff-local",
                Communicator = isOnline ? new QuantumNetworkCommunicator(Client) : null,
                RecordingFlags = RecordingFlags.Input | RecordingFlags.Checksums,
                CancellationToken = lifetime.Token
            };
            args.InitialDynamicAssets = assets;
            if (isOnline)
            {
                using (new ConnectionServiceScope(Client)) Runner = (QuantumRunner)await SessionRunner.StartAsync(args);
            }
            else Runner = await QuantumRunner.StartGameAsync(args);
            lifetime.Token.ThrowIfCancellationRequested();
            int localPlayers = isOnline ? 1 : PlayerCount;
            playerInput = new QuantumPlayerInput(localPlayers, Math.Min(localPlayers, KeyboardCount));
            inputSubscription = QuantumCallback.SubscribeManual<CallbackPollInput>(callback =>
            {
                if (callback.Game != Runner?.Game) return;
                callback.SetInput(InputSource?.Invoke(callback.Frame, callback.PlayerSlot) ?? (IsInputEnabled ? playerInput.ReadSlot(callback.PlayerSlot) : default),
                    DeterministicInputFlags.Repeatable);
            });
            ActiveInputSubscriptions++;
            for (int slot = 0; slot < localPlayers; slot++) Runner.Game.AddPlayer(slot, new RuntimePlayer());
        }

        public Task StopAsync()
        {
            if (stopping != null) return stopping;
            // 취소가 시작 작업을 즉시 깨워도 같은 정리 작업을 기다리도록 먼저 게시한다.
            var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            stopping = completion.Task;
            _ = StopAndSignalAsync(completion);
            return stopping;
        }

        private async Task StopAndSignalAsync(TaskCompletionSource<bool> completion)
        {
            try
            {
                lifetime.Cancel();
                await StopCoreAsync();
                completion.SetResult(true);
            }
            catch (Exception error)
            {
                completion.SetException(error);
            }
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
            playerInput?.Dispose();
            playerInput = null;
            ticket?.Dispose();
            ticket = null;
            try
            {
                if (Runner != null) await Runner.ShutdownAsync();
            }
            finally
            {
                Runner = null;
                if (dynamicMap != null) UnityEngine.Object.Destroy(dynamicMap);
                dynamicMap = null;
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
