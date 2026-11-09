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
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Button = UnityEngine.UI.Button;
using LogType = UnityEngine.LogType;

public static class QuantumLauncherChecks
{
    public static string Status { get; private set; } = "미실행";
    public static string IntegrationStatus { get; private set; } = "미실행";
    private const int Horizon = 2400;

    public static RuntimeConfig Config()
    {
        var config = new RuntimeConfig();
        QuantumGameContent.Prepare(AssetDatabase.LoadAssetAtPath<QuantumOfficialStageAsset>(QuantumLauncherSetup.StagePath), config);
        return config;
    }

    public static void RunContent()
    {
        var read = QuantumOfficialStageWriter.ReadPrefab(QuantumLauncherSetup.PrefabPath);
        var terrain = ScriptableObject.CreateInstance<QuantumMapTerrain>();
        var lines = new List<string>();
        try
        {
            terrain.Configure(read.Terrain, read.TerrainBounds, read.TerrainSignature);
            var asset = AssetDatabase.LoadAssetAtPath<QuantumOfficialStageAsset>(QuantumLauncherSetup.StagePath);
            var converted = QuantumOfficialStageConverter.Convert(read.Data, terrain, null);
            Require(asset.ContentHash == converted.ContentHash && converted.Launchers.Count == 2, "프리팹 재로드 해시·장치2개");
            Require(converted.Launchers.Single(l => l.Kind == 0).Radius == 1 && converted.Launchers.Single(l => l.Kind == 1).Vertices.Length == 4, "원본 원형 바운스/Box 스프링");
            read.Data.launchers = read.Data.launchers.Reverse().ToArray();
            Require(QuantumOfficialStageConverter.Convert(read.Data, terrain, null).ContentHash == asset.ContentHash, "ID 순 독립 해시");
            Require(Config().CustomMapAgreement.Contains(":제품15:") && QuantumGameContent.NetworkVersion == "ff-product-15", "제품15 합의");
            lines.Add("프리팹 재로드·원형/Box·ID순 독립 해시·제품15 합의");
            var first = read.Data.launchers[0];
            var original = (first.kind, first.radius, first.center, first.origin);
            foreach (var field in new[] { "종류", "반지름", "중심", "기준" })
            {
                if (field == "종류") first.kind = 2;
                if (field == "반지름") first.radius = float.NaN;
                if (field == "중심") first.center = new Vector2(float.PositiveInfinity, 0);
                if (field == "기준") first.origin = new Vector2(float.NaN, 0);
                RejectConversion(() => QuantumOfficialStageConverter.Convert(read.Data, terrain, null), field);
                (first.kind, first.radius, first.center, first.origin) = original; lines.Add("변환 거부 " + field);
            }
            first.kind = 1 - first.kind;
            Require(QuantumOfficialStageConverter.Convert(read.Data, terrain, null).ContentHash != asset.ContentHash, "종류 변경 해시");
            first.kind = original.kind;
            first.origin += Vector2.right;
            Require(QuantumOfficialStageConverter.Convert(read.Data, terrain, null).ContentHash != asset.ContentHash, "방향 기준점 변경 해시");
            first.origin = original.origin;
            var bounce = read.Data.launchers.Single(l => l.radius > 0);
            bounce.center += Vector2.right;
            Require(QuantumOfficialStageConverter.Convert(read.Data, terrain, null).ContentHash != asset.ContentHash, "Collider 오프셋 변경 해시");
            lines.Add("종류·방향 원점·판정 오프셋 해시 포함");
            foreach (var kind in new[] { "고체", "다중", "회전", "물리", "오목", "효과", "혼합", "표시", "원본태그", "캡슐" }) RejectAuthoring(kind, lines);
            File.WriteAllText("Logs/QuantumLauncherContent.txt", "통과\n" + string.Join("\n", lines));
        }
        finally { UnityEngine.Object.DestroyImmediate(terrain); }
    }

    private static void RejectConversion(Action action, string label)
    {
        bool rejected = false;
        try { action(); } catch (InvalidOperationException) { rejected = true; }
        Require(rejected, label + " 거부");
    }

    private static void RejectAuthoring(string kind, List<string> lines)
    {
        var previous = SceneManager.GetActiveScene();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(scene);
        var terrain = ScriptableObject.CreateInstance<QuantumMapTerrain>();
        try
        {
            var root = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(QuantumLauncherSetup.PrefabPath), scene);
            PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            var target = root.GetComponentsInChildren<QuantumStageLauncher>().Single(l => l.Kind == QuantumLauncherKind.Spring);
            var box = target.GetComponent<BoxCollider2D>();
            if (kind == "고체") box.isTrigger = false;
            if (kind == "다중") target.gameObject.AddComponent<CircleCollider2D>().isTrigger = true;
            if (kind == "회전") target.transform.eulerAngles = new Vector3(0, 0, 30);
            if (kind == "물리") target.gameObject.AddComponent<Rigidbody2D>();
            if (kind == "오목")
            {
                UnityEngine.Object.DestroyImmediate(box);
                var polygon = target.gameObject.AddComponent<PolygonCollider2D>(); polygon.isTrigger = true;
                polygon.SetPath(0, new[] { new Vector2(-1, -1), new Vector2(1, -1), Vector2.zero, new Vector2(1, 1), new Vector2(-1, 1) });
            }
            if (kind == "효과") box.usedByEffector = true;
            if (kind == "혼합") target.gameObject.AddComponent<QuantumStageTrap>();
            if (kind == "표시") UnityEngine.Object.DestroyImmediate(target.GetComponent<SpriteRenderer>());
            if (kind == "원본태그") { target.gameObject.tag = "Spring"; UnityEngine.Object.DestroyImmediate(target); }
            if (kind == "캡슐") { var g = target.gameObject; UnityEngine.Object.DestroyImmediate(box); g.AddComponent<CapsuleCollider2D>().isTrigger = true; }
            Physics2D.SyncTransforms();
            RejectConversion(() => { var read = QuantumOfficialStageWriter.Read(root); terrain.Configure(read.Terrain, read.TerrainBounds, read.TerrainSignature); QuantumOfficialStageConverter.Convert(read.Data, terrain, null); }, kind);
            lines.Add("작성기 거부 " + kind);
        }
        finally { UnityEngine.Object.DestroyImmediate(terrain); SceneManager.SetActiveScene(previous); EditorSceneManager.CloseScene(scene, true); }
    }

    public static Quantum.Input InputFor(Frame frame, int tick, int player)
    {
        if (frame == null || tick < 120) return default;
        var q = frame.Filter<Transform2D, FlipFriendsPlayer>();
        while (q.Next(out _, out var t, out var p))
        {
            if ((int)p.Player != player || p.IsFinished) continue;
            if (player == 1 && p.PickupCount == 0)
            {
                var gap = FP.FromString("-3.12") - t.Position.X;
                if (FPMath.Abs(gap) > FP.FromString("0.05")) return new Quantum.Input { Horizontal = FPMath.Clamp(gap * 4 - p.Velocity.X, -FP._1, FP._1) };
                if (p.Facing != -1) return new Quantum.Input { Horizontal = -FP.FromString("0.01") };
                return new Quantum.Input { Interact = tick % 20 < 10 };
            }
            if (p.LaunchCount == 0)
            {
                FP target = player == 1 ? 4 : -6;
                var gap = target - t.Position.X;
                return new Quantum.Input { Horizontal = FPMath.Clamp(gap * 4 - p.Velocity.X, -FP._1, FP._1),
                    Jump = player == 1 && (t.Position.X > FP.FromString("1.4") || !p.IsGrounded) };
            }
            var goal = FP.FromString("-1") - t.Position.X;
            return new Quantum.Input { Horizontal = FPMath.Clamp(goal * 4 - p.Velocity.X * 2, -FP._1, FP._1),
                Vertical = FPMath.Abs(goal) < FP.FromString("0.4") ? 1 : 0,
                Jump = p.IsGrounded && FPMath.Abs(goal) > 2 && tick % 90 < 20 };
        }
        return default;
    }

    public static async void Run()
    {
        int errors = 0;
        Application.LogCallback log = (_, _, type) => { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors++; };
        Application.logMessageReceived += log;
        try { Status = "검사 중"; var summary = await Replay(); Require(errors == 0, "Unity 오류0"); Status = "통과: " + summary; }
        catch (Exception error) { Status = "실패: " + error; Debug.LogException(error); }
        finally { Application.logMessageReceived -= log; File.WriteAllText("Logs/QuantumLauncherChecks.txt", Status); }
    }

    private static async Task<string> Replay()
    {
        var session = new QuantumGameSession(); QuantumRunner replay = null;
        var expected = new Dictionary<int, ulong>(); var actual = new Dictionary<int, ulong>();
        var launches = new int[2]; var trace = new List<string>(); bool carried = false; int completed = 0;
        session.InputSource = (tick, slot) => InputFor(session.Runner?.Game.Frames.Verified, tick, slot);
        try
        {
            using (QuantumCallback.SubscribeManual<CallbackSimulateFinished>(c =>
            {
                if (!c.Frame.IsVerified || c.Frame.Number > Horizon) return;
                if (c.Game == session.Runner?.Game)
                {
                    var q = c.Frame.Filter<Transform2D, FlipFriendsPlayer>();
                    while (q.Next(out _, out var t, out var p))
                    {
                        int i = (int)p.Player;
                        Require(!FlipFriendsTerrain.Overlaps(c.Frame.RuntimeConfig.TerrainContours, t.Position + FlipFriendsInteractionSystem.Offset(p), FlipFriendsInteractionSystem.HalfSize(p)), "발사 뒤 지형 비관통");
                        if (p.LaunchCount != launches[i])
                        {
                            Require(p.LaunchFrame == c.Frame.Number && p.LaunchCount > launches[i], "발사 프레임/횟수");
                            var config = c.Frame.RuntimeConfig.Launchers.Last(l => FlipFriendsLauncherSystem.Overlaps(l, t.Position + FlipFriendsInteractionSystem.Offset(p), FlipFriendsInteractionSystem.HalfSize(p)));
                            var away = t.Position - config.Origin;
                            var velocity = away == default ? default : away.Normalized * (config.Kind == 0 ? 20 : 50);
                            Require(p.Velocity == velocity, "방향×20/50 속도 덮어쓰기");
                            Require(i == 1 ? config.Kind == 0 : config.Kind == 1, "플레이어별 바운스/스프링 진입");
                            if (i == 1) carried |= p.Held.IsValid;
                            trace.Add($"진입{c.Frame.Number} P{i} 위치={t.Position} 속도={p.Velocity} 종류={p.LaunchKind} 운반={p.Held.IsValid}");
                            launches[i] = p.LaunchCount;
                        }
                        if (c.Frame.Number % 120 == 0) trace.Add($"{c.Frame.Number} P{i} 위치={t.Position} 속도={p.Velocity} 발사={p.LaunchCount} 도착={p.IsFinished}");
                    }
                    if (c.Frame.FlipFriendsCompletedFrame > 0) completed = c.Frame.FlipFriendsCompletedFrame;
                    if (c.Frame.Number % 120 == 0) expected[c.Frame.Number] = c.Frame.CalculateChecksum();
                }
                if (c.Game == replay?.Game && c.Frame.Number % 120 == 0) actual[c.Frame.Number] = c.Frame.CalculateChecksum();
            }))
            {
                await session.StartLocalAsync(Config()); session.Runner.IsSessionUpdateDisabled = true;
                for (int i = 0; i < Horizon + 200 && !expected.ContainsKey(Horizon); i++) session.Runner.Service(1f / 60);
                File.WriteAllText("Logs/QuantumLauncherTrace.txt", string.Join("\n", trace) + "\n최종=" + Describe(session) + " 러너=" + session.Runner.State + " 일시정지=" + session.Runner.Session.IsPaused);
                Require(launches.All(n => n > 0) && carried && completed > 0, $"실제 바운스/스프링·상자 운반·완료 {string.Join(",", launches)} 운반={carried} 완료={completed}");
                var record = session.Runner.Game.GetRecordedReplay(includeChecksums: true); var serializer = new QuantumUnityJsonSerializer();
                replay = QuantumRunner.StartGame(new SessionRunner.Arguments { RunnerId = "FF_LAUNCHER_REPLAY", RuntimeConfig = serializer.ConfigFromByteArray<RuntimeConfig>(record.RuntimeConfigData.Decode(), compressed: true),
                    SessionConfig = record.DeterministicConfig, ReplayProvider = record.CreateInputProvider(), InitialTick = record.InitialTick, FrameData = record.InitialFrameData, GameMode = DeterministicGameMode.Replay, PlayerCount = 2 });
                replay.IsSessionUpdateDisabled = true;
                for (int i = 0; i < Horizon * 2 && !actual.ContainsKey(Horizon); i++) replay.Service(i % 2 == 0 ? 1f / 30 : 1f / 120);
                Require(expected.Count == Horizon / 120 && expected.Count == actual.Count && expected.All(p => actual[p.Key] == p.Value), "입력 기록/별도 러너 재생 체크섬 일치");
                File.WriteAllText("Logs/QuantumLauncherReplay.json", JsonUtility.ToJson(record));
                File.WriteAllText("Logs/QuantumLauncherChecksums.txt", string.Join("\n", expected.Select(p => p.Key + ": " + p.Value)));
                int direct = Direct(session.Runner.Game.Frames.Verified);
                return $"입력 기록/별도 러너 재생{expected.Count}개 일치·바운스/스프링={string.Join(",", launches)}·상자 운반 유지·완료={completed}·직접{direct}종·확정{Horizon}={expected[Horizon]}";
            }
        }
        finally { if (replay != null) await replay.ShutdownAsync(); await session.StopAsync(); }
    }

    private static int Direct(Frame frame)
    {
        var config = frame.RuntimeConfig; var saved = config.Launchers; int completed = frame.FlipFriendsCompletedFrame;
        var created = new List<EntityRef>(); var lines = new List<string>();
        EntityRef Player(FPVector2 pos, FlipFriendsPlayer state)
        { var e = frame.Create(); created.Add(e); frame.Add(e, new Transform2D { Position = pos }); frame.Add(e, state); return e; }
        FlipFriendsPlayer P(EntityRef e) => frame.Get<FlipFriendsPlayer>(e);
        void Move(EntityRef e, FPVector2 pos) => frame.Set(e, new Transform2D { Position = pos });
        var launcher = new FlipFriendsLauncherConfig { Id = "direct", Origin = new FPVector2(104, 1), Center = new FPVector2(104, 1), Radius = 1 };
        var system = new FlipFriendsLauncherSystem();
        try
        {
            frame.FlipFriendsCompletedFrame = 0;
            // 별도 장치를 써서 검사 전 콘텐츠의 접촉 이력과 섞이지 않게 한다.
            int testIndex = saved.Length;
            config.Launchers = saved.Append(launcher).ToArray();
            var device = frame.Create(); created.Add(device); frame.Add(device, new FlipFriendsLauncher { ContentIndex = testIndex });
            var player = Player(new FPVector2(104, 2), new FlipFriendsPlayer { Player = 0, Velocity = new FPVector2(7, -3), VelocityXSmoothing = FP._0_50,
                IsClimbed = true, ClimbBlockTicks = 7, UncontrollableTicks = 8, InvincibleTicks = 9 });
            system.Update(frame); var first = P(player);
            Require(first.Velocity == new FPVector2(0, 20) && first.LaunchCount == 1 && first.IsClimbed && first.ClimbBlockTicks == 7 && first.UncontrollableTicks == 8 && first.InvincibleTicks == 9 && first.VelocityXSmoothing == FP._0_50, "바운스20·로프/무적/타이머/스무딩 유지");
            system.Update(frame); Require(P(player).LaunchCount == 1, "지속 접촉·동일 틱 재실행 없음");
            Move(player, new FPVector2(110, 2)); system.Update(frame); Move(player, new FPVector2(104, 2)); system.Update(frame);
            Require(P(player).LaunchCount == 2, "이탈 뒤 재진입 재발동"); lines.Add("바운스20·진입1회·이탈 재진입·로프/타이머/스무딩 유지");
            launcher.Kind = 1; config.Launchers[testIndex] = launcher;
            Move(player, new FPVector2(110, 2)); system.Update(frame); Move(player, new FPVector2(FP.FromString("104.5"), 2)); system.Update(frame);
            var away = frame.Get<Transform2D>(player).Position - launcher.Origin;
            Require(P(player).Velocity == away.Normalized * 50 && P(player).Velocity.X > 0 && P(player).Velocity.Y > 0, "스프링50 대각 정규화");
            lines.Add("스프링50·대각 위치 기반 방향");
            Require(!FlipFriendsLauncherSystem.Overlaps(launcher, new FPVector2(FP.FromString("105.4"), 1), new FPVector2(FP.FromString("0.4"), FP._0_50)), "원형 경계 비접촉");
            Require(!FlipFriendsLauncherSystem.Overlaps(launcher, new FPVector2(FP.FromString("105.3"), FP.FromString("1.95")), new FPVector2(FP.FromString("0.4"), FP._0_50)), "원 밖 모서리 비접촉");
            var value = P(player); FlipFriendsLauncherSystem.Apply(frame, launcher.Origin, ref value, launcher); Require(value.Velocity == default, "기준점0 방향");
            lines.Add("원형 모서리/경계·기준점 일치0");
            // 원본 MovementHandler 콜백에는 isCarried 검사가 없다. 들린 플레이어 속도는 바뀌어도 관계/위치는 바꾸지 않는다.
            var holder = Player(new FPVector2(104, FP._0_50), new FlipFriendsPlayer { Player = 1, Held = player });
            var carried = P(player); carried.Carrier = holder; carried.LaunchCount = 0; frame.Set(player, carried);
            var ds = frame.Get<FlipFriendsLauncher>(device); ds.PlayerContacts = 0; frame.Set(device, ds); system.Update(frame);
            Require(P(player).Carrier == holder && P(holder).Held == player && P(player).LaunchCount == 1, "들린 플레이어 콜백·관계 유지");
            Require(P(holder).LaunchCount == 1 && P(holder).Held == player, "운반자 진입·운반 유지"); lines.Add("운반자/들린 플레이어 진입·관계 유지");
            foreach (var mode in new[] { "복귀대기", "복귀틱", "도착" })
            {
                var state = new FlipFriendsPlayer { Player = 0, RespawnPending = mode == "복귀대기", RespawnFrame = mode == "복귀틱" ? frame.Number : 0, IsFinished = mode == "도착" };
                frame.Set(player, state); ds.PlayerContacts = 0; frame.Set(device, ds); system.Update(frame);
                Require(P(player).LaunchCount == 0, mode + " 제외");
            }
            lines.Add("복귀대기/복귀틱/도착 제외");
            frame.Set(player, new FlipFriendsPlayer { Player = 0, HomePosition = new FPVector2(103, 2), LaunchCount = 4, LaunchFrame = frame.Number, LaunchKind = 1 });
            FlipFriendsRespawnSystem.ClearPlayer(frame, player, new FPVector2(103, 2), false);
            Require(P(player).LaunchCount == 4 && P(player).LaunchFrame == 0 && P(player).Velocity == default, "복귀 상태 초기화·카운터 유지");
            lines.Add("복귀 초기화·관측 횟수 유지");
            // 다중 진입은 ID순 마지막 속도가 남고 모든 진입 이력은 소비한다.
            config.Launchers = saved.Append(launcher).Append(new FlipFriendsLauncherConfig { Id = "last", Kind = 0, Origin = new FPVector2(103, 2), Center = launcher.Center, Radius = 1 }).ToArray();
            ds.PlayerContacts = 0; frame.Set(device, ds);
            var lastDevice = frame.Create(); created.Add(lastDevice); frame.Add(lastDevice, new FlipFriendsLauncher { ContentIndex = testIndex + 1 });
            frame.Set(player, new FlipFriendsPlayer { Player = 0 }); Move(player, new FPVector2(104, 2)); system.Update(frame);
            Require(P(player).LaunchCount == 2 && P(player).Velocity == new FPVector2(20, 0), "동일 틱 다중 진입 마지막 덮어쓰기");
            system.Update(frame); Require(P(player).LaunchCount == 2, "모든 진입 소비"); lines.Add("다중 진입 ID순 마지막·재발동 없음");
            var box = frame.Create(); created.Add(box); frame.Add(box, new Transform2D { Position = launcher.Center }); frame.Add(box, new FlipFriendsBox { Velocity = new FPVector2(3, 4) });
            system.Update(frame); Require(frame.Get<FlipFriendsBox>(box).Velocity == new FPVector2(3, 4), "상자 비대상"); lines.Add("상자 비대상");
        }
        finally
        {
            foreach (var e in created) if (frame.Exists(e)) frame.Destroy(e);
            config.Launchers = saved; frame.FlipFriendsCompletedFrame = completed;
            File.WriteAllText("Logs/QuantumLauncherDirect.txt", string.Join("\n", lines));
        }
        return lines.Count;
    }

    public static async void RunMain()
    {
        try
        {
            IntegrationStatus = "Main 검사 중";
            var flow = QuantumGameFlow.Instance;
            var stage = AssetDatabase.LoadAssetAtPath<QuantumOfficialStageAsset>(QuantumLauncherSetup.StagePath);
            void Select()
            {
                Click("Start Button");
                for (int i = 0; i < flow.OfficialStages.Count; i++)
                { Click("공식 스테이지 선택"); if (flow.SelectedContent == (IQuantumMapContent)stage) return; }
                Require(false, "Main 발사 시험 목록 선택");
            }
            Select(); Click("오프라인 시작"); await Wait(() => flow.State == QuantumGameState.Playing, 10000);
            var session = flow.Session; int launches = 0;
            session.InputSource = (tick, slot) =>
            {
                var verified = session.Runner?.Game.Frames.Verified;
                if (verified != null) { int sum = 0; var q = verified.Filter<FlipFriendsPlayer>(); while (q.Next(out _, out var p)) sum += p.LaunchCount; launches = Math.Max(launches, sum); }
                return InputFor(verified, tick, slot);
            };
            await Wait(() => flow.State == QuantumGameState.Ready && SceneManager.GetActiveScene().name == "Main", 90000);
            Require(launches >= 2 && !QuantumRunner.ActiveRunners.Any() && QuantumGameSession.ActiveInputSubscriptions == 0, "발사 시험 완료·발사 관측·정리");
            Select(); Click("오프라인 시작"); await Wait(() => flow.State == QuantumGameState.Playing, 10000);
            Click("플레이 종료"); await Wait(() => flow.State == QuantumGameState.Ready, 10000);
            Require(!QuantumRunner.ActiveRunners.Any() && QuantumGameSession.ActiveInputSubscriptions == 0, "발사 시험 재진입/메뉴 정리");
            IntegrationStatus = $"통과: Main 실제 버튼 발사 시험 선택·플레이(발사{launches})·전원 완료·복귀·재진입·메뉴 종료·러너/입력0";
        }
        catch (Exception error) { IntegrationStatus = "실패: " + error; Debug.LogException(error); }
        finally { File.WriteAllText("Logs/QuantumLauncherMain.txt", IntegrationStatus); }
    }

    public static async void RunCloud()
    {
        var sessions = new[] { new QuantumGameSession(), new QuantumGameSession() };
        var checks = new[] { new Dictionary<int, ulong>(), new Dictionary<int, ulong>() };
        var launches = new[] { new Dictionary<int, string>(), new Dictionary<int, string>() };
        try
        {
            IntegrationStatus = "Photon 검사 중";
            foreach (var session in sessions)
                session.InputSource = (tick, slot) => { var players = session.Runner?.Game.GetLocalPlayers(); return players == null || slot >= players.Count ? default : InputFor(session.Runner.Game.Frames.Verified, tick, (int)players[slot]); };
            using (QuantumCallback.SubscribeManual<CallbackSimulateFinished>(c =>
            {
                if (!c.Frame.IsVerified) return;
                for (int i = 0; i < 2; i++) if (c.Game == sessions[i].Runner?.Game && c.Frame.Number <= Horizon && c.Frame.Number % 120 == 0)
                {
                    checks[i][c.Frame.Number] = c.Frame.CalculateChecksum();
                    var q = c.Frame.Filter<FlipFriendsPlayer>(); var parts = new List<string>();
                    while (q.Next(out _, out var p)) parts.Add($"P{(int)p.Player}:{p.LaunchCount}/{p.LaunchFrame}/{p.LaunchKind}/{p.Held.IsValid}");
                    launches[i][c.Frame.Number] = string.Join(" ", parts.OrderBy(s => s, StringComparer.Ordinal));
                }
            }))
            {
                string room = "ff-launcher-" + Guid.NewGuid().ToString("N");
                var first = sessions[0].StartDevelopmentAsync(Config(), room, false);
                await Wait(() => sessions[0].Client?.InRoom == true || first.IsFaulted, 15000); if (first.IsFaulted) await first;
                await Task.WhenAll(first, sessions[1].StartDevelopmentAsync(Config(), room, true));
                try { await Wait(() => sessions.All(s => s.Runner.Game.Frames.Verified.FlipFriendsCompletedFrame > 0) && checks.All(c => c.ContainsKey(Horizon)), 120000); }
                catch (InvalidOperationException) { throw new InvalidOperationException("발사 Photon 완료 대기 초과: " + string.Join(" | ", sessions.Select(Describe))); }
                var samples = checks[0].Where(p => p.Key >= 240).ToArray();
                Require(samples.Length == Horizon / 120 - 1 && samples.All(p => checks[1].TryGetValue(p.Key, out var h) && p.Value == h), $"발사 Photon 확정{samples.Length}개");
                Require(launches[0].All(p => launches[1][p.Key] == p.Value) && !launches[0][Horizon].Contains("P0:0/") && !launches[0][Horizon].Contains("P1:0/"), "발사 상태 양쪽 일치");
                int completed = sessions[0].Runner.Game.Frames.Verified.FlipFriendsCompletedFrame;
                Require(completed == sessions[1].Runner.Game.Frames.Verified.FlipFriendsCompletedFrame, "발사 Photon 완료 프레임");
                IntegrationStatus = $"통과: 발사 Photon 동일PC 두 연결·지역={sessions[0].Client.CurrentRegion}·RTT={sessions[0].Client.RealtimePeer.Stats.RoundtripTime}/{sessions[1].Client.RealtimePeer.Stats.RoundtripTime}ms·완료={completed}·확정{samples.Length}개·발사 상태 일치({launches[0][Horizon]})·지연/손실 주입 없음\n" + string.Join("\n", samples.Select(p => p.Key + ": " + p.Value));
            }
        }
        catch (Exception error) { IntegrationStatus = "실패: " + error; Debug.LogException(error); }
        finally
        {
            foreach (var session in sessions) await session.StopAsync();
            IntegrationStatus += "\n러너/연결/입력 정리=" + sessions.All(s => s.Runner == null && s.Client == null && !s.HasInputSubscription);
            File.WriteAllText("Logs/QuantumLauncherCloud.txt", IntegrationStatus);
        }
    }

    private static string Describe(QuantumGameSession session)
    {
        var frame = session.Runner?.Game.Frames.Verified;
        if (frame == null) return "러너 없음";
        var parts = new List<string> { "확정" + frame.Number };
        var q = frame.Filter<Transform2D, FlipFriendsPlayer>();
        while (q.Next(out _, out var t, out var p)) parts.Add($"P{(int)p.Player} {t.Position} 발사={p.LaunchCount} 들기={p.Held.IsValid} 복귀={p.RespawnCount} 도착={p.IsFinished}");
        return string.Join(" ", parts);
    }

    private static void Click(string name) => UnityEngine.Object.FindObjectsByType<Button>(FindObjectsInactive.Exclude).Single(b => b.name == name).onClick.Invoke();
    private static async Task Wait(Func<bool> ready, int timeout)
    { var end = DateTime.UtcNow.AddMilliseconds(timeout); while (!ready() && DateTime.UtcNow < end && EditorApplication.isPlaying) await Task.Delay(20); Require(ready(), "비동기 제한시간"); }

    private static void Require(bool value, string label) { if (!value) throw new InvalidOperationException("스프링/바운스 검사 실패: " + label); }
}
