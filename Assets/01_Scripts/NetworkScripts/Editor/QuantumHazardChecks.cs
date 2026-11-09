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

// 피격·넉백 묶음 검사. 실제 입력 기록/별도 러너 재생, 같은 프레임 API의 규칙 직접 검사, 작성기 거부, Main·Photon 경로를 확인한다.
public static class QuantumHazardChecks
{
    public static string Status { get; private set; } = "미실행";
    public static string IntegrationStatus { get; private set; } = "미실행";
    private const int Horizon = 3000;

    public static RuntimeConfig Config()
    {
        var config = new RuntimeConfig();
        QuantumGameContent.Prepare(AssetDatabase.LoadAssetAtPath<QuantumOfficialStageAsset>(QuantumHazardSetup.StagePath), config);
        return config;
    }

    public static void RunContent()
    {
        var lines = new List<string>();
        var asset = AssetDatabase.LoadAssetAtPath<QuantumOfficialStageAsset>(QuantumHazardSetup.StagePath);
        var read = QuantumOfficialStageWriter.ReadPrefab(QuantumHazardSetup.PrefabPath);
        var terrain = ScriptableObject.CreateInstance<QuantumMapTerrain>();
        try
        {
            terrain.Configure(read.Terrain, read.TerrainBounds, read.TerrainSignature);
            var conversion = QuantumOfficialStageConverter.Convert(read.Data, terrain, null);
            Require(conversion.ContentHash == asset.ContentHash, "피격 프리팹 재로드 해시");
            Require(conversion.Traps.Count == 3 && conversion.Traps.Select(t => t.Id).SequenceEqual(conversion.Traps.Select(t => t.Id).OrderBy(i => i, StringComparer.Ordinal)), "트랩3개 ID 순 정렬");
            var spike = conversion.Traps.Single(t => t.Direction == default);
            var directed = conversion.Traps.Single(t => t.Id.EndsWith("/방향 트랩"));
            var wall = conversion.Traps.Single(t => t.Id.EndsWith("/벽 트랩"));
            Require(spike.Vertices.Length == 3 && directed.Vertices.Length == 4 && directed.Direction == new FPVector2(-1, 0) && wall.Direction == new FPVector2(1, 0), "원본 가시 삼각·지정 방향 사각·벽 트랩");
            Require(Config().CustomMapAgreement.StartsWith(asset.Agreement + ":제품14:") && QuantumGameContent.NetworkVersion == "ff-product-14", "공식1/제품14 합의");
            // 같은 원본 배열을 역순으로 줘도 변환은 ID 순이다. 동일 틱 다중 접촉의 우선순위가 작성 순서와 무관해야 한다.
            read.Data.traps = read.Data.traps.Reverse().ToArray();
            Require(QuantumOfficialStageConverter.Convert(read.Data, terrain, null).ContentHash == asset.ContentHash, "트랩 입력 순서 독립 해시");
            lines.Add("재로드 해시·ID 순·입력 순서 독립·제품14 합의");

            Reject(root => Trap(root, "원").AddComponent<CircleCollider2D>().isTrigger = true, "CircleCollider2D", lines);
            Reject(root => { var c = Trap(root, "오목").AddComponent<PolygonCollider2D>(); c.isTrigger = true;
                c.SetPath(0, new[] { new Vector2(-0.5f, -0.5f), new Vector2(0.5f, -0.5f), new Vector2(0, 0), new Vector2(0.5f, 0.5f), new Vector2(-0.5f, 0.5f) }); }, "오목하거나", lines);
            Reject(root => Trap(root, "고체").AddComponent<BoxCollider2D>(), "활성 트리거", lines);
            Reject(root => { var g = Trap(root, "둘"); g.AddComponent<BoxCollider2D>().isTrigger = true; g.AddComponent<PolygonCollider2D>().isTrigger = true; }, "정확히 하나", lines);
            Reject(root => { var c = Trap(root, "다중").AddComponent<PolygonCollider2D>(); c.isTrigger = true; c.pathCount = 2;
                c.SetPath(1, new[] { new Vector2(2, 0), new Vector2(3, 0), new Vector2(2, 1) }); }, "경로 하나만", lines);
            Reject(root => { var b = Trap(root, "둥근").AddComponent<BoxCollider2D>(); b.isTrigger = true; b.edgeRadius = 0.1f; }, "edgeRadius", lines);
            Reject(root => { var g = Trap(root, "회전"); g.AddComponent<BoxCollider2D>().isTrigger = true; g.transform.rotation = Quaternion.Euler(0, 0, 30); }, "회전0", lines);
            Reject(root => { var g = Trap(root, "물리"); g.AddComponent<BoxCollider2D>().isTrigger = true; g.AddComponent<Rigidbody2D>(); }, "Rigidbody2D", lines);
            Reject(root => { var g = Trap(root, "표시 없음"); g.AddComponent<BoxCollider2D>().isTrigger = true; UnityEngine.Object.DestroyImmediate(g.GetComponent<SpriteRenderer>()); }, "SpriteRenderer", lines);
            // 원본처럼 Trap 태그 Collider만 두고 작성 컴포넌트가 없으면 지원 판정으로 보지 않는다.
            Reject(root => { var g = new GameObject("원본 태그 가시"); g.transform.SetParent(root.transform); g.tag = "Trap";
                g.AddComponent<SpriteRenderer>().sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/02_Sprites/Obstacle/Thron1 16x16.png");
                g.AddComponent<PolygonCollider2D>().isTrigger = true; }, "지원하지 않는 판정", lines);
            var source = read.Data.traps[0];
            foreach (var (vertices, reason) in new[] { (Enumerable.Range(0, 9).Select(i => new Vector2(Mathf.Cos(i * 0.6f), Mathf.Sin(i * 0.6f))).ToArray(), "3~8"),
                (new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(2, 0), new Vector2(1, 1) }, "일직선"), (new[] { new Vector2(0, 0), new Vector2(1, 1), new Vector2(2, 2) }, "넓이가 0") })
            {
                var copy = read.Data.traps.Select(t => t == source ? new QuantumTrapSource { id = t.id, origin = t.origin, direction = t.direction, vertices = vertices,
                    renderCenter = t.renderCenter, renderSize = t.renderSize, sprite = t.sprite } : t).ToArray();
                var data = new QuantumOfficialStageData { stageId = read.Data.stageId, spawns = read.Data.spawns, boxes = read.Data.boxes, goals = read.Data.goals,
                    ropes = read.Data.ropes, renderBounds = read.Data.renderBounds, traps = copy };
                bool rejected = false;
                try { QuantumOfficialStageConverter.Convert(data, terrain, null); }
                catch (InvalidOperationException error) { Require(error.Message.Contains(reason) && error.Message.Contains("위치=") && error.Message.Contains(source.id), "변환 거부 원인 " + reason + ": " + error.Message); rejected = true; }
                Require(rejected, "변환 거부 " + reason); lines.Add("변환 거부: " + reason);
            }
            File.WriteAllText("Logs/QuantumHazardContent.txt", "통과\n" + string.Join("\n", lines));
        }
        finally { UnityEngine.Object.DestroyImmediate(terrain); }
    }

    private static GameObject Trap(GameObject root, string name)
    {
        var trap = new GameObject("거부 " + name); trap.transform.SetParent(root.transform); trap.transform.position = new Vector3(0, 3);
        trap.AddComponent<SpriteRenderer>().sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/02_Sprites/Obstacle/Thron1 16x16.png");
        trap.AddComponent<QuantumStageTrap>();
        return trap;
    }

    private static void Reject(Action<GameObject> mutate, string reason, List<string> lines)
    {
        var previous = SceneManager.GetActiveScene();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(scene);
        try
        {
            var root = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(QuantumHazardSetup.PrefabPath), scene);
            PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            mutate(root); Physics2D.SyncTransforms();
            bool rejected = false;
            var terrain = ScriptableObject.CreateInstance<QuantumMapTerrain>();
            // 변환 메뉴와 같이 읽기와 FP 변환을 모두 거친다. 볼록성은 양자화 뒤 변환 단계에서 확인한다.
            try { var read = QuantumOfficialStageWriter.Read(root); terrain.Configure(read.Terrain, read.TerrainBounds, read.TerrainSignature); QuantumOfficialStageConverter.Convert(read.Data, terrain, null); }
            catch (InvalidOperationException error)
            {
                Require(error.Message.Contains(reason) && error.Message.Contains(QuantumHazardSetup.StageId + "/"), "작성기 거부 원인/위치 " + reason + ": " + error.Message);
                rejected = true; lines.Add("작성기 거부: " + error.Message);
            }
            finally { UnityEngine.Object.DestroyImmediate(terrain); }
            Require(rejected, "작성기 거부 " + reason);
        }
        finally { SceneManager.SetActiveScene(previous); EditorSceneManager.CloseScene(scene, true); }
    }

    // 확정 상태를 읽어 다음 입력을 만든다. 1P는 상자를 든 채 공중 지정 트랩 아래에서 점프해 맞고, 내려놓인 상자를 다시 들어 도착한다.
    // 2P는 가시에 걷기·달리기 점프로 반복 접촉한 뒤 도착한다. 입력은 기록되므로 재생은 이 함수에 의존하지 않는다.
    public static Quantum.Input InputFor(Frame frame, int tick, int player)
    {
        if (frame == null || tick < 120) return default;
        var query = frame.Filter<Transform2D, FlipFriendsPlayer>();
        while (query.Next(out _, out var t, out var p))
        {
            if ((int)p.Player != player) continue;
            if (p.IsFinished) return default;
            if (player == 0)
            {
                if (!p.Held.IsValid) return Pickup(frame, tick, t, p);
                if (p.DamageCount == 0)
                {
                    var gap = QuantumMapConverter.Q(QuantumHazardSetup.DirectedPosition.x + 0.6f) - t.Position.X;
                    return new Quantum.Input { Horizontal = FPMath.Clamp(gap * 4 - p.Velocity.X, -FP._1, FP._1), Jump = FPMath.Abs(gap) < FP.FromString("0.3") && tick % 40 < 20 };
                }
                return Goal(t, p);
            }
            // 넉백이 수 칸을 밀어내므로 1P가 상자를 들고 도착할 때까지 기다려 서로 길을 막지 않게 한다.
            if (!Finished(frame, 0) && tick < 1200) return default;
            if (p.RespawnCount > 0) return Goal(t, p);
            if (p.DamageCount == 0) return new Quantum.Input { Horizontal = 1 };
            // 가시를 넘으면 벽 트랩에서 지속 접촉 재피격을 만든 뒤 수동 Reset으로 빠져나온다.
            if (t.Position.X > QuantumMapConverter.Q(QuantumHazardSetup.ThornPosition.x + 0.5f))
                return new Quantum.Input { Horizontal = 1, Run = true, Reset = p.DamageCount >= 4 && tick % 20 < 10 };
            // 가시 바로 앞에서 짧게 뛰어(가변 점프 최소 높이) 내려오며 위쪽에서 접촉한다.
            bool jump = p.IsGrounded && t.Position.X > FP.FromString("2.2") && t.Position.X < FP.FromString("2.6");
            return new Quantum.Input { Horizontal = 1, Run = true, Jump = jump };
        }
        return default;
    }

    private static bool Finished(Frame frame, int player)
    {
        var query = frame.Filter<FlipFriendsPlayer>();
        while (query.Next(out _, out var p)) if ((int)p.Player == player) return p.IsFinished;
        return false;
    }

    // 가장 가까운 상자 옆에 서서 그쪽을 본 뒤 들기를 누른다.
    private static Quantum.Input Pickup(Frame frame, int tick, Transform2D t, FlipFriendsPlayer p)
    {
        FPVector2 target = default; FP best = FP.MaxValue;
        var boxes = frame.Filter<Transform2D, FlipFriendsBox>();
        while (boxes.Next(out _, out var bt, out var box))
            if (!box.Carrier.IsValid && !box.RespawnPending && FPMath.Abs(bt.Position.X - t.Position.X) < best) { best = FPMath.Abs(bt.Position.X - t.Position.X); target = bt.Position; }
        FP side = target.X > t.Position.X ? -1 : 1;
        var gap = target.X + side * FP.FromString("0.88") - t.Position.X;
        if (FPMath.Abs(gap) > FP.FromString("0.05") || !p.IsGrounded)
            return new Quantum.Input { Horizontal = FPMath.Clamp(gap * 3 - p.Velocity.X, -FP._1, FP._1) };
        if (p.Facing != -(int)side) return new Quantum.Input { Horizontal = -side * FP.FromString("0.01") };
        return new Quantum.Input { Interact = tick % 20 < 10 };
    }

    private static Quantum.Input Goal(Transform2D t, FlipFriendsPlayer p)
    {
        var gap = QuantumMapConverter.Q(QuantumHazardSetup.GoalPosition.x) - t.Position.X;
        // 가시 오른쪽으로 넘어간 경우 달려 점프해 넘어온다.
        bool jump = p.IsGrounded && t.Position.X > QuantumMapConverter.Q(QuantumHazardSetup.ThornPosition.x) && t.Position.X < FP.FromString("6.9");
        // 내려놓인 상자에 막혀 제자리면 넘어간다.
        jump |= p.IsGrounded && FPMath.Abs(gap) > FP._0_50 && FPMath.Abs(t.Position.X - p.PreviousPosition.X) < FP.FromString("0.001");
        return new Quantum.Input { Horizontal = FPMath.Clamp(gap * 4 - p.Velocity.X * 2, -FP._1, FP._1), Run = true, Jump = jump || !p.IsGrounded && p.Velocity.Y > 0,
            Vertical = FPMath.Abs(gap) < FP.FromString("0.3") ? 1 : 0 };
    }

    public static async void Run()
    {
        int errors = 0;
        Application.LogCallback onLog = (_, _, type) => { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors++; };
        Application.logMessageReceived += onLog;
        try { Status = "검사 중"; var summary = await Replay(); Require(errors == 0, "실행 중 Unity 오류0"); Status = "통과: " + summary; }
        catch (Exception error) { Status = "실패: " + error; Debug.LogException(error); }
        finally { Application.logMessageReceived -= onLog; File.WriteAllText("Logs/QuantumHazardChecks.txt", Status); }
    }

    private static async Task<string> Replay()
    {
        QuantumGameSession session = null; QuantumRunner replay = null;
        var expected = new Dictionary<int, ulong>(); var actual = new Dictionary<int, ulong>();
        var trace = new List<string>(); var hits = new List<string>();
        var lastInvincible = new int[2]; var lastDamageFrame = new int[2]; var lastCount = new int[2]; var lastPutDown = new int[2];
        var lastRespawn = new int[2]; bool resetAfterHit = false;
        var intervals = new List<int>(); bool diagonal = false, directedExact = false, carrierDropped = false; int completed = 0;
        session = new QuantumGameSession();
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
                        Require(!FlipFriendsTerrain.Overlaps(c.Frame.RuntimeConfig.TerrainContours, t.Position + FlipFriendsInteractionSystem.Offset(p), FlipFriendsInteractionSystem.HalfSize(p)),
                            $"확정{c.Frame.Number} P{i} 지형 관통 {t.Position}");
                        if (p.DamageCount != lastCount[i])
                        {
                            Require(p.DamageCount == lastCount[i] + 1 && p.DamageFrame == c.Frame.Number, $"P{i} 틱당 피격1회");
                            Require(lastInvincible[i] <= 1 && p.InvincibleTicks == 60 && p.UncontrollableTicks == 60 && p.ClimbBlockTicks >= 60 && !p.IsClimbed, $"P{i} 무적 종료 뒤에만 피격·타이머60");
                            var trap = c.Frame.RuntimeConfig.Traps.First(x => FlipFriendsHazardSystem.Overlaps(x.Vertices, t.Position + FlipFriendsInteractionSystem.Offset(p), FlipFriendsInteractionSystem.HalfSize(p)));
                            var dir = FlipFriendsHazardSystem.Direction(trap, t.Position);
                            Require(p.Velocity == new FPVector2(dir.X * 20, dir.Y * 20), $"P{i} 넉백 = 방향×(20,20)");
                            if (trap.Direction != default) directedExact |= p.Velocity == new FPVector2(-20, 0);
                            else diagonal |= p.Velocity.Y > FP._0_50;
                            if (lastCount[i] > 0) intervals.Add(c.Frame.Number - lastDamageFrame[i]);
                            if (i == 0 && lastCount[i] == 0) carrierDropped = !p.Held.IsValid && p.PutDownCount == lastPutDown[i] + 1 && p.Facing == -1;
                            hits.Add($"{c.Frame.Number} P{i} 위치={t.Position} 속도={p.Velocity} 트랩={trap.Id} 운반={p.Held.IsValid}");
                            lastCount[i] = p.DamageCount; lastDamageFrame[i] = p.DamageFrame;
                        }
                        if (p.RespawnCount != lastRespawn[i])
                        {
                            Require(p.InvincibleTicks == 0 && p.UncontrollableTicks == 0 && p.ClimbBlockTicks == 0 && p.DamageCount == lastCount[i] && lastInvincible[i] > 0, $"P{i} 피격 직후 수동 Reset 초기화·카운터 유지");
                            resetAfterHit = true; lastRespawn[i] = p.RespawnCount;
                        }
                        lastInvincible[i] = p.InvincibleTicks; lastPutDown[i] = p.PutDownCount;
                        if (c.Frame.Number % 30 == 0) trace.Add($"{c.Frame.Number} P{i} {t.Position} v={p.Velocity} 무적={p.InvincibleTicks} 조작={p.UncontrollableTicks} 피격={p.DamageCount} 들기={p.Held.IsValid} 도착={p.IsFinished}");
                    }
                    if (completed == 0 && c.Frame.FlipFriendsCompletedFrame > 0) completed = c.Frame.FlipFriendsCompletedFrame;
                    if (c.Frame.Number % 120 == 0) expected[c.Frame.Number] = c.Frame.CalculateChecksum();
                }
                if (c.Game == replay?.Game && c.Frame.Number % 120 == 0) actual[c.Frame.Number] = c.Frame.CalculateChecksum();
            }))
            {
                await session.StartLocalAsync(Config()); session.Runner.IsSessionUpdateDisabled = true;
                for (int i = 0; i < Horizon + 200 && !expected.ContainsKey(Horizon); i++) session.Runner.Service(1f / 60);
                File.WriteAllText("Logs/QuantumHazardTrace.txt", string.Join("\n", trace) + "\n--- 피격\n" + string.Join("\n", hits));
                Require(lastCount[0] >= 1 && lastCount[1] >= 2, "실제 입력 양쪽 피격(1P1회↑·2P2회↑) " + string.Join(",", lastCount));
                Require(directedExact && diagonal && carrierDropped && resetAfterHit, $"피격 직후 Reset={resetAfterHit}·지정방향(-20,0)={directedExact}·위치기반 대각={diagonal}·운반자 피격 내려놓기={carrierDropped}");
                Require(intervals.Count > 0 && intervals.All(n => n >= 60) && intervals.Contains(60), "지속 접촉 재피격 간격60·무적 중 피격0 " + string.Join(",", intervals));
                Require(completed > 0, "피격 뒤 상자 운반 도착·전원 완료");
                var record = session.Runner.Game.GetRecordedReplay(includeChecksums: true); var serializer = new QuantumUnityJsonSerializer();
                replay = QuantumRunner.StartGame(new SessionRunner.Arguments { RunnerId = "FF_HAZARD_REPLAY",
                    RuntimeConfig = serializer.ConfigFromByteArray<RuntimeConfig>(record.RuntimeConfigData.Decode(), compressed: true),
                    SessionConfig = record.DeterministicConfig, ReplayProvider = record.CreateInputProvider(), InitialTick = record.InitialTick,
                    FrameData = record.InitialFrameData, GameMode = DeterministicGameMode.Replay, PlayerCount = 2 });
                replay.IsSessionUpdateDisabled = true;
                for (int i = 0; i < Horizon * 2 && !actual.ContainsKey(Horizon); i++) replay.Service(i % 2 == 0 ? 1f / 30 : 1f / 120);
                Require(expected.Count == Horizon / 120 && actual.Count == expected.Count && expected.All(p => actual[p.Key] == p.Value), $"확정 체크섬{expected.Count}개 일치");
                File.WriteAllText("Logs/QuantumHazardReplay.json", JsonUtility.ToJson(record));
                File.WriteAllText("Logs/QuantumHazardChecksums.txt", string.Join("\n", expected.Select(p => p.Key + ": " + p.Value)));
                var direct = Direct(session.Runner.Game.Frames.Verified);
                return $"피격 입력 기록·별도 러너 재생{expected.Count}개 일치(확정{Horizon}={expected[Horizon]})·피격 1P{lastCount[0]}/2P{lastCount[1]}·재피격 간격 {string.Join(",", intervals)}·완료{completed}·직접{direct}종·Unity 오류0";
            }
        }
        finally { if (replay != null) await replay.ShutdownAsync(); await session.StopAsync(); }
    }

    private static FlipFriendsTrapConfig TrapAt(string id, FPVector2 origin, FPVector2 direction, bool triangle) => new FlipFriendsTrapConfig
    {
        Id = id, Origin = origin, Direction = direction,
        Vertices = triangle ? new[] { origin + new FPVector2(-FP._0_50, -FP._0_50), origin + new FPVector2(FP._0_50, -FP._0_50), origin + new FPVector2(0, FP._0_50) }
            : new[] { origin + new FPVector2(-FP._0_50, -FP._0_50), origin + new FPVector2(FP._0_50, -FP._0_50), origin + new FPVector2(FP._0_50, FP._0_50), origin + new FPVector2(-FP._0_50, FP._0_50) },
    };

    private static int Direct(Frame frame)
    {
        var config = frame.RuntimeConfig;
        var originalTraps = config.Traps; var originalContours = config.TerrainContours;
        var originalRopes = (config.CustomMapRopeCenters, config.CustomMapRopeSizes);
        int completedFrame = frame.FlipFriendsCompletedFrame;
        var cases = new List<string>(); var created = new List<EntityRef>();
        EntityRef Player(FPVector2 position, FlipFriendsPlayer state)
        {
            var e = frame.Create(); created.Add(e);
            frame.Add(e, new Transform2D { Position = position }); frame.Add(e, state); return e;
        }
        FlipFriendsPlayer P(EntityRef e) => frame.Get<FlipFriendsPlayer>(e);
        var thorn = TrapAt("b-thorn", new FPVector2(104, FP._0_50), default, true);
        var half = new FPVector2(FP.FromString("0.4"), FP._0_50);
        try
        {
            config.TerrainContours = new[] { new FlipFriendsTerrainContour { Id = "floor", Vertices = new[] { new FPVector2(90, -5), new FPVector2(130, -5), new FPVector2(130, 0), new FPVector2(90, 0) } } };
            config.Traps = new[] { thorn };
            // 경계에 닿기만 하면 원본 겹침 판정처럼 피격이 아니다.
            Require(!FlipFriendsHazardSystem.Overlaps(thorn.Vertices, new FPVector2(FP.FromString("103.1"), FP._0_50), half), "경계 접촉 비피격");
            var side = Player(new FPVector2(FP.FromString("103.2"), FP._0_50), new FlipFriendsPlayer { IsGrounded = true, Facing = 1, VelocityXSmoothing = FP._0_50 });
            var sideDir = FlipFriendsHazardSystem.Direction(thorn, new FPVector2(FP.FromString("103.2"), FP._0_50));
            Require(FlipFriendsHazardSystem.TryDamage(frame, side) && P(side).Velocity == new FPVector2(sideDir.X * 20, sideDir.Y * 20) &&
                FPMath.Abs(P(side).Velocity.X + 20) < FP.FromString("0.001") && P(side).Velocity.Y == 0 && P(side).Facing == -1 && P(side).VelocityXSmoothing == FP._0_50, "옆 접촉 위치 기반 (-20,0)·방향 전환·스무딩 유지");
            var top = Player(new FPVector2(FP.FromString("103.6"), FP.FromString("1.3")), new FlipFriendsPlayer());
            var expectedTop = (new FPVector2(FP.FromString("103.6"), FP.FromString("1.3")) - thorn.Origin).Normalized;
            Require(FlipFriendsHazardSystem.TryDamage(frame, top) && P(top).Velocity == new FPVector2(expectedTop.X * 20, expectedTop.Y * 20) && P(top).Velocity.X < 0 && P(top).Velocity.Y > 0 &&
                FPMath.Abs(P(top).Velocity.Magnitude - 20) < FP.FromString("0.01"), "위 접촉 대각 정규화");
            var center = Player(thorn.Origin, new FlipFriendsPlayer { Velocity = new FPVector2(3, 3) });
            Require(FlipFriendsHazardSystem.TryDamage(frame, center) && P(center).Velocity == default && P(center).InvincibleTicks == 60, "기준점 일치 시 원본 normalized 0");
            cases.Add("위치 기반 좌우/대각·경계 비접촉·기준점 0");

            config.Traps = new[] { TrapAt("a", new FPVector2(110, FP._0_50), new FPVector2(2, 1), false), TrapAt("b", new FPVector2(110, FP._0_50), new FPVector2(-1, 0), false) };
            var raw = Player(new FPVector2(110, FP._0_50), new FlipFriendsPlayer());
            Require(FlipFriendsHazardSystem.TryDamage(frame, raw) && P(raw).Velocity == new FPVector2(40, 20), "지정 방향 비정규화·동일 틱 다중 접촉 첫 트랩");
            Require(!FlipFriendsHazardSystem.TryDamage(frame, raw) && P(raw).DamageCount == 1, "같은 틱 재판정 무적");
            config.Traps = config.Traps.Reverse().ToArray();
            var raw2 = Player(new FPVector2(110, FP._0_50), new FlipFriendsPlayer());
            Require(FlipFriendsHazardSystem.TryDamage(frame, raw2) && P(raw2).Velocity == new FPVector2(-20, 0), "배열 순서 첫 트랩 우선");
            cases.Add("지정 방향 비정규화·다중 접촉 첫 트랩·틱 내 재피격 없음");

            config.Traps = new[] { thorn };
            foreach (var (state, label) in new[] { (new FlipFriendsPlayer { InvincibleTicks = 1 }, "무적1틱"), (new FlipFriendsPlayer { IsFinished = true }, "도착"),
                (new FlipFriendsPlayer { RespawnPending = true }, "복귀 대기"), (new FlipFriendsPlayer { RespawnFrame = frame.Number }, "복귀 틱") })
            {
                var e = Player(new FPVector2(FP.FromString("103.2"), FP._0_50), state);
                Require(!FlipFriendsHazardSystem.TryDamage(frame, e) && P(e).DamageCount == 0, label + " 피격 제외");
            }
            cases.Add("무적·도착·복귀 대기/복귀 틱 제외");

            // 운반자 피격: 상자는 넉백 방향 앞에 내려놓고 관계를 끊는다. 들린 플레이어는 틱 시작 기준으로 이번 틱 판정에서 빠진다.
            frame.FlipFriendsCompletedFrame = 0;
            var carrier = Player(new FPVector2(FP.FromString("103.2"), FP._0_50), new FlipFriendsPlayer { Player = 0, Facing = 1, IsGrounded = true });
            var box = frame.Create(); created.Add(box);
            frame.Add(box, new Transform2D { Position = new FPVector2(FP.FromString("103.2"), FP.FromString("1.45")) });
            frame.Add(box, new FlipFriendsBox { HalfSize = new FPVector2(FP.FromString("0.45"), FP.FromString("0.45")), Carrier = carrier });
            var c0 = P(carrier); c0.Held = box; frame.Set(carrier, c0);
            new FlipFriendsHazardSystem().Update(frame);
            var boxState = frame.Get<FlipFriendsBox>(box);
            Require(P(carrier).DamageCount == 1 && !P(carrier).Held.IsValid && !boxState.Carrier.IsValid && P(carrier).PutDownCount == 1 &&
                frame.Get<Transform2D>(box).Position.X < FP.FromString("103.2") && boxState.Velocity == default, "운반자 피격 상자 넉백 쪽 내려놓기");
            frame.Destroy(box); created.Remove(box); frame.Destroy(carrier); created.Remove(carrier);

            var holder = Player(new FPVector2(FP.FromString("103.2"), FP._0_50), new FlipFriendsPlayer { Player = 0, Facing = 1 });
            var carried = Player(new FPVector2(FP.FromString("103.2"), FP.FromString("1.45")), new FlipFriendsPlayer { Player = 1, Carrier = holder });
            var h0 = P(holder); h0.Held = carried; frame.Set(holder, h0);
            new FlipFriendsHazardSystem().Update(frame);
            Require(P(holder).DamageCount == 1 && !P(holder).Held.IsValid && !P(carried).Carrier.IsValid && P(carried).Velocity == default && P(carried).DamageCount == 0,
                "운반자 피격 플레이어 내려놓기·들린 쪽 이번 틱 제외");
            Require(FlipFriendsHazardSystem.TryDamage(frame, carried) || !FlipFriendsHazardSystem.Overlaps(thorn.Vertices, frame.Get<Transform2D>(carried).Position, half), "내려놓인 뒤 다음 판정 대상");
            frame.Destroy(carried); created.Remove(carried); frame.Destroy(holder); created.Remove(holder);
            var lifted = Player(new FPVector2(FP.FromString("103.2"), FP._0_50), new FlipFriendsPlayer { Carrier = side });
            Require(!FlipFriendsHazardSystem.TryDamage(frame, lifted), "들린 플레이어 피격 제외");

            // 놓을 자리가 모두 막히면 기존 놓기 규칙대로 운반을 유지하되 넉백·무적은 적용한다.
            config.TerrainContours = new[] { config.TerrainContours[0],
                new FlipFriendsTerrainContour { Id = "low-ceiling", Vertices = new[] { new FPVector2(100, FP.FromString("1.42")), new FPVector2(FP.FromString("103.6"), FP.FromString("1.42")), new FPVector2(FP.FromString("103.6"), 5), new FPVector2(100, 5) } },
                new FlipFriendsTerrainContour { Id = "left-wall", Vertices = new[] { new FPVector2(100, 0), new FPVector2(FP.FromString("102.75"), 0), new FPVector2(FP.FromString("102.75"), 2), new FPVector2(100, 2) } } };
            var blocked = Player(new FPVector2(FP.FromString("103.2"), FP._0_50), new FlipFriendsPlayer { Player = 0, Facing = 1 });
            var heldPlayer = Player(new FPVector2(FP.FromString("103.2"), FP.FromString("1.45")), new FlipFriendsPlayer { Player = 1, Carrier = blocked });
            var blocker = frame.Create(); created.Add(blocker);
            frame.Add(blocker, new Transform2D { Position = new FPVector2(FP.FromString("103.2"), FP._0_50) });
            frame.Add(blocker, new FlipFriendsBox { HalfSize = new FPVector2(FP.FromString("0.45"), FP.FromString("0.45")) });
            var b0 = P(blocked); b0.Held = heldPlayer; frame.Set(blocked, b0);
            Require(FlipFriendsHazardSystem.TryDamage(frame, blocked) && P(blocked).Held == heldPlayer && P(heldPlayer).Carrier == blocked &&
                P(blocked).Velocity == new FPVector2(sideDir.X * 20, sideDir.Y * 20) && P(blocked).InvincibleTicks == 60, "막힌 놓기 운반 유지·넉백 적용");
            frame.Destroy(blocker); created.Remove(blocker);
            cases.Add("운반자 상자/플레이어 내려놓기·들린 쪽 제외·막힌 놓기 운반 유지");
            config.TerrainContours = new[] { config.TerrainContours[0] };

            // 로프: 피격은 등반을 끊고 60틱 진입을 막는다. 조작 제한 중 로프 점프는 기존처럼 거부된다.
            config.CustomMapRopeCenters = new[] { new FPVector2(120, 3) }; config.CustomMapRopeSizes = new[] { new FPVector2(1, 8) };
            config.Traps = new[] { TrapAt("rope", new FPVector2(120, 3), new FPVector2(1, 0), false) };
            var climber = Player(new FPVector2(120, 3), new FlipFriendsPlayer { IsClimbed = true, RopeContactCount = 1 });
            Require(FlipFriendsHazardSystem.TryDamage(frame, climber) && !P(climber).IsClimbed && P(climber).ClimbBlockTicks == 60, "로프 피격 등반 해제·차단60");
            config.Traps = Array.Empty<FlipFriendsTrapConfig>();
            var ct = frame.Get<Transform2D>(climber); var cp = P(climber); cp.Velocity = default; ct.Position = new FPVector2(120, 3);
            int climbTick = -1;
            for (int i = 1; i <= 70 && climbTick < 0; i++)
            {
                ct.Position = new FPVector2(120, 3); cp.Velocity = default;
                FlipFriendsMovementSystem.Simulate(frame, ref ct, ref cp, new Quantum.Input { Vertical = 1 });
                if (cp.IsClimbed) climbTick = i;
            }
            Require(climbTick == 60, "로프 재진입 60틱 " + climbTick);
            cp.UncontrollableTicks = 30; cp.WasJumpHeld = false; cp.Velocity = default;
            FlipFriendsMovementSystem.Simulate(frame, ref ct, ref cp, new Quantum.Input { Jump = true, Vertical = 1 });
            Require(cp.IsClimbed && cp.Velocity.Y < 13, "조작 제한 중 로프 점프 거부");
            config.CustomMapRopeCenters = originalRopes.CustomMapRopeCenters; config.CustomMapRopeSizes = originalRopes.CustomMapRopeSizes;
            cases.Add("로프 피격 해제·재진입60·조작 제한 로프 점프 거부");

            // 넉백과 입력·중력 합성: 원본처럼 수평은 smoothTime1 SmoothDamp, 수직은 중력·낙하12 제한을 그대로 탄다.
            var mt = new Transform2D { Position = new FPVector2(115, FP._0_50) };
            var mp = new FlipFriendsPlayer { IsGrounded = true, Velocity = new FPVector2(-20, 20), UncontrollableTicks = 60 };
            FP smoothing = mp.VelocityXSmoothing;
            var expectedX = FlipFriendsMovementSystem.SmoothHorizontal(-20, 4, ref smoothing, 1, frame.DeltaTime);
            FlipFriendsMovementSystem.Simulate(frame, ref mt, ref mp, new Quantum.Input { Horizontal = 1 });
            Require(mp.Velocity.X == expectedX && mp.Velocity.Y == 20 - 44 * frame.DeltaTime, "넉백 수평 smoothTime1·중력 합성");
            for (int i = 0; i < 40; i++) FlipFriendsMovementSystem.Simulate(frame, ref mt, ref mp, new Quantum.Input { Horizontal = 1 });
            Require(mp.Velocity.Y >= -12, "넉백 뒤 낙하12 제한");
            cases.Add("넉백 SmoothDamp1·중력·낙하 제한 합성");

            // 벽·천장·경사 인접 피격 뒤 연속 이동 비관통·정지 떨림 없음.
            foreach (var (contour, start, velocity, label) in new[]
            {
                (new FlipFriendsTerrainContour { Id = "wall", Vertices = new[] { new FPVector2(100, 0), new FPVector2(FP.FromString("101.5"), 0), new FPVector2(FP.FromString("101.5"), 6), new FPVector2(100, 6) } },
                    new FPVector2(102, FP._0_50), new FPVector2(-20, 20), "벽"),
                (new FlipFriendsTerrainContour { Id = "ceiling", Vertices = new[] { new FPVector2(100, FP.FromString("1.6")), new FPVector2(110, FP.FromString("1.6")), new FPVector2(110, 3), new FPVector2(100, 3) } },
                    new FPVector2(105, FP._0_50), new FPVector2(0, 20), "천장"),
                (new FlipFriendsTerrainContour { Id = "slope", Vertices = new[] { new FPVector2(100, 0), new FPVector2(110, 5), new FPVector2(110, 0) } },
                    new FPVector2(106, FP.FromString("3.8")), new FPVector2(-10, 10), "경사"),
            })
            {
                config.TerrainContours = new[] { new FlipFriendsTerrainContour { Id = "floor", Vertices = new[] { new FPVector2(90, -5), new FPVector2(130, -5), new FPVector2(130, 0), new FPVector2(90, 0) } }, contour };
                var wt = new Transform2D { Position = start }; var wp = new FlipFriendsPlayer { Velocity = velocity, UncontrollableTicks = 60 };
                Require(!FlipFriendsTerrain.Overlaps(config.TerrainContours, wt.Position, half), label + " 시작 비관통");
                for (int i = 0; i < 180; i++)
                {
                    if (wp.UncontrollableTicks > 0) wp.UncontrollableTicks--;
                    FlipFriendsMovementSystem.Simulate(frame, ref wt, ref wp, default);
                    Require(!FlipFriendsTerrain.Overlaps(config.TerrainContours, wt.Position, half), $"{label} 인접 넉백 비관통 {i} {wt.Position}");
                }
                var rest = wt.Position;
                for (int i = 0; i < 30; i++) FlipFriendsMovementSystem.Simulate(frame, ref wt, ref wp, default);
                Require(wp.IsGrounded && (label == "경사" || wt.Position == rest), $"{label} 넉백 뒤 착지·정지 떨림 없음 {rest}->{wt.Position}");
                cases.Add(label + " 인접 넉백 비관통·착지");
            }
            config.TerrainContours = originalContours;

            // 복귀·구간Reset·도착은 기존 Quantum 운동 상태 초기화 규칙을 따르고 관측 카운터만 남긴다.
            config.Traps = new[] { thorn };
            foreach (var mode in new[] { "수동/추락 복귀", "구간 복귀", "도착" })
            {
                var e = Player(new FPVector2(FP.FromString("103.2"), FP._0_50), new FlipFriendsPlayer { Player = (PlayerRef)0, HomePosition = new FPVector2(95, FP._0_50) });
                Require(FlipFriendsHazardSystem.TryDamage(frame, e), mode + " 전 피격");
                if (mode == "수동/추락 복귀") FlipFriendsRespawnSystem.QueuePlayer(frame, e);
                else if (mode == "구간 복귀") FlipFriendsRespawnSystem.ClearPlayer(frame, e, new FPVector2(95, FP._0_50), false);
                else
                {
                    var saved = (config.EnableGoals, config.GoalCenters, config.GoalSizes);
                    config.EnableGoals = true; config.GoalCenters = new[] { new FPVector2(FP.FromString("103.2"), FP._0_50) }; config.GoalSizes = new[] { FPVector2.One };
                    var et = frame.Get<Transform2D>(e); var ep = P(e);
                    Require(FlipFriendsMovementSystem.TryFinish(frame, ref et, ref ep, new Quantum.Input { Vertical = 1 }), "피격 직후 도착");
                    frame.Set(e, ep); (config.EnableGoals, config.GoalCenters, config.GoalSizes) = saved;
                }
                var after = P(e);
                Require(after.InvincibleTicks == 0 && after.UncontrollableTicks == 0 && after.ClimbBlockTicks == 0 && after.Velocity == default && after.DamageCount == 1, mode + " 타이머 초기화·카운터 유지");
            }
            cases.Add("피격 직후 수동/추락·구간 복귀·도착 초기화");

            // 상자에는 원본 피격 규칙이 없다. 트랩과 겹친 상자는 그대로다.
            var looseBox = frame.Create(); created.Add(looseBox);
            frame.Add(looseBox, new Transform2D { Position = thorn.Origin });
            frame.Add(looseBox, new FlipFriendsBox { HalfSize = new FPVector2(FP.FromString("0.45"), FP.FromString("0.45")), Velocity = new FPVector2(1, 1) });
            new FlipFriendsHazardSystem().Update(frame);
            Require(frame.Get<FlipFriendsBox>(looseBox).Velocity == new FPVector2(1, 1), "상자 피격 없음");
            cases.Add("상자 비대상");
        }
        finally
        {
            foreach (var e in created) if (frame.Exists(e)) frame.Destroy(e);
            config.Traps = originalTraps; config.TerrainContours = originalContours;
            config.CustomMapRopeCenters = originalRopes.CustomMapRopeCenters; config.CustomMapRopeSizes = originalRopes.CustomMapRopeSizes;
            frame.FlipFriendsCompletedFrame = completedFrame;
            File.WriteAllText("Logs/QuantumHazardDirect.txt", string.Join("\n", cases));
        }
        return cases.Count;
    }

    public static async void RunMain()
    {
        try
        {
            IntegrationStatus = "Main 검사 중";
            var flow = QuantumGameFlow.Instance;
            var stage = AssetDatabase.LoadAssetAtPath<QuantumOfficialStageAsset>(QuantumHazardSetup.StagePath);
            void Select()
            {
                Click("Start Button");
                for (int i = 0; i < flow.OfficialStages.Count; i++)
                { Click("공식 스테이지 선택"); if (flow.SelectedContent == (IQuantumMapContent)stage) return; }
                Require(false, "Main 피격 시험 목록 선택");
            }
            Select(); Click("오프라인 시작"); await Wait(() => flow.State == QuantumGameState.Playing, 10000);
            var session = flow.Session; int damage = 0;
            session.InputSource = (tick, slot) =>
            {
                var verified = session.Runner?.Game.Frames.Verified;
                if (verified != null) { int sum = 0; var q = verified.Filter<FlipFriendsPlayer>(); while (q.Next(out _, out var p)) sum += p.DamageCount; damage = Math.Max(damage, sum); }
                return InputFor(verified, tick, slot);
            };
            await Wait(() => flow.State == QuantumGameState.Ready && SceneManager.GetActiveScene().name == "Main", 90000);
            Require(damage >= 3 && !QuantumRunner.ActiveRunners.Any() && QuantumGameSession.ActiveInputSubscriptions == 0, "피격 시험 완료·피격 관측·정리");
            Select(); Click("오프라인 시작"); await Wait(() => flow.State == QuantumGameState.Playing, 10000);
            Click("플레이 종료"); await Wait(() => flow.State == QuantumGameState.Ready, 10000);
            Require(!QuantumRunner.ActiveRunners.Any() && QuantumGameSession.ActiveInputSubscriptions == 0, "피격 시험 재진입/메뉴 정리");
            IntegrationStatus = $"통과: Main 실제 버튼 피격 시험 선택·플레이(피격{damage})·전원 완료·복귀·재진입·메뉴 종료·러너/입력0";
        }
        catch (Exception error) { IntegrationStatus = "실패: " + error; Debug.LogException(error); }
        finally { File.WriteAllText("Logs/QuantumHazardMain.txt", IntegrationStatus); }
    }

    public static async void RunCloud()
    {
        var sessions = new[] { new QuantumGameSession(), new QuantumGameSession() };
        var checks = new[] { new Dictionary<int, ulong>(), new Dictionary<int, ulong>() };
        var damage = new[] { new Dictionary<int, string>(), new Dictionary<int, string>() };
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
                    while (q.Next(out _, out var p)) parts.Add($"P{(int)p.Player}:{p.DamageCount}/{p.DamageFrame}/{p.InvincibleTicks}/{p.Held.IsValid}");
                    damage[i][c.Frame.Number] = string.Join(" ", parts.OrderBy(s => s, StringComparer.Ordinal));
                }
            }))
            {
                string room = "ff-hazard-" + Guid.NewGuid().ToString("N");
                var first = sessions[0].StartDevelopmentAsync(Config(), room, false);
                await Wait(() => sessions[0].Client?.InRoom == true || first.IsFaulted, 15000); if (first.IsFaulted) await first;
                await Task.WhenAll(first, sessions[1].StartDevelopmentAsync(Config(), room, true));
                try { await Wait(() => sessions.All(s => s.Runner.Game.Frames.Verified.FlipFriendsCompletedFrame > 0) && checks.All(c => c.ContainsKey(Horizon)), 120000); }
                catch (InvalidOperationException) { throw new InvalidOperationException("피격 Photon 완료 대기 초과: " + string.Join(" | ", sessions.Select(Describe))); }
                var samples = checks[0].Where(p => p.Key >= 240).ToArray();
                Require(samples.Length == Horizon / 120 - 1 && samples.All(p => checks[1].TryGetValue(p.Key, out var h) && p.Value == h), $"피격 Photon 확정{samples.Length}개");
                Require(damage[0].All(p => damage[1][p.Key] == p.Value) && !damage[0][Horizon].Contains("P0:0/") && !damage[0][Horizon].Contains("P1:0/"), "피격 상태 양쪽 일치");
                int completed = sessions[0].Runner.Game.Frames.Verified.FlipFriendsCompletedFrame;
                Require(completed == sessions[1].Runner.Game.Frames.Verified.FlipFriendsCompletedFrame, "피격 Photon 완료 프레임");
                IntegrationStatus = $"통과: 피격 Photon 동일PC 두 연결·지역={sessions[0].Client.CurrentRegion}·RTT={sessions[0].Client.RealtimePeer.Stats.RoundtripTime}/{sessions[1].Client.RealtimePeer.Stats.RoundtripTime}ms·완료={completed}·확정{samples.Length}개·피격 상태 일치({damage[0][Horizon]})·지연/손실 주입 없음\n" + string.Join("\n", samples.Select(p => p.Key + ": " + p.Value));
            }
        }
        catch (Exception error) { IntegrationStatus = "실패: " + error; Debug.LogException(error); }
        finally
        {
            foreach (var session in sessions) await session.StopAsync();
            IntegrationStatus += "\n러너/연결/입력 정리=" + sessions.All(s => s.Runner == null && s.Client == null && !s.HasInputSubscription);
            File.WriteAllText("Logs/QuantumHazardCloud.txt", IntegrationStatus);
        }
    }

    private static string Describe(QuantumGameSession session)
    {
        var frame = session.Runner?.Game.Frames.Verified;
        if (frame == null) return "러너 없음";
        var parts = new List<string> { "확정" + frame.Number };
        var q = frame.Filter<Transform2D, FlipFriendsPlayer>();
        while (q.Next(out _, out var t, out var p)) parts.Add($"P{(int)p.Player} {t.Position} 피격={p.DamageCount} 들기={p.Held.IsValid} 복귀={p.RespawnCount} 도착={p.IsFinished}");
        return string.Join(" ", parts);
    }

    private static void Click(string name) => UnityEngine.Object.FindObjectsByType<Button>(FindObjectsInactive.Exclude).Single(b => b.name == name).onClick.Invoke();
    private static async Task Wait(Func<bool> ready, int timeout)
    { var end = DateTime.UtcNow.AddMilliseconds(timeout); while (!ready() && DateTime.UtcNow < end && EditorApplication.isPlaying) await Task.Delay(20); Require(ready(), "비동기 제한시간"); }

    private static void Require(bool value, string label)
    { if (!value) throw new InvalidOperationException("피격 검사 실패: " + label); }
}
