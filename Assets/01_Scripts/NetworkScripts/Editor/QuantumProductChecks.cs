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

    [MenuItem("Tools/Flip Friends/Quantum/Check Three And Four Player Replay")]
    public static async void RunMultiplayerReplay()
    {
        try
        {
            Require(EditorApplication.isPlaying, "Play 모드 필요");
            var results = new List<string>();
            foreach (int count in new[] { 3, 4 }) results.Add(await CheckMultiplayerReplay(count));
            File.WriteAllLines("Logs/QuantumMultiplayerReplay.txt", results);
            Debug.Log(string.Join("\n", results));
        }
        catch (Exception error)
        {
            File.WriteAllText("Logs/QuantumMultiplayerReplay.txt", error.ToString());
            Debug.LogException(error);
        }
    }

    private static async Task<string> CheckMultiplayerReplay(int count)
    {
        const int firstSample = 120;
        const int target = 840;
        var expected = new Dictionary<int, (ulong checksum, string events)>();
        var actual = new Dictionary<int, (ulong checksum, string events)>();
        var session = new QuantumGameSession();
        session.InputSource = (tick, slot) =>
        {
            if (tick >= 720) return new Quantum.Input { Vertical = 1 };
            if (slot >= 2) return tick < 150 ? new Quantum.Input { Horizontal = slot == 2 ? -1 : 1 } : default;
            return QuantumInteractionChecks.InputFor(session.Runner?.Game.Frames.Verified, tick, slot);
        };
        var config = new RuntimeConfig { FlipFriendsPlayerCount = count };
        QuantumGameContent.Prepare(AssetDatabase.LoadAssetAtPath<QuantumCustomMapAsset>(QuantumInteractionSetup.ManifestPath), config);
        config.Traps = new[] { new FlipFriendsTrapConfig
        {
            Id = "다인 피격 시험", Center = new FPVector2(-2, FP.FromString("0.75")),
            Radius = FP.FromString("0.15"), Direction = new FPVector2(FP.FromString("-0.2"), 0)
        } };
        config.GoalCenters = new[] { new FPVector2(0, 1) };
        config.GoalSizes = new[] { new FPVector2(17, 8) };
        bool hasCarry = false, hasStomp = false, hasDamage = false;
        int completion = 0;
        QuantumRunner replay = null;
        try
        {
            using var observer = QuantumCallback.SubscribeManual<CallbackSimulateFinished>(callback =>
            {
                var frame = callback.Frame;
                if (!frame.IsVerified || frame.Number < firstSample || frame.Number > target) return;
                var evidence = callback.Game == session.Runner?.Game ? expected : callback.Game == replay?.Game ? actual : null;
                if (evidence == null) return;
                var query = frame.Filter<FlipFriendsPlayer>();
                var states = new string[count];
                var homes = new HashSet<FPVector2>();
                while (query.Next(out _, out var player))
                {
                    homes.Add(player.HomePosition);
                    states[(int)player.Player] = $"{player.PickupCount}/{player.StompCount}/{player.DamageCount}/{player.DamageFrame}/{player.FinishedFrame}";
                    if (evidence == expected)
                    {
                        hasCarry |= player.PickupCount > 0;
                        hasStomp |= player.StompCount > 0;
                        hasDamage |= player.DamageCount > 0;
                    }
                }
                if (states.All(value => value != null))
                    Require(homes.Count == count, "부족한 시작점 주변에 서로 다른 초기 위치 배정");
                if (evidence == expected) completion = frame.FlipFriendsCompletedFrame;
                evidence[frame.Number] = (frame.Number % 30 == 0 ? frame.CalculateChecksum() : 0,
                    frame.FlipFriendsCompletedFrame + ":" + string.Join(",", states));
            });
            await session.StartLocalAsync(config);
            var original = session.Runner;
            original.IsSessionUpdateDisabled = true;
            for (int i = 0; i < target + 240 && !expected.ContainsKey(target); i++) original.Service(1f / 60);
            Require(expected.ContainsKey(target), "다인 기록 확정 프레임");
            var record = original.Game.GetRecordedReplay(includeChecksums: true);
            File.WriteAllText($"Logs/QuantumMultiplayer{count}Replay.json", JsonUtility.ToJson(record));
            var serializer = new QuantumUnityJsonSerializer();
            replay = QuantumRunner.StartGame(new SessionRunner.Arguments
            {
                RunnerId = "FF_MULTIPLAYER_REPLAY",
                RuntimeConfig = serializer.ConfigFromByteArray<RuntimeConfig>(record.RuntimeConfigData.Decode(), compressed: true),
                SessionConfig = record.DeterministicConfig, ReplayProvider = record.CreateInputProvider(),
                InitialTick = record.InitialTick, FrameData = record.InitialFrameData,
                GameMode = DeterministicGameMode.Replay, PlayerCount = count
            });
            replay.IsSessionUpdateDisabled = true;
            for (int i = 0; i < target + 240 && !actual.ContainsKey(target); i++) replay.Service(1f / 60);
            int mismatch = expected.Keys.FirstOrDefault(tick => !actual.TryGetValue(tick, out var value) || value != expected[tick]);
            Require(expected.Count == actual.Count && mismatch == 0,
                $"다인 기록·재생 일치 ({count}인 기록 {expected.Count}, 재생 {actual.Count}, 첫 불일치 {mismatch})");
            Require(hasCarry && hasStomp && hasDamage && completion > 0,
                $"다인 사건 재현 (운반 {hasCarry}, 밟기 {hasStomp}, 피격 {hasDamage}, 완료 {completion})");
            return $"{count}인: 확정 {actual.Count}프레임, 체크섬 {actual.Count(pair => pair.Value.checksum != 0)}개, 운반/밟기/피격/완료 틱 일치. 완료 {completion}";
        }
        finally
        {
            if (replay != null) await replay.ShutdownAsync();
            await session.StopAsync();
        }
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
        await Click("Start Button");
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
            await Click("오프라인 시작");
            await Wait(() => flow.State == QuantumGameState.Failed, "미지원 콘텐츠 시작 전 거부");
            Require(flow.Message.Contains("위치=") && flow.Session == null, "미지원 위치·원인 UI 표시 및 부분 시작 없음");
        }
        finally
        {
            flow.SupportedMaps[0] = valid;
            UnityEngine.Object.Destroy(invalid);
            UnityEngine.Object.Destroy(badSource);
        }
        await Click("온라인 시작");
        TMPro.TMP_InputField codeField = null;
        await Wait(() => (codeField = UnityEngine.Object.FindObjectsByType<TMPro.TMP_InputField>()
            .SingleOrDefault(field => field.name == "방 코드 입력")) != null && codeField.IsInteractable(), "방 코드 입력란 표시");
        codeField.text = "1234";
        await Click("참가");
        Require(codeField.text == "1234" && flow.Message.Contains("8자리"), "잘못된 코드 안내 및 입력 유지");
        await Click("방 만들기");
        await Wait(() => flow.State == QuantumGameState.Failed, "출시 설정 실패 표시");
        Require(flow.Session == null && flow.Message.Contains("출시 Photon"), "출시 익명 폴백 없음");
        await Click("취소");
        await Click("로프 맵 선택");
        await Click("오프라인 시작");
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
        await Click("Start Button");
        await Click("정적 맵 선택");
        await Click("오프라인 시작");
        await Wait(() => flow.State == QuantumGameState.Playing, "종료 후 재진입");
        await Click("플레이 종료");
        await Wait(() => flow.State == QuantumGameState.Ready && SceneManager.GetActiveScene().name == "Main", "메뉴 종료");
        await AssertNoResidue();
        await Click("Start Button");
        await Click("오프라인 시작");
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
        await Click("Start Button");
        await Click("상호작용 맵 선택");
        await Click("오프라인 시작");
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
        await Click("Start Button"); await Click("상호작용 맵 선택"); await Click("오프라인 시작");
        await Wait(() => flow.State == QuantumGameState.Playing, "상호작용 맵 재진입");
        await Click("플레이 종료");
        await Wait(() => flow.State == QuantumGameState.Ready, "상호작용 재진입 종료");
        await AssertNoResidue();
    }

    private static async Task CheckRespawnMain()
    {
        var flow = QuantumGameFlow.Instance;
        await Click("Start Button"); await Click("복귀 맵 선택"); await Click("오프라인 시작");
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
        await Click("Start Button"); await Click("복귀 맵 선택"); await Click("오프라인 시작");
        await Wait(() => flow.State == QuantumGameState.Playing, "복귀 맵 재진입");
        await Click("플레이 종료"); await Wait(() => flow.State == QuantumGameState.Ready, "복귀 맵 메뉴 종료");
        await AssertNoResidue();
    }

    private static async Task CheckDeviceMain()
    {
        var flow = QuantumGameFlow.Instance;
        await Click("Start Button"); await Click("장치 맵 선택"); await Click("오프라인 시작");
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
        await Click("Start Button"); await Click("장치 맵 선택"); await Click("오프라인 시작");
        await Wait(() => flow.State == QuantumGameState.Playing, "장치 맵 재진입");
        await Click("플레이 종료"); await Wait(() => flow.State == QuantumGameState.Ready, "장치 맵 메뉴 종료");
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

    private static RuntimeConfig Config(bool rope, int playerCount = 2)
    {
        var config = new RuntimeConfig { FlipFriendsPlayerCount = playerCount };
        QuantumGameContent.Prepare(AssetDatabase.LoadAssetAtPath<QuantumCustomMapAsset>(rope ? QuantumRopeTestSetup.ManifestPath : QuantumCustomMapSetup.ManifestPath), config);
        return config;
    }

    // 준비 화면은 화면 전환 중 CanvasGroup으로 입력을 막고 그동안의 클릭을 버린다. 실제로 누를 수 있을 때까지 기다린다.
    private static async Task Click(string name)
    {
        Button button = null;
        await Wait(() => (button = UnityEngine.Object.FindObjectsByType<Button>(FindObjectsInactive.Exclude)
            .SingleOrDefault(x => x.name == name)) != null && button.IsInteractable(), "버튼 활성: " + name);
        button.onClick.Invoke();
    }

    [MenuItem("Tools/Flip Friends/Quantum/Check Saved Map Sharing")]
    public static async void RunSavedMapSharing()
    {
        if (isRunning) return;
        isRunning = true;
        string directory = Path.Combine(Path.GetTempPath(), "ff-saved-map-" + Guid.NewGuid().ToString("N"));
        var saveDirectory = typeof(MapDataRepository).GetProperty("SaveDirectoryOverride",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
        var previousDirectory = saveDirectory.GetValue(null);
        var flow = QuantumGameFlow.Instance;
        var evidence = new List<string>();
        try
        {
            Require(EditorApplication.isPlaying && flow != null && !flow.IsBusy && SceneManager.GetActiveScene().name == "Main", "Main 유휴 상태 필요");
            Directory.CreateDirectory(directory);
            saveDirectory.SetValue(null, directory);
            var palette = AssetDatabase.LoadAssetAtPath<MapEditorPalette>(QuantumMapConverter.PaletteAssetPath);
            var data = MapDataRepository.FromJson(Resources.Load<TextAsset>("FlipFriendsCustomMap").text);
            data.mapId = Guid.NewGuid().ToString("N");
            data.mapName = "친구 맵 검사";
            for (int index = 0; index < 240; index++)
                data.objects.Add(new PlacedObjectData("terrain.ground.basic",
                    new Vector3(index % 80 - 40, 20 + index / 80, 0), 0, Vector3.one));
            Require(MapDataRepository.TryCreate(data, false, out _), "격리한 저장 맵 생성");
            string path = Directory.GetFiles(directory, "*.json").Single();
            byte[] original = File.ReadAllBytes(path);
            Require(MapSessionSnapshot.TryCreate(0, MapDataRepository.ToJson(data), MapSessionSnapshot.MaximumContentBytes,
                palette, out var source, out _), "전송 스냅샷 생성");

            flow.SelectNextSavedMap();
            Require(flow.SavedMapName == data.mapName, "저장 맵 선택");
            if (flow.PlayerCount != flow.KeyboardSlots)
                while (flow.PlayerCount != flow.KeyboardSlots) flow.CycleParticipants();
            var localStart = flow.StartAsync(false);
            Require(ReferenceEquals(localStart, flow.StartAsync(false)), "중복 시작 공유");
            await localStart;
            Require(flow.State == QuantumGameState.Playing && flow.Content != null, "저장 맵 오프라인 시작");
            await flow.ReturnAsync("저장 맵 검사");
            Require(flow.Session == null && flow.Content == null && flow.SavedMapName == null &&
                QuantumGameSession.ActiveInputSubscriptions == 0, "저장 맵 오프라인 정리");

            var unsupported = JsonUtility.FromJson<MapData>(MapDataRepository.ToJson(data));
            unsupported.objects[0].prefabID = "unknown.prefab";
            File.WriteAllText(path, MapDataRepository.ToJson(unsupported));
            flow.SelectNextSavedMap();
            Require(flow.SavedMapName == null && flow.State == QuantumGameState.Failed, "미지원 맵 거부");
            File.WriteAllText(path, MapDataRepository.ToJson(data).Replace("\"2.1\"", "\"9.0\""));
            flow.SelectNextSavedMap();
            Require(flow.SavedMapName == null && flow.State == QuantumGameState.Failed, "다른 MapData 버전 거부");
            string legacy = MapDataRepository.ToJson(data).Replace("\"2.1\"", "\"2.0\"");
            File.WriteAllText(path, legacy);
            flow.SelectNextSavedMap();
            Require(flow.SavedMapName == null && File.ReadAllText(path) == legacy, "2.0 맵 선택 거부 및 파일 보존");
            File.WriteAllBytes(path, original);
            flow.SelectNextSavedMap();
            Require(flow.SavedMapName == data.mapName, "실패 후 다시 선택");
            evidence.Add("저장 맵 선택·오프라인 시작/중복·정리·미지원/버전 거부·재선택");

            var bad = new QuantumGameSession();
            try
            {
                source.Content[0] ^= 1;
                await ExpectSavedMapFailure(bad.StartSavedMapDevelopmentAsync(new RuntimeConfig { FlipFriendsPlayerCount = 2 },
                    "ff-saved-check-" + Guid.NewGuid().ToString("N"), palette, source), "해시");
            }
            finally
            {
                source.Content[0] ^= 1;
                await bad.StopAsync();
            }
            evidence.Add("잘못된 스냅샷 해시 거부");

            var release = new QuantumGameSession();
            try
            {
                await ExpectSavedMapFailure(release.StartOnlineAsync(new RuntimeConfig { FlipFriendsPlayerCount = 2 },
                    new Photon.Realtime.AppSettings { AppIdQuantum = QuantumGameSession.DevelopmentAppId.ToUpperInvariant() },
                    0, "ff-release-check"), "출시 Photon");
            }
            finally { await release.StopAsync(); }
            Require(release.Client == null, "출시 경로에서 개발 앱 접속 차단");
            evidence.Add("출시 경로에서 개발 앱 접속 차단");

            await CheckSavedMapPair(palette, source, false);
            Require(File.ReadAllBytes(path).SequenceEqual(original), "보유 맵 바이트 보존");
            evidence.Add("동일 PC 개발 Photon 2연결 보유 맵 합의·시작·정리");

            File.Delete(path);
            await CheckSavedMapPair(palette, source, true);
            Require(Directory.GetFiles(directory).Length == 0, "자동 수신 파일 없음");
            evidence.Add("동일 PC 개발 Photon 2연결 필요 청크 전송·합의·시작·정리 및 수신 파일 없음");

            var waiting = new QuantumGameSession();
            try
            {
                bool connected = false;
                var start = waiting.StartSavedMapDevelopmentAsync(new RuntimeConfig { FlipFriendsPlayerCount = 2 },
                    "ff-saved-check-" + Guid.NewGuid().ToString("N"), palette, source, () => connected = true);
                await Wait(() => connected || start.IsCompleted, "취소 전 방 연결", 20000);
                if (start.IsCompleted) await start;
                var client = waiting.Client;
                await waiting.StopAsync();
                Require(start.IsCanceled && client != null && !client.IsConnected &&
                    waiting.Client == null && waiting.Runner == null && !waiting.HasInputSubscription, "취소 정리");
            }
            finally { await waiting.StopAsync(); }
            evidence.Add("맵 보유 확인 대기 중 취소·Photon 정리");

            await CheckSavedMapPair(palette, source, true, true);
            evidence.Add("여러 청크 중 첫 수신 뒤 취소·참가자 이탈로 시작 거부 및 정리");
            await CheckSavedMapPair(palette, source, true, corrupt: true);
            evidence.Add("수신 콘텐츠 해시 불일치로 시작 거부");
            var timeout = new QuantumGameSession();
            try
            {
                var start = timeout.StartSavedMapDevelopmentAsync(new RuntimeConfig { FlipFriendsPlayerCount = 2 },
                    "ff-saved-check-" + Guid.NewGuid().ToString("N"), palette, source);
                await ExpectSavedMapFailure(start, "초과");
            }
            finally { await timeout.StopAsync(); }
            Require(timeout.Client == null && timeout.Runner == null && !timeout.HasInputSubscription, "제한 시간 초과 정리");
            await CheckSavedMapPair(palette, source, true);
            evidence.Add("시간 초과·정리 뒤 새 방에서 전송 재시도");
            Status = "통과";
        }
        catch (Exception error) { Status = "실패: " + error; Debug.LogException(error); }
        finally
        {
            if (flow != null && flow.IsBusy) await flow.ReturnAsync("저장 맵 검사 종료");
            flow?.ClearSavedSelection();
            saveDirectory.SetValue(null, previousDirectory);
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
            File.WriteAllLines("Logs/QuantumSavedMapSharing.txt", evidence.Append(Status));
            isRunning = false;
        }
    }

    private static async Task CheckSavedMapPair(MapEditorPalette palette, MapSessionSnapshot source, bool needsTransfer, bool leaves = false, bool corrupt = false)
    {
        var host = new QuantumGameSession();
        var guest = new QuantumGameSession();
        Photon.Realtime.RealtimeClient hostClient = null, guestClient = null;
        var dynamicMap = typeof(QuantumGameSession).GetField("dynamicMap",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        var maps = new UnityEngine.Object[2];
        byte originalByte = source.Content[0];
        try
        {
            string room = "ff-saved-check-" + Guid.NewGuid().ToString("N");
            bool connected = false;
            var hostStart = host.StartSavedMapDevelopmentAsync(new RuntimeConfig { FlipFriendsPlayerCount = 2 },
                room, palette, source, () => connected = true);
            await Wait(() => connected || hostStart.IsCompleted, "방장 개발 연결", 20000);
            if (hostStart.IsCompleted) await hostStart;
            hostClient = host.Client;
            if (corrupt) source.Content[0] ^= 1;
            int chunks = 0;
            var guestStart = guest.StartSavedMapDevelopmentAsync(new RuntimeConfig { FlipFriendsPlayerCount = 2 },
                room, palette, null, () =>
                {
                    guestClient = guest.Client;
                    guestClient.EventReceived += message => { if (message.Code == 75) chunks++; };
                });
            if (leaves)
            {
                await Wait(() => chunks > 0 || guestStart.IsCompleted, "이탈 전 첫 청크 수신", 20000);
                if (guestStart.IsCompleted) await guestStart;
                await guest.StopAsync();
                Require(guestStart.IsCanceled, "전송 중 수신 취소");
                await ExpectSavedMapFailure(hostStart, "나갔습니다");
                return;
            }

            if (corrupt)
            {
                await ExpectSavedMapFailure(guestStart, "해시");
                await guest.StopAsync();
                await ExpectSavedMapFailure(hostStart, "나갔습니다");
                return;
            }

            await Task.WhenAll(hostStart, guestStart);
            maps[0] = (UnityEngine.Object)dynamicMap.GetValue(host);
            maps[1] = (UnityEngine.Object)dynamicMap.GetValue(guest);
            Require(maps.All(map => map != null), "동적 맵 생성");
            Require((needsTransfer ? chunks > 1 : chunks == 0) && host.Runner != null && guest.Runner != null &&
                !hostClient.CurrentRoom.IsOpen && !guestClient.CurrentRoom.IsOpen, "필요한 경우만 전송하고 방을 닫은 뒤 시작");
            await Wait(() => host.Runner.Game.Frames.Verified?.Number > 120 && guest.Runner.Game.Frames.Verified?.Number > 120,
                "두 러너의 확정 프레임", 15000);
        }
        finally
        {
            source.Content[0] = originalByte;
            await Task.WhenAll(host.StopAsync(), guest.StopAsync());
            await Wait(() => maps.All(map => map == null), "동적 맵 파괴");
            Require(hostClient?.IsConnected != true && guestClient?.IsConnected != true &&
                host.Runner == null && guest.Runner == null && host.Client == null && guest.Client == null &&
                QuantumGameSession.ActiveInputSubscriptions == 0 && QuantumPlayerInput.ActiveDeviceSubscriptions == 0, "두 러너·Photon·입력 정리");
        }
    }

    private static async Task ExpectSavedMapFailure(Task start, string reason)
    {
        try { await start; }
        catch (InvalidOperationException error) when (error.Message.Contains(reason)) { return; }
        throw new InvalidOperationException("예상한 저장 맵 실패 사유가 없습니다: " + reason);
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
