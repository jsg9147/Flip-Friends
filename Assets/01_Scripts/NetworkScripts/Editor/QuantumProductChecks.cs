using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FlipFriends.Maps;
using FlipFriends.Network;
using Photon.Deterministic;
using Quantum;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Button = UnityEngine.UI.Button;
using LogType = UnityEngine.LogType;

public static class QuantumProductChecks
{
    public static string Status { get; private set; } = "미실행";
    private static bool isRunning;

    [MenuItem("Tools/Flip Friends/Quantum/Check Product Offline And Main")]
    public static async void RunOffline()
    {
        if (isRunning) return;
        isRunning = true;
        Status = "오프라인 검사 중";
        int errorCount = 0;
        Application.LogCallback onLog = (_, _, type) => { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errorCount++; };
        Application.logMessageReceived += onLog;
        try
        {
            Require(EditorApplication.isPlaying && SceneManager.GetActiveScene().name == "Main", "Main에서 Play 필요");
            await QuantumMovementChecks.Run();
            await QuantumInteractionChecks.Run();
            await QuantumRespawnChecks.Run();
            await QuantumDeviceChecks.Run();
            await CheckReplay(false);
            await CheckReplay(true);
            await CheckMain();
            await CheckInteractionMain();
            await CheckRespawnMain();
            await CheckDeviceMain();
            Require(errorCount == 0, "제품 오프라인 실행 중 Unity 오류/예외 없음");
            Status = "통과: 장치 상태/경계·새 재생20개·Main 체크포인트/구간운반대기/소환실패·성공/전원도착/재진입, 복귀 경계/관계·재생20개·Main 복귀 맵 리셋/운반/상자/추락·전원 도착/재진입, 상호작용 전이·재생12개·Main 새 맵 플레이/도착, 이동 재생8개·정적/로프 재생6/8개, 실패/취소/중복/씬 왕복·잔존·Unity 오류0";
        }
        catch (Exception error) { Status = "실패: " + error; Debug.LogException(error); }
        finally
        {
            Application.logMessageReceived -= onLog;
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/QuantumProductOffline.txt", Status);
            isRunning = false;
        }
        Debug.Log("[Quantum 제품] " + Status);
    }

    private static async Task CheckReplay(bool rope)
    {
        var session = new QuantumGameSession { InputSource = rope ? QuantumRopeChecks.InputFor : QuantumCustomMapChecks.InputFor };
        QuantumRunner replay = null;
        int target = rope ? 960 : 720;
        var expected = new Dictionary<int, ulong>();
        var actual = new Dictionary<int, ulong>();
        IQuantumMovementEvidence evidence = rope ? new QuantumRopeChecks.RopeEvidence() : new QuantumCustomMapChecks.MovementEvidence();
        var config = Config(rope);
        try
        {
            using (QuantumCallback.SubscribeManual<CallbackSimulateFinished>(callback =>
            {
                if (!callback.Frame.IsVerified || callback.Frame.Number > target) return;
                if (callback.Game == session.Runner?.Game)
                {
                    evidence.Observe(callback.Frame);
                    if (callback.Frame.Number % 120 == 0) expected[callback.Frame.Number] = callback.Frame.CalculateChecksum();
                }
                if (callback.Game == replay?.Game && callback.Frame.Number % 120 == 0)
                    actual[callback.Frame.Number] = callback.Frame.CalculateChecksum();
            }))
            {
                await session.StartLocalAsync(config);
                var original = session.Runner;
                original.IsSessionUpdateDisabled = true;
                for (int i = 0; i < target + 240 && !expected.ContainsKey(target); i++) original.Service(1f / 60);
                evidence.AssertComplete();
                Require(expected.Count == target / 120, "제품 기록 체크섬 표본");
                var record = original.Game.GetRecordedReplay(includeChecksums: true);
                var serializer = new QuantumUnityJsonSerializer();
                replay = QuantumRunner.StartGame(new SessionRunner.Arguments
                {
                    RunnerId = "FF_PRODUCT_REPLAY", RuntimeConfig = serializer.ConfigFromByteArray<RuntimeConfig>(record.RuntimeConfigData.Decode(), compressed: true),
                    SessionConfig = record.DeterministicConfig, ReplayProvider = record.CreateInputProvider(),
                    InitialTick = record.InitialTick, FrameData = record.InitialFrameData,
                    GameMode = DeterministicGameMode.Replay, PlayerCount = 2
                });
                replay.IsSessionUpdateDisabled = true;
                for (int i = 0; i < target + 240 && !actual.ContainsKey(target); i++) replay.Service(1f / 60);
                Require(expected.Count == actual.Count && expected.All(pair => actual.TryGetValue(pair.Key, out var value) && pair.Value == value), "제품 확정 프레임 재생 체크섬");
                string prefix = "Logs/QuantumProduct" + (rope ? "Rope" : "Static");
                File.WriteAllText(prefix + "Replay.json", JsonUtility.ToJson(record));
                File.WriteAllText(prefix + "Checks.txt", "제품 코드 새 실행 통과: " + config.CustomMapAgreement + "\n" + string.Join("\n", expected.Select(pair => pair.Key + ": " + pair.Value)) + "\n" + evidence.Summary);
            }
        }
        finally
        {
            if (replay != null) await replay.ShutdownAsync();
            await session.StopAsync();
            Require(session.Runner == null && !session.HasInputSubscription, "재생 뒤 러너·입력 콜백 정리");
        }
    }

    private static async Task CheckMain()
    {
        var flow = QuantumGameFlow.Instance;
        Click("Start Button");
        Require(UnityEngine.Object.FindAnyObjectByType<ScreenNavigator>().CurrentScreen.ScreenId == "quantum-prepare", "Start 실제 Quantum 연결");
        var valid = flow.SupportedMaps[0];
        var invalid = UnityEngine.Object.Instantiate(valid);
        var data = JsonUtility.FromJson<MapData>(AssetDatabase.LoadAssetAtPath<TextAsset>(QuantumCustomMapSetup.SourcePath).text);
        data.objects[3].prefabID = "obstacle.cannon";
        var badSource = new TextAsset(JsonUtility.ToJson(data));
        try
        {
            var serialized = new SerializedObject(invalid);
            serialized.FindProperty("source").objectReferenceValue = badSource;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            flow.SupportedMaps[0] = invalid;
            Click("오프라인 시작");
            await Wait(() => flow.State == QuantumGameState.Failed, "미지원 콘텐츠 시작 전 거부");
            Require(flow.Message.Contains("위치=") && flow.Session == null, "미지원 위치·원인 UI 표시 및 부분 시작 없음");
        }
        finally
        {
            flow.SupportedMaps[0] = valid;
            UnityEngine.Object.Destroy(invalid);
            UnityEngine.Object.Destroy(badSource);
        }
        Click("온라인 시작");
        await Wait(() => flow.State == QuantumGameState.Failed, "출시 설정 실패 표시");
        Require(flow.Session == null && flow.Message.Contains("출시 Photon"), "출시 익명 폴백 없음");
        Click("로프 맵 선택");
        Click("오프라인 시작");
        var duplicate = flow.StartAsync(false);
        await Wait(() => flow.State == QuantumGameState.Playing, "오프라인 재시도");
        Require(flow.SelectedMap == 1 && UnityEngine.Object.FindObjectsByType<QuantumGameView>().Length == 1, "로프 선택·표시 단일 생성");
        var first = flow.Session;
        var originalRunner = first.Runner;
        await flow.StartAsync(false);
        Require(first == flow.Session && originalRunner == flow.Session.Runner, "중복 시작 거부");
        first.InputSource = (frame, slot) => FinishInput(first, slot, false);
        await Wait(() => SceneManager.GetActiveScene().name == "Main" && flow.State == QuantumGameState.Ready, "위 입력·전원 도착·정상 종료", 15000);
        Require(flow.Message.Contains("전원 도착") && !first.HasInputSubscription && first.Runner == null, "정상 종료 콜백 해제");
        await AssertNoResidue();
        Click("Start Button");
        Click("정적 맵 선택");
        Click("오프라인 시작");
        await Wait(() => flow.State == QuantumGameState.Playing, "종료 후 재진입");
        Click("플레이 종료");
        await Wait(() => flow.State == QuantumGameState.Ready && SceneManager.GetActiveScene().name == "Main", "메뉴 종료");
        await AssertNoResidue();
        Click("Start Button");
        Click("오프라인 시작");
        await flow.ReturnAsync("검사 취소");
        Require(flow.State == QuantumGameState.Ready, "시작 중 취소");
        await AssertNoResidue();
        await flow.StartAsync(false);
        await Wait(() => flow.State == QuantumGameState.Playing, "취소 후 재시도");
        await SceneManager.LoadSceneAsync("Main");
        await Wait(() => flow.State == QuantumGameState.Ready, "외부 씬 왕복 정리");
        await AssertNoResidue();
    }

    private static async Task CheckInteractionMain()
    {
        var flow = QuantumGameFlow.Instance;
        Click("Start Button");
        Click("상호작용 맵 선택");
        Click("오프라인 시작");
        await Wait(() => flow.State == QuantumGameState.Playing, "상호작용 맵 Main 진입");
        Require(flow.SelectedMap == 2 && flow.Content.MovableBoxes.Count == 1, "상자 맵 선택");
        var session = flow.Session;
        var evidence = new QuantumInteractionChecks.Evidence();
        session.InputSource = (tick, slot) => QuantumInteractionChecks.InputFor(session.Runner?.Game.Frames.Verified, tick, slot);
        using (QuantumCallback.SubscribeManual<CallbackSimulateFinished>(callback =>
        {
            if (callback.Game == session.Runner?.Game && callback.Frame.IsVerified) evidence.Observe(callback.Frame);
        }))
        {
            await Wait(() => session.Runner.Game.Frames.Verified.Number >= 1440, "Main 실제 상호작용 입력", 30000);
            evidence.AssertComplete();
            session.InputSource = (tick, slot) => FinishInput(session, slot, false);
            await Wait(() => flow.State == QuantumGameState.Ready && SceneManager.GetActiveScene().name == "Main", "상자 맵 전원 도착", 20000);
        }
        await AssertNoResidue();
        Click("Start Button"); Click("상호작용 맵 선택"); Click("오프라인 시작");
        await Wait(() => flow.State == QuantumGameState.Playing, "상호작용 맵 재진입");
        Click("플레이 종료");
        await Wait(() => flow.State == QuantumGameState.Ready, "상호작용 재진입 종료");
        await AssertNoResidue();
    }

    private static async Task CheckRespawnMain()
    {
        var flow = QuantumGameFlow.Instance;
        Click("Start Button"); Click("복귀 맵 선택"); Click("오프라인 시작");
        await Wait(() => flow.State == QuantumGameState.Playing, "복귀 맵 Main 진입");
        Require(flow.SelectedMap == 3 && flow.Content.Version == 4, "새 복귀 콘텐츠 선택");
        var session = flow.Session;
        var evidence = new QuantumRespawnChecks.Evidence();
        session.InputSource = (tick, slot) => QuantumRespawnChecks.InputFor(session.Runner?.Game.Frames.Verified, tick, slot);
        using (QuantumCallback.SubscribeManual<CallbackSimulateFinished>(callback =>
        {
            if (callback.Game == session.Runner?.Game && callback.Frame.IsVerified) evidence.Observe(callback.Frame);
        }))
        {
            await Wait(() => session.Runner.Game.Frames.Verified.Number >= 2400, "Main 리셋/운반/상자/추락 실제 입력", 55000);
            File.WriteAllText("Logs/QuantumRespawnMainTrace.txt", string.Join("\n", evidence.Trace));
            evidence.AssertComplete();
            session.InputSource = (tick, slot) => QuantumInteractionChecks.FinishInput(session, slot);
            await Wait(() => flow.State == QuantumGameState.Ready && SceneManager.GetActiveScene().name == "Main", "복귀 맵 전원 도착", 25000);
        }
        await AssertNoResidue();
        Click("Start Button"); Click("복귀 맵 선택"); Click("오프라인 시작");
        await Wait(() => flow.State == QuantumGameState.Playing, "복귀 맵 재진입");
        Click("플레이 종료"); await Wait(() => flow.State == QuantumGameState.Ready, "복귀 맵 메뉴 종료");
        await AssertNoResidue();
    }

    private static async Task CheckDeviceMain()
    {
        var flow = QuantumGameFlow.Instance;
        Click("Start Button"); Click("장치 맵 선택"); Click("오프라인 시작");
        await Wait(() => flow.State == QuantumGameState.Playing, "장치 맵 Main 진입");
        Require(flow.SelectedMap == 4 && flow.Content.Version == 5, "새 장치 콘텐츠 선택");
        var session = flow.Session;
        var evidence = new QuantumDeviceChecks.Evidence();
        session.InputSource = (tick, slot) => QuantumDeviceChecks.InputFor(session.Runner?.Game.Frames.Verified, tick, slot);
        using (QuantumCallback.SubscribeManual<CallbackSimulateFinished>(callback =>
        {
            if (callback.Game == session.Runner?.Game && callback.Frame.IsVerified) evidence.Observe(callback.Frame);
        }))
        {
            await Wait(() => session.Runner.Game.Frames.Verified.Number >= 2400, "Main 체크포인트/구간/운반대기/막힌소환/성공 실제 입력", 55000);
            File.WriteAllText("Logs/QuantumDeviceMainTrace.txt", string.Join("\n", evidence.Trace));
            evidence.AssertComplete();
            session.InputSource = (tick, slot) => QuantumDeviceChecks.FinishInput(session, slot);
            await Wait(() => flow.State == QuantumGameState.Ready && SceneManager.GetActiveScene().name == "Main", "장치 맵 전원 도착", 25000);
        }
        await AssertNoResidue();
        Click("Start Button"); Click("장치 맵 선택"); Click("오프라인 시작");
        await Wait(() => flow.State == QuantumGameState.Playing, "장치 맵 재진입");
        Click("플레이 종료"); await Wait(() => flow.State == QuantumGameState.Ready, "장치 맵 메뉴 종료");
        await AssertNoResidue();
    }

    private static async Task AssertNoResidue()
    {
        await Task.Delay(150);
        Require(!QuantumRunner.ActiveRunners.Any(), "잔존 Quantum 러너 없음");
        Require(QuantumGameSession.ActiveInputSubscriptions == 0, "잔존 제품 입력 콜백 없음");
        Require(!UnityEngine.Object.FindObjectsByType<QuantumGameView>().Any(), "잔존 표시 객체 없음");
        Require(QuantumGameFlow.Instance.Session == null, "잔존 세션 없음");
        Require(!Mirror.NetworkClient.active && !Mirror.NetworkServer.active, "Mirror 비활성 유지");
        var menuEvent = typeof(InputManager).GetField("OnMenuEvent", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.GetValue(InputManager.instance) as Action;
        Require(menuEvent == null || menuEvent.GetInvocationList().All(callback => callback.Target is not QuantumGameView), "잔존 메뉴 콜백 없음");
    }

    [MenuItem("Tools/Flip Friends/Quantum/Check Product Development Two Clients")]
    public static async void RunCloud()
    {
        if (isRunning) return;
        isRunning = true;
        Status = "제품 Photon 연결 중";
        int errorCount = 0;
        Application.LogCallback onLog = (_, _, type) => { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errorCount++; };
        Application.logMessageReceived += onLog;
        var sessions = new[] { new QuantumGameSession(), new QuantumGameSession() };
        var clients = new Photon.Realtime.RealtimeClient[2];
        var checks = new[] { new Dictionary<int, ulong>(), new Dictionary<int, ulong>() };
        var evidence = new[] { new QuantumRopeChecks.RopeEvidence(), new QuantumRopeChecks.RopeEvidence() };
        var movementEvidence = new[] { new QuantumMovementChecks.Evidence(), new QuantumMovementChecks.Evidence() };
        Task[] starts = null;
        bool isFinishing = false;
        try
        {
            Require(EditorApplication.isPlaying && QuantumGameFlow.Instance.Session == null, "Main 유휴 상태 필요");
            await CheckConnectionFailures();
            for (int i = 0; i < 2; i++)
            {
                var session = sessions[i];
                session.InputSource = (frame, slot) =>
                {
                    var players = session.Runner?.Game.GetLocalPlayers();
                    if (players == null || players.Count <= slot) return default;
                    return isFinishing ? FinishInput(session, slot, true) : frame <= 960
                        ? QuantumRopeChecks.InputFor(frame, (int)players[slot]) : QuantumMovementChecks.InputFor(frame - 720, (int)players[slot]);
                };
            }
            using (QuantumCallback.SubscribeManual<CallbackSimulateFinished>(callback =>
            {
                if (!callback.Frame.IsVerified) return;
                for (int i = 0; i < 2; i++)
                    if (callback.Game == sessions[i].Runner?.Game && callback.Frame.Number <= 1920)
                    {
                        if (callback.Frame.Number <= 960) evidence[i].Observe(callback.Frame);
                        else movementEvidence[i].Observe(callback.Frame);
                        if (callback.Frame.Number % 120 == 0) checks[i][callback.Frame.Number] = callback.Frame.CalculateChecksum();
                    }
            }))
            {
                string room = "ff-product-check-" + Guid.NewGuid().ToString("N");
                starts = new Task[2];
                starts[0] = sessions[0].StartDevelopmentAsync(Config(true), room, false);
                await Wait(() => sessions[0].Client?.InRoom == true || starts[0].IsFaulted, "개발 방 생성");
                if (starts[0].IsFaulted) await starts[0];
                starts[1] = sessions[1].StartDevelopmentAsync(Config(true), room, true);
                await Task.WhenAll(starts);
                clients[0] = sessions[0].Client; clients[1] = sessions[1].Client;
                await Wait(() => checks.All(values => values.ContainsKey(1920)), "Photon 확정 프레임", 45000);
                foreach (var item in evidence) item.AssertComplete();
                foreach (var item in movementEvidence) item.AssertComplete();
                var samples = checks[0].Where(pair => pair.Key >= 240).ToArray();
                Require(samples.Length == 15 && samples.All(pair => checks[1].TryGetValue(pair.Key, out var other) && other == pair.Value), "제품 Photon 동일 확정 체크섬 15개");
                isFinishing = true;
                await Wait(() => sessions.All(session => session.Runner.Game.Frames.Verified.FlipFriendsCompletedFrame > 0), "Photon 도착 완료", 15000);
                Require(sessions[0].Runner.Game.Frames.Verified.FlipFriendsCompletedFrame == sessions[1].Runner.Game.Frames.Verified.FlipFriendsCompletedFrame, "두 연결 완료 프레임 일치");
                Require(errorCount == 0, "제품 Photon 실행 중 Unity 오류/예외 없음");
                Status = $"통과: 연결 대기 중 취소·콘텐츠 불일치 거부 후 새 QuantumGameSession 두 연결·맵 합의·로프/새 이동 상태 전이·확정 체크섬 15개·도착 완료 프레임={sessions[0].Runner.Game.Frames.Verified.FlipFriendsCompletedFrame}\n지역={clients[0].CurrentRegion}, RTT={clients[0].RealtimePeer.Stats.RoundtripTime}/{clients[1].RealtimePeer.Stats.RoundtripTime}ms, 같은 PC, 지연/손실 주입 없음, Unity={Application.unityVersion}\n" +
                    string.Join("\n", samples.Select(pair => pair.Key + ": " + pair.Value)) + "\n" + evidence[0].Summary + "\n" + evidence[1].Summary + "\n" + movementEvidence[0].Summary + "\n" + movementEvidence[1].Summary;
            }
            foreach (var session in sessions) await session.StopAsync();
            File.WriteAllText("Logs/QuantumProductCloudMovement.txt", Status);
            Status += "\n" + await QuantumInteractionChecks.RunCloud();
            Status += "\n" + await QuantumRespawnChecks.RunCloud();
            Status += "\n" + await QuantumDeviceChecks.RunCloud();
        }
        catch (Exception error) { Status = "실패: " + error; Debug.LogException(error); }
        finally
        {
            foreach (var session in sessions) await session.StopAsync();
            Application.logMessageReceived -= onLog;
            if (errorCount > 0) Status = "실패: 제품 Photon 실행/정리 중 Unity 오류/예외 " + errorCount + "건\n" + Status;
            bool isClean = sessions.All(session => session.Runner == null && session.Client == null && !session.HasInputSubscription) && clients.All(client => client == null || !client.IsConnected);
            Status += "\n종료 후 러너·Photon 연결·입력 콜백 정리=" + isClean;
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/QuantumProductCloud.txt", Status);
            isRunning = false;
        }
        Debug.Log("[Quantum 제품] " + Status);
    }

    private static async Task CheckConnectionFailures()
    {
        var canceled = new QuantumGameSession();
        var start = canceled.StartDevelopmentAsync(Config(true), "ff-cancel-" + Guid.NewGuid().ToString("N"), false);
        await Wait(() => canceled.Client?.InRoom == true || start.IsCompleted, "연결 취소 준비");
        var client = canceled.Client;
        await canceled.StopAsync();
        Require(start.IsCompleted && canceled.Runner == null && !canceled.HasInputSubscription && !client.IsConnected, "Photon 참가자 대기 중 취소 정리");
        var mismatch = new[] { new QuantumGameSession(), new QuantumGameSession() };
        Task[] starts = null;
        try
        {
            string room = "ff-mismatch-" + Guid.NewGuid().ToString("N");
            starts = new Task[2];
            starts[0] = mismatch[0].StartDevelopmentAsync(Config(false), room, false);
            await Wait(() => mismatch[0].Client?.InRoom == true || starts[0].IsCompleted, "불일치 검사 방 생성");
            starts[1] = mismatch[1].StartDevelopmentAsync(Config(true), room, true);
            try { await Task.WhenAll(starts); }
            catch (InvalidOperationException error) { Debug.LogWarning("[Quantum 제품] 예상된 콘텐츠 거부: " + error.Message); }
            Require(starts.All(task => task.IsFaulted || task.IsCanceled) && mismatch.All(session => session.Runner == null), "전원 콘텐츠 합의 전 러너 시작 금지");
        }
        finally { foreach (var session in mismatch) await session.StopAsync(); }
        await AssertNoResidue();
    }

    private static Quantum.Input FinishInput(QuantumGameSession session, int slot, bool online)
    {
        var frame = session.Runner?.Game.Frames.Verified;
        if (frame == null) return default;
        var locals = session.Runner.Game.GetLocalPlayers();
        if (locals.Count <= slot) return default;
        var playerRef = locals[slot];
        var query = frame.Filter<Transform2D, FlipFriendsPlayer>();
        while (query.Next(out var entity, out var transform, out var player))
            if (player.Player == playerRef)
            {
                var distance = frame.RuntimeConfig.GoalCenters[0].X - transform.Position.X;
                bool hasBoxAhead = false;
                var boxes = frame.Filter<Transform2D, FlipFriendsBox>();
                while (boxes.Next(out _, out var boxTransform, out var box))
                    hasBoxAhead |= !box.Carrier.IsValid && FPMath.Abs(boxTransform.Position.X - transform.Position.X) < FP.FromString("1.4");
                return new Quantum.Input { Horizontal = FPMath.Clamp(distance * 2 - player.Velocity.X, -FP._1, FP._1),
                    Jump = hasBoxAhead && frame.Number % 90 < 40,
                    Vertical = FPMath.Abs(distance) < FP.FromString("0.3") ? 1 : 0 };
            }
        return default;
    }

    private static RuntimeConfig Config(bool rope)
    {
        var config = new RuntimeConfig();
        QuantumGameContent.Prepare(AssetDatabase.LoadAssetAtPath<QuantumCustomMapAsset>(rope ? QuantumRopeTestSetup.ManifestPath : QuantumCustomMapSetup.ManifestPath), config);
        return config;
    }

    private static void Click(string name)
    {
        var button = UnityEngine.Object.FindObjectsByType<Button>(FindObjectsInactive.Exclude).Single(x => x.name == name);
        Require(button.interactable, "버튼 활성: " + name);
        button.onClick.Invoke();
    }

    private static async Task Wait(Func<bool> ready, string label, int timeout = 10000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeout);
        while (!ready() && DateTime.UtcNow < deadline && EditorApplication.isPlaying) await Task.Delay(20);
        Require(ready(), "제한 시간: " + label);
    }

    private static void Require(bool value, string label)
    {
        if (!value) throw new InvalidOperationException("제품 자동 검사 실패: " + label);
    }
}
