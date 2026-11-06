using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FlipFriends.Network;
using Photon.Deterministic;
using Photon.Realtime;
using Quantum;
using UnityEditor;
using UnityEngine;

public static class QuantumCloudCheck
{
    // 출시 인증 경로와 분리된 에디터 전용 검사이며 이 앱에만 익명 시험 연결을 허용한다.
    public const string DevelopmentAppId = "d07ddd19-754f-4cc0-853c-b83c7e52027f";
    public static string Status { get; private set; } = "미실행";
    private static bool isRunning;

    [MenuItem("Tools/Flip Friends/Quantum/Check Development Cloud Two Clients")]
    public static async void Run()
    {
        if (isRunning) return;
        var bootstrap = UnityEngine.Object.FindAnyObjectByType<QuantumTestBootstrap>();
        if (!EditorApplication.isPlaying || bootstrap == null)
            throw new InvalidOperationException("Quantum 이동 테스트 씬을 Play한 뒤 실행하세요.");
        isRunning = true;
        Status = "연결 중";
        var clients = new[] { new RealtimeClient(), new RealtimeClient() };
        var runners = new QuantumRunner[2];
        Task<SessionRunner>[] startTasks = null;
        var checksums = new[] { new Dictionary<int, ulong>(), new Dictionary<int, ulong>() };
        bool isRopeMap = bootstrap.Config.CustomMapRopeCenters?.Length > 0;
        IQuantumMovementEvidence NewEvidence() => isRopeMap ? new QuantumRopeChecks.RopeEvidence() : new QuantumCustomMapChecks.MovementEvidence();
        var evidence = new[] { NewEvidence(), NewEvidence() };
        bool isCustomMap = !string.IsNullOrEmpty(bootstrap.Config.CustomMapAgreement);
        int target = isRopeMap ? QuantumRopeChecks.TargetFrame : isCustomMap ? QuantumCustomMapChecks.TargetFrame : 600;
        bool wasEnabled = bootstrap.enabled;
        bootstrap.enabled = false;
        try
        {
            bootstrap.ValidateCustomMap();
            string room = "ff-check-" + Guid.NewGuid().ToString("N");
            var settings = new AppSettings { AppIdQuantum = DevelopmentAppId, AppVersion = "ff-quantum-step1", FixedRegion = "asia", UseNameServer = true };
            for (int i = 0; i < 2; i++)
                clients[i] = await MatchmakingExtensions.ConnectToRoomAsync(new MatchmakingArguments
                {
                    NetworkClient = clients[i], PhotonSettings = settings,
                    UserId = Guid.NewGuid().ToString("N"), RoomName = room,
                    PluginName = "QuantumPlugin", MaxPlayers = 2, CanOnlyJoin = i == 1
                });
            clients[0].CurrentRoom.IsOpen = false;
            if (isCustomMap)
                await Task.WhenAll(QuantumMapAgreement.WaitAsync(clients[0], bootstrap.Config.CustomMapAgreement, System.Threading.CancellationToken.None),
                    QuantumMapAgreement.WaitAsync(clients[1], bootstrap.Config.CustomMapAgreement, System.Threading.CancellationToken.None));
            var session = JsonUtility.FromJson<DeterministicSessionConfig>(JsonUtility.ToJson(QuantumDeterministicSessionConfigAsset.DefaultConfig));
            session.UpdateFPS = 60;
            session.PlayerCount = 2;
            using (QuantumCallback.SubscribeManual<CallbackPollInput>(callback =>
            {
                for (int i = 0; i < 2; i++)
                    if (callback.Game == runners[i]?.Game)
                    {
                        // 연결 순서와 전역 PlayerRef 순서는 같다는 보장이 없다.
                        var localPlayers = callback.Game.GetLocalPlayers();
                        if (localPlayers.Count != 1)
                        {
                            callback.SetInput(default, DeterministicInputFlags.Repeatable);
                            continue;
                        }
                        int player = (int)localPlayers[0];
                        callback.SetInput(isCustomMap ? (isRopeMap ? QuantumRopeChecks.InputFor(callback.Frame, player) : QuantumCustomMapChecks.InputFor(callback.Frame, player)) : new Quantum.Input
                        {
                            Horizontal = (callback.Frame / 120 + player) % 2 == 0 ? FP._1 : -FP._1,
                            Jump = callback.Frame % 90 < 10
                        }, DeterministicInputFlags.Repeatable);
                    }
            }))
            using (QuantumCallback.SubscribeManual<CallbackSimulateFinished>(callback =>
            {
                if (!callback.Frame.IsVerified) return;
                for (int i = 0; i < 2; i++)
                    if (callback.Game == runners[i]?.Game)
                    {
                        if (isCustomMap && callback.Frame.Number <= target) evidence[i].Observe(callback.Frame);
                        if (callback.Frame.Number % 120 == 0) checksums[i][callback.Frame.Number] = callback.Frame.CalculateChecksum();
                    }
            }))
            {
                using (new ConnectionServiceScope(clients[0]))
                using (new ConnectionServiceScope(clients[1]))
                {
                    startTasks = new Task<SessionRunner>[2];
                    for (int i = 0; i < 2; i++)
                        startTasks[i] = SessionRunner.StartAsync(new SessionRunner.Arguments
                        {
                            RunnerId = "FF_CLOUD_CHECK_" + i,
                            RunnerFactory = QuantumRunnerUnityFactory.DefaultFactory,
                            GameParameters = QuantumRunnerUnityFactory.CreateGameParameters,
                            RuntimeConfig = bootstrap.Config, SessionConfig = session,
                            GameMode = DeterministicGameMode.Multiplayer, PlayerCount = 2,
                            ClientId = clients[i].UserId, Communicator = new QuantumNetworkCommunicator(clients[i])
                        });
                    var started = await Task.WhenAll(startTasks);
                    for (int i = 0; i < 2; i++)
                    {
                        runners[i] = (QuantumRunner)started[i];
                        runners[i].Game.AddPlayer(0, new RuntimePlayer());
                    }
                }
                Status = "확정 프레임 대조 중";
                var deadline = DateTime.UtcNow.AddSeconds(30);
                while ((!checksums[0].ContainsKey(target) || !checksums[1].ContainsKey(target)) && DateTime.UtcNow < deadline && EditorApplication.isPlaying)
                    await Task.Delay(100);
                if (!checksums[0].ContainsKey(target) || !checksums[1].ContainsKey(target))
                    throw new InvalidOperationException("확정 프레임 대조 제한 시간 초과");
                int matched = 0;
                foreach (var pair in checksums[0])
                    if (pair.Key >= 240 && pair.Key <= target && checksums[1].TryGetValue(pair.Key, out var other))
                    {
                        if (pair.Value != other) throw new InvalidOperationException("확정 프레임 체크섬 불일치");
                        matched++;
                    }
                if (matched != (target - 240) / 120 + 1) throw new InvalidOperationException("체크섬 표본 부족");
                int playerCount = CountPlayers(runners[0].Game.Frames.Verified);
                if (playerCount != 2) throw new InvalidOperationException("두 플레이어 생성 실패");
                if (isCustomMap) foreach (var item in evidence) item.AssertComplete();
                Status = $"통과: Photon {clients[0].CloudRegion} 2연결, 플레이어 {playerCount}명, 확정 240~{target} 120간격 체크섬 일치. RTT={clients[0].LoadBalancingPeer.RoundTripTime}/{clients[1].LoadBalancingPeer.RoundTripTime}ms, 지연·손실 주입 없음, 같은 PC, Unity={Application.unityVersion}, 맵={bootstrap.Config.CustomMapAgreement}\n" +
                    string.Join("\n", checksums[0].Where(pair => pair.Key >= 240 && pair.Key <= target).Select(pair => $"{pair.Key}: {pair.Value}")) +
                    (isCustomMap ? "\n" + evidence[0].Summary + "\n" + evidence[1].Summary : "");
                System.IO.Directory.CreateDirectory("Logs");
                System.IO.File.WriteAllText(isRopeMap ? "Logs/QuantumRopeCloudCheck.txt" : isCustomMap ? "Logs/QuantumCustomMapCloudCheck.txt" : "Logs/QuantumCloudCheck.txt", Status);
                Debug.Log("[QuantumTest] " + Status);
            }
        }
        catch (Exception exception)
        {
            Status = "실패: " + exception.GetType().Name;
            System.IO.Directory.CreateDirectory("Logs");
            System.IO.File.WriteAllText(isRopeMap ? "Logs/QuantumRopeCloudFailure.txt" : "Logs/QuantumCloudFailure.txt",
                Status + "\n" + string.Join("\n", evidence.Select(item => item.Summary)));
            Debug.LogException(exception);
        }
        finally
        {
            if (startTasks != null)
                for (int i = 0; i < startTasks.Length; i++)
                    if (runners[i] == null && startTasks[i]?.Status == TaskStatus.RanToCompletion)
                        runners[i] = (QuantumRunner)startTasks[i].Result;
            foreach (var runner in runners) if (runner != null) await runner.ShutdownAsync();
            foreach (var client in clients) client.Disconnect();
            if (bootstrap != null) bootstrap.enabled = wasEnabled;
            isRunning = false;
        }
    }

    private static int CountPlayers(Frame frame)
    {
        var players = frame.Filter<FlipFriendsPlayer>();
        int count = 0;
        while (players.Next(out var entity, out var player)) count++;
        return count;
    }
}
