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
using Button = UnityEngine.UI.Button;
using LogType = UnityEngine.LogType;

public static class QuantumSlopeChecks
{
    public static string Status { get; private set; } = "미실행";
    public static string IntegrationStatus { get; private set; } = "미실행";
    public static RuntimeConfig Config()
    {
        var config = new RuntimeConfig();
        QuantumGameContent.Prepare(AssetDatabase.LoadAssetAtPath<QuantumOfficialStageAsset>(QuantumSlopeSetup.StagePath), config);
        return config;
    }

    public static void RunContent()
    {
        var read = QuantumOfficialStageWriter.ReadPrefab(QuantumSlopeSetup.PrefabPath);
        var asset = AssetDatabase.LoadAssetAtPath<QuantumOfficialStageAsset>(QuantumSlopeSetup.StagePath);
        var terrain = ScriptableObject.CreateInstance<QuantumMapTerrain>();
        try
        {
            terrain.Configure(read.Terrain, read.TerrainBounds, read.TerrainSignature);
            Require(QuantumOfficialStageConverter.Convert(read.Data, terrain, null).ContentHash == asset.ContentHash, "경사 프리팹 재로드 해시");
            Require(read.Terrain.SelectMany(c => c.vertices.Select((v, i) => c.vertices[(i + 1) % c.vertices.Length] - v)).Any(d => Mathf.Abs(d.x) > 1 && Mathf.Abs(d.y) > 1), "실제 경사 윤곽 저장");
            foreach (float height in new[] { 0.5f, 1f, 1.01f, 2f })
            {
                terrain.Configure(new[] { new QuantumTerrainSource { id = "boundary", vertices = new[] {
                    new Vector2(100, -2), new Vector2(102, -2), new Vector2(102, height * 2), new Vector2(100, 0) } } },
                    new Bounds(new Vector3(101, 1), new Vector3(4, 10, 1)), "boundary");
                bool rejected = false;
                try { terrain.Apply(new QuantumMapConversion { CanonicalContent = "" }); }
                catch (InvalidOperationException error) { Require(error.Message.Contains("위치=") && error.Message.Contains("45도") && error.Message.Contains("변="), "미지원 경사 원인/위치"); rejected = true; }
                Require(rejected == (height > 1), "45도 포함/초과 작성기 경계");
            }
            File.WriteAllText("Logs/QuantumSlopeContent.txt", "통과: 실제 경사 프리팹 재로드 해시·45도 포함/초과 거부 위치/변/원인");
        }
        finally { UnityEngine.Object.DestroyImmediate(terrain); }
    }

    public static Quantum.Input InputFor(int tick, int player)
    {
        if (tick < 120 + player * 180) return default;
        int t = tick - 120 - player * 180;
        return new Quantum.Input { Horizontal = t < 480 ? 1 : t < 660 ? 0 : t < 1140 ? -1 : 1,
            Run = t >= 660, Jump = t >= 180 && t < 210 || t >= 850 && t < 875 };
    }

    public static async void Run()
    {
        int errors = 0;
        Application.LogCallback onLog = (_, _, type) => { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors++; };
        Application.logMessageReceived += onLog;
        try { Status = "검사 중"; await Replay(); Require(errors == 0, "실행 중 Unity 오류0"); Status = "통과: 경사 입력 기록·별도 러너 재생16개·확정 몸체 비관통·오르막/내리막·직접 전이·Unity 오류0"; }
        catch (Exception error) { Status = "실패: " + error; Debug.LogException(error); }
        finally { Application.logMessageReceived -= onLog; File.WriteAllText("Logs/QuantumSlopeChecks.txt", Status); }
    }

    public static Quantum.Input FinishInput(Frame frame, int tick, int player)
    {
        if (frame == null || tick < 120 + player * 120) return default;
        var query = frame.Filter<Transform2D, FlipFriendsPlayer>();
        while (query.Next(out _, out var t, out var p))
        {
            if ((int)p.Player != player) continue;
            var gap = frame.RuntimeConfig.GoalCenters[0].X - t.Position.X;
            return new Quantum.Input { Horizontal = FPMath.Clamp(gap * 4 - p.Velocity.X * 2, -FP._1, FP._1),
                Run = true, Vertical = FPMath.Abs(gap) < FP.FromString("0.3") ? 1 : 0 };
        }
        return default;
    }

    public static async void RunMain()
    {
        try
        {
            IntegrationStatus = "Main 검사 중";
            var flow = QuantumGameFlow.Instance;
            var stage = AssetDatabase.LoadAssetAtPath<QuantumOfficialStageAsset>(QuantumSlopeSetup.StagePath);
            void Select()
            {
                Click("Start Button");
                for (int i = 0; i < flow.OfficialStages.Count; i++)
                { Click("공식 스테이지 선택"); if (flow.SelectedContent == (IQuantumMapContent)stage) return; }
                Require(false, "Main 경사 목록 선택");
            }
            Select(); Click("오프라인 시작"); await Wait(() => flow.State == QuantumGameState.Playing, 10000);
            var session = flow.Session;
            session.InputSource = (tick, slot) => FinishInput(session.Runner?.Game.Frames.Verified, tick, slot);
            await Wait(() => flow.State == QuantumGameState.Ready && SceneManager.GetActiveScene().name == "Main", 60000);
            Require(!QuantumRunner.ActiveRunners.Any() && QuantumGameSession.ActiveInputSubscriptions == 0, "경사 완료 정리");
            Select(); Click("오프라인 시작"); await Wait(() => flow.State == QuantumGameState.Playing, 10000);
            Click("플레이 종료"); await Wait(() => flow.State == QuantumGameState.Ready, 10000);
            Require(!QuantumRunner.ActiveRunners.Any() && QuantumGameSession.ActiveInputSubscriptions == 0, "경사 재진입/메뉴 정리");
            IntegrationStatus = "통과: Main 실제 버튼 경사 선택·플레이·전원 완료·복귀·재진입·러너/입력0";
        }
        catch (Exception error) { IntegrationStatus = "실패: " + error; Debug.LogException(error); }
        finally { File.WriteAllText("Logs/QuantumSlopeMain.txt", IntegrationStatus); }
    }

    public static async void RunCloud()
    {
        var sessions = new[] { new QuantumGameSession(), new QuantumGameSession() };
        var checks = new[] { new Dictionary<int, ulong>(), new Dictionary<int, ulong>() };
        try
        {
            IntegrationStatus = "Photon 검사 중";
            foreach (var session in sessions)
                session.InputSource = (tick, slot) => { var players = session.Runner?.Game.GetLocalPlayers(); return players == null || slot >= players.Count ? default : FinishInput(session.Runner.Game.Frames.Verified, tick, (int)players[slot]); };
            using (QuantumCallback.SubscribeManual<CallbackSimulateFinished>(c =>
            {
                if (!c.Frame.IsVerified) return;
                for (int i = 0; i < 2; i++) if (c.Game == sessions[i].Runner?.Game && c.Frame.Number <= 1200 && c.Frame.Number % 120 == 0)
                    checks[i][c.Frame.Number] = c.Frame.CalculateChecksum();
            }))
            {
                string room = "ff-slope-" + Guid.NewGuid().ToString("N");
                var first = sessions[0].StartDevelopmentAsync(Config(), room, false);
                await Wait(() => sessions[0].Client?.InRoom == true || first.IsFaulted, 15000); if (first.IsFaulted) await first;
                await Task.WhenAll(first, sessions[1].StartDevelopmentAsync(Config(), room, true));
                await Wait(() => sessions.All(s => s.Runner.Game.Frames.Verified.FlipFriendsCompletedFrame > 0) && checks.All(c => c.ContainsKey(1200)), 60000);
                var samples = checks[0].Where(p => p.Key >= 240).ToArray();
                Require(samples.Length == 9 && samples.All(p => checks[1].TryGetValue(p.Key, out var h) && p.Value == h), "경사 Photon 확정9개");
                int completed = sessions[0].Runner.Game.Frames.Verified.FlipFriendsCompletedFrame;
                Require(completed == sessions[1].Runner.Game.Frames.Verified.FlipFriendsCompletedFrame, "경사 Photon 완료 프레임");
                IntegrationStatus = $"통과: 경사 Photon 동일PC 두 연결·지역={sessions[0].Client.CurrentRegion}·RTT={sessions[0].Client.RealtimePeer.Stats.RoundtripTime}/{sessions[1].Client.RealtimePeer.Stats.RoundtripTime}ms·완료={completed}·확정9개 일치·지연/손실 주입 없음\n" + string.Join("\n", samples.Select(p => p.Key + ": " + p.Value));
            }
        }
        catch (Exception error) { IntegrationStatus = "실패: " + error; Debug.LogException(error); }
        finally
        {
            foreach (var session in sessions) await session.StopAsync();
            IntegrationStatus += "\n러너/연결/입력 정리=" + sessions.All(s => s.Runner == null && s.Client == null && !s.HasInputSubscription);
            File.WriteAllText("Logs/QuantumSlopeCloud.txt", IntegrationStatus);
        }
    }

    private static void Click(string name) => UnityEngine.Object.FindObjectsByType<Button>(FindObjectsInactive.Exclude).Single(b => b.name == name).onClick.Invoke();
    private static async Task Wait(Func<bool> ready, int timeout)
    { var end = DateTime.UtcNow.AddMilliseconds(timeout); while (!ready() && DateTime.UtcNow < end && EditorApplication.isPlaying) await Task.Delay(20); Require(ready(), "비동기 제한시간"); }

    private static async Task Replay()
    {
        var session = new QuantumGameSession { InputSource = InputFor }; QuantumRunner replay = null;
        var expected = new Dictionary<int, ulong>(); var actual = new Dictionary<int, ulong>();
        var trace = new List<string>(); var highest = new FP[2]; bool up = false, down = false;
        var previous = new FPVector2[2];
        try
        {
            using (QuantumCallback.SubscribeManual<CallbackSimulateFinished>(c =>
            {
                if (!c.Frame.IsVerified || c.Frame.Number > 1920) return;
                if (c.Game == session.Runner?.Game)
                {
                    var q = c.Frame.Filter<Transform2D, FlipFriendsPlayer>();
                    while (q.Next(out _, out var t, out var p))
                    {
                        int i = (int)p.Player;
                        Require(!FlipFriendsTerrain.Overlaps(c.Frame.RuntimeConfig.TerrainContours,
                            t.Position + FlipFriendsInteractionSystem.Offset(p), FlipFriendsInteractionSystem.HalfSize(p)), $"확정{c.Frame.Number} P{i} 지형 관통 {t.Position}");
                        highest[i] = FPMath.Max(highest[i], t.Position.Y);
                        up |= p.IsGrounded && t.Position.Y > previous[i].Y + FP.FromRaw(8);
                        down |= p.IsGrounded && t.Position.Y < previous[i].Y - FP.FromRaw(8);
                        previous[i] = t.Position;
                        if (c.Frame.Number % 30 == 0) trace.Add($"{c.Frame.Number} P{i} {t.Position} v={p.Velocity} ground={p.IsGrounded}");
                    }
                    if (c.Frame.Number % 120 == 0) expected[c.Frame.Number] = c.Frame.CalculateChecksum();
                }
                if (c.Game == replay?.Game && c.Frame.Number % 120 == 0) actual[c.Frame.Number] = c.Frame.CalculateChecksum();
            }))
            {
                await session.StartLocalAsync(Config()); session.Runner.IsSessionUpdateDisabled = true;
                for (int i = 0; i < 2200 && !expected.ContainsKey(1920); i++) session.Runner.Service(1f / 60);
                File.WriteAllText("Logs/QuantumSlopeTrace.txt", string.Join("\n", trace));
                Require(highest.All(y => y > 4) && up && down, "양쪽 정상 도달/접지 오르내림");
                var record = session.Runner.Game.GetRecordedReplay(includeChecksums: true); var serializer = new QuantumUnityJsonSerializer();
                replay = QuantumRunner.StartGame(new SessionRunner.Arguments { RunnerId = "FF_SLOPE_REPLAY",
                    RuntimeConfig = serializer.ConfigFromByteArray<RuntimeConfig>(record.RuntimeConfigData.Decode(), compressed: true),
                    SessionConfig = record.DeterministicConfig, ReplayProvider = record.CreateInputProvider(), InitialTick = record.InitialTick,
                    FrameData = record.InitialFrameData, GameMode = DeterministicGameMode.Replay, PlayerCount = 2 });
                replay.IsSessionUpdateDisabled = true;
                for (int i = 0; i < 2400 && !actual.ContainsKey(1920); i++) replay.Service(i % 2 == 0 ? 1f / 30 : 1f / 120);
                Require(expected.Count == 16 && actual.Count == 16 && expected.All(p => actual[p.Key] == p.Value), "확정 체크섬16개 일치");
                File.WriteAllText("Logs/QuantumSlopeReplay.json", JsonUtility.ToJson(record));
                File.WriteAllText("Logs/QuantumSlopeChecksums.txt", string.Join("\n", expected.Select(p => p.Key + ": " + p.Value)));
                Direct(session.Runner.Game.Frames.Verified);
            }
        }
        finally { if (replay != null) await replay.ShutdownAsync(); await session.StopAsync(); }
    }

    private static void Direct(Frame frame)
    {
        var original = frame.RuntimeConfig.TerrainContours;
        var cases = new List<string>();
        try
        {
            foreach (int direction in new[] { -1, 1 })
            {
                // 실제 다른 몸체·장치에서 떨어진 좌표에서 동일한 프레임 API로 양방향 전이를 검사한다.
                frame.RuntimeConfig.TerrainContours = new[] { new FlipFriendsTerrainContour { Id = "direct", Vertices = new[] {
                    new FPVector2(90, -30), new FPVector2(120, -30), new FPVector2(120, direction > 0 ? 10 : -10),
                    new FPVector2(90, direction > 0 ? -5 : 5) } } };
                FP x = 104, floor = direction > 0 ? 2 : -2;
                var t = new Transform2D { Position = new FPVector2(x, floor + FP.FromString("0.7")) };
                var p = new FlipFriendsPlayer();
                for (int i = 0; i < 90; i++) FlipFriendsMovementSystem.Simulate(frame, ref t, ref p, default);
                Require(p.IsGrounded && p.Velocity.Y == 0, " 경사 정지 접지");
                var stop = t.Position;
                for (int i = 0; i < 60; i++) FlipFriendsMovementSystem.Simulate(frame, ref t, ref p, default);
                Require(t.Position == stop, "경사 위 정지 떨림 없음");
                FlipFriendsMovementSystem.Simulate(frame, ref t, ref p, new Quantum.Input { Jump = true });
                Require(p.Velocity.Y > 12 && !p.IsGrounded && t.Position.Y > stop.Y, "경사 수직 일반 점프");
                for (int i = 0; i < 150; i++) FlipFriendsMovementSystem.Simulate(frame, ref t, ref p, default);
                Require(p.IsGrounded && t.Position == stop, "경사 점프 뒤 같은 자리 착지");
                foreach (int move in new[] { direction, -direction })
                {
                    t.Position = stop; p = new FlipFriendsPlayer { IsGrounded = true };
                    for (int i = 0; i < 120; i++)
                    {
                        var old = t.Position;
                        FlipFriendsMovementSystem.Simulate(frame, ref t, ref p, new Quantum.Input { Horizontal = move, Run = i >= 60 });
                        Require(p.IsGrounded && !FlipFriendsTerrain.Overlaps(frame.RuntimeConfig.TerrainContours, t.Position, FlipFriendsInteractionSystem.HalfSize(p)), "양방향 걷기/달리기 접지 비관통");
                        // 틱 이동량을 FP로 제곱하면 가속 초기에는 1raw 미만으로 잘린다. 검사 측정만 double로 한다.
                        var movement = t.Position - old;
                        double length = Math.Sqrt(movement.X.AsDouble * movement.X.AsDouble + movement.Y.AsDouble * movement.Y.AsDouble);
                        Require(Math.Abs(length - Math.Abs((p.Velocity.X * frame.DeltaTime).AsDouble)) < 0.0002,
                            $"접선 속도 원본 sin/cos 일치 방향={direction}/{move} 틱={i} 전={old} 후={t.Position} 속도={p.Velocity.X} dt={frame.DeltaTime}");
                    }
                    Require((t.Position.Y - stop.Y) * direction * move > 0, "오르막/내리막 방향");
                    for (int i = 0; i < 120; i++) FlipFriendsMovementSystem.Simulate(frame, ref t, ref p, new Quantum.Input { Horizontal = -move });
                    Require(p.Velocity.X * move < 0, "경사 반전");
                }
                t.Position = stop; p = new FlipFriendsPlayer { IsGrounded = true, ShrinkTicks = 30 };
                for (int i = 0; i < 60; i++) FlipFriendsMovementSystem.Simulate(frame, ref t, ref p, new Quantum.Input { Horizontal = direction });
                Require(p.IsGrounded && !FlipFriendsTerrain.Overlaps(frame.RuntimeConfig.TerrainContours, t.Position + FlipFriendsInteractionSystem.Offset(p), FlipFriendsInteractionSystem.HalfSize(p)), "축소 경사 이동");
                cases.Add("정지/출발/걷기/달리기/접선속도/양방향/반전/점프/착지/축소 " + direction);
                CheckThrows(frame, stop, cases);

                var slope = frame.RuntimeConfig.TerrainContours[0];
                foreach (int heldKind in new[] { 0, 1, 2 })
                {
                    bool carrying = heldKind > 0;
                    t.Position = stop; p = new FlipFriendsPlayer { IsGrounded = true };
                    EntityRef held = default;
                    try
                    {
                        if (carrying)
                        {
                            held = frame.Create(); frame.Add(held, new Transform2D { Position = stop + FlipFriendsInteractionSystem.HeldOffset });
                            if (heldKind == 1) frame.Add(held, new FlipFriendsBox { HalfSize = new FPVector2(FP.FromString("0.45"), FP.FromString("0.45")) });
                            else frame.Add(held, new FlipFriendsPlayer());
                            p.Held = held;
                        }
                        FP ceiling = stop.Y + (heldKind == 2 ? FP.FromString("1.46") : carrying ? FP.FromString("1.41") : FP.FromString("0.51"));
                        frame.RuntimeConfig.TerrainContours = new[] { slope, new FlipFriendsTerrainContour { Id = "ceiling", Vertices = new[] {
                            new FPVector2(100, ceiling), new FPVector2(110, ceiling), new FPVector2(110, ceiling + 1), new FPVector2(100, ceiling + 1) } } };
                        for (int i = 0; i < 90; i++)
                        {
                            FlipFriendsMovementSystem.Simulate(frame, ref t, ref p, new Quantum.Input { Horizontal = direction });
                            Require(!FlipFriendsTerrain.Overlaps(frame.RuntimeConfig.TerrainContours, t.Position, FlipFriendsInteractionSystem.HalfSize(p)), "천장 인접 경사 몸체 비관통");
                            if (carrying) Require(!FlipFriendsTerrain.Overlaps(frame.RuntimeConfig.TerrainContours, t.Position + FlipFriendsInteractionSystem.HeldOffset,
                                heldKind == 1 ? frame.Get<FlipFriendsBox>(held).HalfSize : FlipFriendsInteractionSystem.HalfSize(frame.Get<FlipFriendsPlayer>(held))), "경사 운반물 천장 비관통");
                        }
                        Require(FPMath.Abs(t.Position.X - stop.X) < FP.FromString("0.04"), "막힌 천장 경사 상승 정지");
                        cases.Add("천장 인접 경사/운반종류=" + heldKind + "/방향=" + direction);
                    }
                    finally { if (held.IsValid) frame.Destroy(held); frame.RuntimeConfig.TerrainContours = new[] { slope }; }
                }
            }
            var tangent30 = FPMath.Tan(FP.Pi / 6);
            Require(FlipFriendsTerrain.IsPlayerWalkable(new FPVector2(1, tangent30)) &&
                !FlipFriendsTerrain.IsPlayerWalkable(new FPVector2(1, tangent30 + FP.FromRaw(1))), "플레이어30도 FP 경계");
            Require(FlipFriendsTerrain.IsWalkable(new FPVector2(1, 1)) && !FlipFriendsTerrain.IsWalkable(new FPVector2(1, FP._1 + FP.FromRaw(1))), "지형/상자45도 FP 경계");
            foreach (var tangent in new[] { tangent30, tangent30 + FP.FromRaw(1) })
            {
                frame.RuntimeConfig.TerrainContours = new[] { new FlipFriendsTerrainContour { Id = "angle30", Vertices = new[] {
                    new FPVector2(90, -30), new FPVector2(120, -30), new FPVector2(120, 20 * tangent), new FPVector2(90, -10 * tangent) } } };
                var center = new FPVector2(104, 0); var half = new FPVector2(FP.FromString("0.4"), FP.FromString("0.5"));
                Require(FlipFriendsTerrain.Support(frame.RuntimeConfig.TerrainContours, center, half, -20, 20, out var surface, out _), "30도 경계 지지면");
                var angleT = new Transform2D { Position = new FPVector2(104, surface + half.Y) };
                var angleP = new FlipFriendsPlayer { IsGrounded = true, Velocity = new FPVector2(4, 0) };
                FlipFriendsMovementSystem.Simulate(frame, ref angleT, ref angleP, new Quantum.Input { Horizontal = 1 });
                Require(tangent == tangent30 ? angleT.Position.X > 104 : angleT.Position.X < 104, "30도 정확한 등반/초과 미끄럼 전이");
            }
            frame.RuntimeConfig.TerrainContours = new[] { new FlipFriendsTerrainContour { Id = "valley", Vertices = new[] {
                new FPVector2(90, -30), new FPVector2(130, -30), new FPVector2(130, 0), new FPVector2(124, 0),
                new FPVector2(118, 3), new FPVector2(112, 0), new FPVector2(106, 3), new FPVector2(100, 0), new FPVector2(90, 0) } } };
            var valleyT = new Transform2D { Position = new FPVector2(100, FP.FromString("0.7")) };
            var valleyP = new FlipFriendsPlayer { IsGrounded = true }; bool passedValley = false, passedPeak = false;
            for (int i = 0; i < 360; i++)
            {
                FlipFriendsMovementSystem.Simulate(frame, ref valleyT, ref valleyP, new Quantum.Input { Horizontal = 1 });
                Require(valleyP.IsGrounded && !FlipFriendsTerrain.Overlaps(frame.RuntimeConfig.TerrainContours, valleyT.Position, FlipFriendsInteractionSystem.HalfSize(valleyP)), "꼭대기/골짜기 접지 비관통");
                passedValley |= valleyT.Position.X > 112 && valleyT.Position.Y < 1;
                passedPeak |= valleyT.Position.X > 118 && valleyT.Position.Y > 3;
            }
            Require(passedValley && passedPeak, "골짜기/꼭대기 실제 통과"); cases.Add("골짜기/꼭대기 연속 이동 접지·비관통");
            frame.RuntimeConfig.TerrainContours = new[] { new FlipFriendsTerrainContour { Id = "wall-slope", Vertices = new[] {
                new FPVector2(90, -30), new FPVector2(120, -30), new FPVector2(120, 12), new FPVector2(FP.FromString("104.8"), 12),
                new FPVector2(FP.FromString("104.8"), FP.FromString("2.4")), new FPVector2(100, 0), new FPVector2(90, 0) } } };
            var wallT = new Transform2D { Position = new FPVector2(104, FP.FromString("2.7")) }; var wallP = new FlipFriendsPlayer { IsGrounded = true };
            for (int i = 0; i < 120; i++)
            {
                FlipFriendsMovementSystem.Simulate(frame, ref wallT, ref wallP, new Quantum.Input { Horizontal = 1 });
                Require(!FlipFriendsTerrain.Overlaps(frame.RuntimeConfig.TerrainContours, wallT.Position, FlipFriendsInteractionSystem.HalfSize(wallP)), "벽 인접 경사 비관통");
            }
            Require(wallT.Position.X + FlipFriendsInteractionSystem.HalfSize(wallP).X <= FP.FromString("104.8"), "경사 끝 벽 정지 " + wallT.Position); cases.Add("경사 끝 벽 정지·비관통");
            frame.RuntimeConfig.TerrainContours = new[] { new FlipFriendsTerrainContour { Id = "steep45", Vertices = new[] {
                new FPVector2(90, -30), new FPVector2(120, -30), new FPVector2(120, 20), new FPVector2(90, -10) } } };
            var steepPosition = new FPVector2(104, FP.FromString("4.9"));
            var steepT = new Transform2D { Position = steepPosition }; var steepP = new FlipFriendsPlayer { IsGrounded = true };
            for (int i = 0; i < 60; i++) FlipFriendsMovementSystem.Simulate(frame, ref steepT, ref steepP, default);
            Require(steepT.Position.X < steepPosition.X && steepT.Position.Y < steepPosition.Y, "30도 초과 급경사 미끄럼");
            steepT.Position = steepPosition; steepP = new FlipFriendsPlayer { IsGrounded = true };
            FlipFriendsMovementSystem.Simulate(frame, ref steepT, ref steepP, new Quantum.Input { Jump = true });
            Require(steepP.Velocity.X < 0 && steepP.Velocity.Y > 0, "급경사 법선 점프");
            steepT.Position = steepPosition; steepP = new FlipFriendsPlayer { IsGrounded = true };
            FlipFriendsMovementSystem.Simulate(frame, ref steepT, ref steepP, new Quantum.Input { Jump = true, Horizontal = 1 });
            Require(steepP.Velocity.Y <= 0, "원본 급경사 안쪽 입력 점프 거부");
            cases.Add("30도/45도 경계·급경사 미끄럼/법선 점프/안쪽 입력 거부");
            var entity = frame.Create();
            try
            {
                frame.Add(entity, new Transform2D { Position = new FPVector2(104, 7) });
                frame.Add(entity, new FlipFriendsBox { HalfSize = new FPVector2(FP.FromString("0.45"), FP.FromString("0.45")), Velocity = new FPVector2(6, 6) });
                var boxTransform = frame.Get<Transform2D>(entity); var box = frame.Get<FlipFriendsBox>(entity);
                for (int i = 0; i < 360; i++)
                {
                    FlipFriendsBoxSystem.Simulate(frame, entity, ref boxTransform, ref box);
                    frame.Set(entity, boxTransform); frame.Set(entity, box);
                    Require(!FlipFriendsTerrain.Overlaps(frame.RuntimeConfig.TerrainContours, boxTransform.Position, box.HalfSize), "던진 상자45도 경사 비관통");
                }
                Require(box.IsGrounded && boxTransform.Position.X > 104, "상자45도 착지/경사 감속 이동");
                cases.Add("던진 상자45도 착지/경사 감속 이동");
            }
            finally { frame.Destroy(entity); }
        }
        finally { frame.RuntimeConfig.TerrainContours = original; File.WriteAllText("Logs/QuantumSlopeDirect.txt", string.Join("\n", cases)); }
    }

    private static void Require(bool value, string label)
    { if (!value) throw new InvalidOperationException("경사 검사 실패: " + label); }

    private static void CheckThrows(Frame frame, FPVector2 position, List<string> cases)
    {
        foreach (bool isPlayer in new[] { false, true })
        {
            var carrier = frame.Create(); var held = frame.Create();
            try
            {
                frame.Add(carrier, new Transform2D { Position = position });
                frame.Add(carrier, new FlipFriendsPlayer { Held = held, IsGrounded = true, Facing = 1 });
                frame.Add(held, new Transform2D { Position = position + FlipFriendsInteractionSystem.HeldOffset });
                if (isPlayer) frame.Add(held, new FlipFriendsPlayer { Carrier = carrier, Player = (PlayerRef)1 });
                else frame.Add(held, new FlipFriendsBox { Carrier = carrier, HalfSize = new FPVector2(FP.FromString("0.45"), FP.FromString("0.45")) });
                Require(FlipFriendsInteractionSystem.Release(frame, carrier, false), "경사 위 던지기");
                Require(!frame.Get<FlipFriendsPlayer>(carrier).Held.IsValid, "경사 위 운반 관계 해제");
                var t = frame.Get<Transform2D>(held);
                if (isPlayer)
                {
                    var p = frame.Get<FlipFriendsPlayer>(held);
                    Require(p.Velocity == new FPVector2(7, 12) && !p.Carrier.IsValid, "경사 플레이어 던지기 속도/관계");
                    for (int i = 0; i < 180; i++)
                    {
                        FlipFriendsMovementSystem.Simulate(frame, ref t, ref p, default); frame.Set(held, t); frame.Set(held, p);
                        Require(!FlipFriendsTerrain.Overlaps(frame.RuntimeConfig.TerrainContours, t.Position + FlipFriendsInteractionSystem.Offset(p), FlipFriendsInteractionSystem.HalfSize(p)), "경사에서 던진 플레이어 비관통");
                    }
                }
                else
                {
                    var box = frame.Get<FlipFriendsBox>(held);
                    Require(box.Velocity == new FPVector2(6, 6) && !box.Carrier.IsValid, "경사 상자 던지기 속도/관계");
                    for (int i = 0; i < 180; i++)
                    {
                        FlipFriendsBoxSystem.Simulate(frame, held, ref t, ref box); frame.Set(held, t); frame.Set(held, box);
                        Require(!FlipFriendsTerrain.Overlaps(frame.RuntimeConfig.TerrainContours, t.Position, box.HalfSize), "경사에서 던진 상자 비관통");
                    }
                }
                cases.Add("경사 위 실제 Release·운반 해제·던진 " + (isPlayer ? "플레이어" : "상자") + " 궤적 비관통");
            }
            finally { frame.Destroy(held); frame.Destroy(carrier); }
        }
    }
}
