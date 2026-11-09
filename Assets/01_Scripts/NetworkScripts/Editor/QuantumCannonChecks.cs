using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FlipFriends.Maps;
using FlipFriends.Network;
using Photon.Deterministic;
using Quantum;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using LogType = UnityEngine.LogType;

public static class QuantumCannonChecks
{
    private const int Horizon = 2400;
    private const int ChecksumInterval = 120;
    // 제품13까지의 공식 시험 7종 해시. 제품14 변환기에서도 그대로여야 한다.
    public static readonly (string id, string agreement)[] ExistingStages =
    {
        ("official-test", "공식1:f7695175f228c1e20b9481517d73898374aab6ea96eaedde885bf36cf49442c9"),
        ("official-slope-test", "공식1:ead65eb3cba1920f83885f660ce53f09e8153d85cbd5001c616196139d230287"),
        ("official-hazard-test", "공식1:5100d2c66f594f23cc9ee3cff626338745182cc457d17557b68b4408cedd1c19"),
        ("official-launcher-test", "공식1:3384b7e4105060e0f0b32cc0a92be8dfdaa0b969b7d242fd553cc745336bb89e"),
        ("official-motion-test", "공식1:c9bd60ef653e25285cfe6d41c67b758d33582a45977e5ee34ac24b57b592354b"),
        ("official-switch-test", "공식2:1505d624064ea08219dd263ccf445283857e87d1e408ffcf34bacd4e3bed5ad1"),
        ("official-conveyor-test", "공식3:12b0726fcfb2ccb82fa0b4dcbcd6e3882e7b6911aaaff2a60b8cb1f88d6f3292"),
    };
    public static string Status { get; private set; } = "미실행";

    private static QuantumOfficialStageAsset Asset => AssetDatabase.LoadAssetAtPath<QuantumOfficialStageAsset>(QuantumCannonSetup.StagePath);

    public static RuntimeConfig Config()
    {
        var config = new RuntimeConfig();
        QuantumGameContent.Prepare(Asset, config);
        return config;
    }

    private static void Require(bool condition, string reason)
    {
        if (!condition) throw new InvalidOperationException("대포 검사: " + reason);
    }

    public static void RunContent()
    {
        var read = QuantumOfficialStageWriter.ReadPrefab(QuantumCannonSetup.PrefabPath);
        var terrain = ScriptableObject.CreateInstance<QuantumMapTerrain>();
        var results = new List<string>();
        try
        {
            var catalog = AssetDatabase.LoadAssetAtPath<QuantumOfficialStageCatalog>(QuantumOfficialStageWriter.CatalogPath);
            foreach (var (id, agreement) in ExistingStages)
            {
                var stage = catalog.Stages.Single(s => s.StageId == id);
                stage.ValidateAndApply(new RuntimeConfig());
                Require(stage.Agreement == agreement, $"기존 해시 {id}={stage.Agreement}");
            }
            Require(catalog.Stages.Count == ExistingStages.Length + 1 && catalog.Stages.Last() == Asset, "목록 8종·대포 시험 마지막");
            results.Add("기존 공식 7종 해시 유지·재검증");

            terrain.Configure(read.Terrain, read.TerrainBounds, read.TerrainSignature);
            QuantumMapConversion Convert(QuantumOfficialStageData data) => QuantumOfficialStageConverter.Convert(data, terrain, null);
            var converted = Convert(read.Data);
            Require(converted.ContentHash == Asset.ContentHash && Asset.Agreement.StartsWith("공식4:") && converted.Cannons.Count == 4, "재변환 해시/공식4/대포4");
            Require(Config().CustomMapAgreement.StartsWith(Asset.Agreement + ":제품14:") && QuantumGameContent.NetworkVersion == "ff-product-14", "제품14 합의");
            var fast = converted.Cannons.Single(c => c.Id.EndsWith("/빠른 대포"));
            Require(fast.IntervalTicks == FP.FromString("7.5") && fast.Velocity == new FPVector2(-30, 0) && fast.Muzzle == new FPVector2(19, 3), "빠른 대포 7.5틱·속도·포구");
            var down = converted.Cannons.Single(c => c.Id.EndsWith("/하강 대포"));
            Require(down.DelayTicks == 72 && down.IntervalTicks == 180 && down.Velocity == new FPVector2(0, -8), "하강 대포 지연72·간격180·방향");
            Array.Reverse(read.Data.cannons);
            Require(Convert(read.Data).ContentHash == Asset.ContentHash, "작성 배열 순서와 무관한 해시");
            read.Data.cannons[0].interval += 0.5f;
            Require(Convert(read.Data).ContentHash != Asset.ContentHash, "간격 해시");
            read.Data.cannons[0].interval -= 0.5f;
            Require(Convert(read.Data).ContentHash == Asset.ContentHash, "복원 해시");
            results.Add("재변환·순서 독립·간격 해시·공식4·제품14·7.5틱/지연 환산");

            var mutations = new (string name, Action<QuantumOfficialStageData> apply)[]
            {
                ("누락", d => d.cannons[0] = null),
                ("중복", d => d.cannons[1].id = d.cannons[0].id),
                ("몸체 참조", d => d.cannons[0].id = d.stageId + "/없는 대포"),
                ("이동 대포", d => d.motions = new[] { new QuantumMotionSource { id = d.cannons[0].id, kind = 1, waypoints = new[] { Vector2.zero, Vector2.up }, speed = 1, renderSize = Vector2.one } }),
                ("간격0", d => d.cannons[0].interval = 0),
                ("1틱 미만 간격", d => d.cannons[0].interval = 0.01f),
                ("음수 지연", d => d.cannons[0].delay = -1),
                ("비유한 속도", d => d.cannons[0].speed = float.NaN),
                ("방향0", d => d.cannons[0].direction = Vector2.zero),
                ("터널링 속도", d => d.cannons[0].speed = 31),
                ("반지름0", d => d.cannons[0].radius = 0),
                ("동시 탄 상한", d => { foreach (var c in d.cannons) { c.interval = 0.02f; c.distance = 20; c.speed = 5; } }),
                ("수명 초과", d => { d.cannons[0].speed = 0.1f; d.cannons[0].distance = 20; }),
            };
            foreach (var (name, apply) in mutations)
            {
                var copy = JsonUtility.FromJson<QuantumOfficialStageData>(JsonUtility.ToJson(read.Data));
                for (int i = 0; i < copy.cannons.Length; i++) copy.cannons[i].projectileSprite = read.Data.cannons[i].projectileSprite;
                apply(copy);
                bool rejected = false;
                try { Convert(copy); }
                catch (InvalidOperationException error) { rejected = error.Message.Contains("위치=") || error.Message.Contains("대포 "); }
                Require(rejected, name + " 위치/원인 거부");
                results.Add("변환 거부 " + name);
            }

            foreach (var test in new[] { "몸체없음", "탄표시없음", "회전", "배율", "발사장치혼합", "이동혼합", "비활성" })
            {
                var previous = SceneManager.GetActiveScene();
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                SceneManager.SetActiveScene(scene);
                try
                {
                    var root = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(QuantumCannonSetup.PrefabPath));
                    var cannon = root.GetComponentsInChildren<QuantumStageCannon>().First(c => c.name == "왼쪽 대포");
                    if (test == "몸체없음") { UnityEngine.Object.DestroyImmediate(cannon.GetComponent<QuantumStageTrap>()); UnityEngine.Object.DestroyImmediate(cannon.GetComponent<BoxCollider2D>()); }
                    if (test == "회전") cannon.transform.eulerAngles = new Vector3(0, 0, 90);
                    if (test == "배율") cannon.transform.localScale = new Vector3(-1, 1, 1);
                    if (test == "발사장치혼합") cannon.gameObject.AddComponent<QuantumStageLauncher>();
                    if (test == "이동혼합") cannon.gameObject.AddComponent<QuantumStageMotion>();
                    if (test == "비활성") cannon.gameObject.SetActive(false);
                    if (test == "탄표시없음")
                    {
                        var so = new SerializedObject(cannon); so.FindProperty("projectileSprite").objectReferenceValue = null; so.ApplyModifiedPropertiesWithoutUndo();
                    }
                    bool rejected = false;
                    try { QuantumOfficialStageWriter.Read(root); }
                    catch (InvalidOperationException error) { rejected = error.Message.Contains("왼쪽 대포"); }
                    Require(rejected, "작성기 " + test);
                    results.Add("작성기 거부 " + test);
                }
                finally { SceneManager.SetActiveScene(previous); EditorSceneManager.CloseScene(scene, true); }
            }
            Status = "통과: 콘텐츠\n" + string.Join("\n", results);
        }
        catch (Exception error) { Status = "실패: " + error; Debug.LogException(error); }
        finally { UnityEngine.Object.DestroyImmediate(terrain); File.WriteAllText("Logs/QuantumCannonContent.txt", Status); }
    }

    private static int CannonIndex(Frame frame, string name) =>
        Array.FindIndex(frame.RuntimeConfig.Cannons, c => c.Id.EndsWith("/" + name, StringComparison.Ordinal));

    // 둘 다 왼쪽 도착점으로 걷는다. 한 번 맞은 뒤부터는 바닥 대포 탄이 다가오면 뛰어넘는다.
    // 2P는 첫 피격 뒤 한 번 수동 Reset을 눌러 복귀가 탄 상태와 무관함을 본다.
    public static Quantum.Input InputFor(Frame frame, int tick, int player)
    {
        if (frame == null || frame.FlipFriendsMotionStartedFrame == 0) return default;
        int age = tick - frame.FlipFriendsMotionStartedFrame;
        var players = frame.Filter<Transform2D, FlipFriendsPlayer>();
        while (players.Next(out _, out var transform, out var state))
        {
            if ((int)state.Player != player || state.IsFinished) continue;
            if (player == 1 && state.DamageCount == 1 && state.RespawnCount == 0 && state.InvincibleTicks == 0 && age > 0)
                return new Quantum.Input { Reset = true };
            // 탄 위치는 발사 예약에서 입력 틱 기준으로 계산한다. 늦은 확정 프레임의 탄 위치를 쓰면 온라인에서 회피가 늦다.
            bool jump = false;
            var cannon = frame.RuntimeConfig.Cannons[CannonIndex(frame, "왼쪽 대포")];
            long relRaw = (long)age << 16;
            for (long k = Math.Max(0, (relRaw - cannon.DelayTicks.RawValue) / cannon.IntervalTicks.RawValue - 2); state.DamageCount > 0; k++)
            {
                long dueRaw = cannon.DelayTicks.RawValue + k * cannon.IntervalTicks.RawValue;
                if (dueRaw > relRaw) break;
                long spawn = (dueRaw + 65535) >> 16;
                FP ballX = cannon.Muzzle.X + cannon.Velocity.X * frame.DeltaTime * (int)(age - spawn);
                FP dx = transform.Position.X - ballX;
                if (dx > -FP._0_25 && dx < FP.FromString("2.5")) jump = true;
            }
            FP gap = QuantumCannonSetup.GoalX - transform.Position.X;
            return new Quantum.Input
            {
                Horizontal = FPMath.Clamp(gap * 3 - state.Velocity.X * 2, -FP._1, FP._1),
                Vertical = FPMath.Abs(gap) < FP._0_50 ? 1 : 0,
                Jump = jump,
            };
        }
        return default;
    }

    // 확정 프레임마다 비교하는 대포·탄·피격 상태. 탄 순서는 엔티티 색인과 무관하게 (대포, 발사 번호)로 정렬한다.
    public static string State(Frame frame)
    {
        var parts = new List<string>();
        var cannons = frame.Filter<FlipFriendsCannon>();
        while (cannons.Next(out _, out var c)) parts.Add($"C{c.ContentIndex}:{c.ShotCount}/{c.LastShotFrame}");
        var shots = new List<string>();
        var q = frame.Filter<FlipFriendsProjectile>();
        while (q.Next(out _, out var s)) shots.Add($"{s.Cannon:D2}.{s.Shot:D5}@{s.SpawnFrame}:{s.Position.X.RawValue},{s.Position.Y.RawValue}h{s.HitCount}");
        shots.Sort(StringComparer.Ordinal);
        parts.AddRange(shots);
        var players = frame.Filter<Transform2D, FlipFriendsPlayer>();
        while (players.Next(out _, out var t, out var p))
            parts.Add($"P{(int)p.Player}:{t.Position.X.RawValue},{t.Position.Y.RawValue} 피격{p.DamageCount}@{p.DamageFrame} 무적{p.InvincibleTicks} 복귀{p.RespawnCount} 도착{p.IsFinished}");
        parts.Add($"탄{frame.FlipFriendsProjectileCount} 생성{frame.FlipFriendsProjectileSpawned} 소멸{frame.FlipFriendsProjectileExpired} 누락{frame.FlipFriendsDroppedShots} 완료{frame.FlipFriendsCompletedFrame}");
        return string.Join(" ", parts);
    }

    // 세션 시작 틱과 무관하게 비교하도록 시작 프레임 기준 상대 틱으로 사건을 남긴다.
    private sealed class Events
    {
        public readonly List<string> Lines = new();
        private readonly Dictionary<long, (int spawn, FPVector2 last, FPVector2 origin)> alive = new();
        private readonly int[] damage = new int[2];
        private readonly int[] respawn = new int[2];
        private int completed;
        public readonly Dictionary<int, List<int>> Lifetimes = new();
        public readonly Dictionary<int, List<int>> ShotTicks = new();
        public bool PassedWall, PassedFloor;
        public int MaxAlive, ProjectileHits;

        public void Observe(Frame frame)
        {
            int start = frame.FlipFriendsMotionStartedFrame;
            if (start == 0) return;
            int rel = frame.Number - start;
            var seen = new HashSet<long>();
            int count = 0;
            var q = frame.Filter<FlipFriendsProjectile>();
            while (q.Next(out _, out var s))
            {
                count++;
                long key = ((long)s.Cannon << 32) | (uint)s.Shot;
                seen.Add(key);
                if (!alive.ContainsKey(key))
                {
                    Lines.Add($"{rel} 발사 대포{s.Cannon} #{s.Shot} 생성틱{s.SpawnFrame - start}");
                    if (!ShotTicks.TryGetValue(s.Cannon, out var list)) ShotTicks[s.Cannon] = list = new List<int>();
                    list.Add(s.SpawnFrame - start);
                }
                alive[key] = (s.SpawnFrame, s.Position, s.Origin);
                var id = frame.RuntimeConfig.Cannons[s.Cannon].Id;
                if (id.EndsWith("/벽 관통 대포") && s.Position.X < QuantumCannonSetup.WallX) PassedWall = true;
                if (id.EndsWith("/하강 대포") && s.Position.Y < -2) PassedFloor = true;
            }
            MaxAlive = Math.Max(MaxAlive, count);
            foreach (var key in alive.Keys.Where(k => !seen.Contains(k)).ToArray())
            {
                var (spawn, last, origin) = alive[key];
                int cannon = (int)(key >> 32);
                var config = frame.RuntimeConfig.Cannons[cannon];
                // 제거 틱 직전 위치는 6의 배수 나이에서 처음 최대 거리를 넘은 위치이며 그 전 검사 위치는 넘지 않았다.
                var step = config.Velocity * frame.DeltaTime;
                var previousCheck = last - step * FlipFriendsCannonSystem.RangeCheckTicks;
                if (frame.FlipFriendsCompletedFrame == 0 && !((last - origin).SqrMagnitude > config.MaxDistanceSquared && (previousCheck - origin).SqrMagnitude <= config.MaxDistanceSquared))
                    throw new InvalidOperationException($"대포 검사: 탄 대포{cannon} #{(int)key} 소멸 거리 위치={last}");
                Lines.Add($"{rel} 소멸 대포{cannon} #{(int)key} 수명{frame.Number - spawn}");
                if (!Lifetimes.TryGetValue(cannon, out var list)) Lifetimes[cannon] = list = new List<int>();
                list.Add(frame.Number - spawn);
                alive.Remove(key);
            }
            var players = frame.Filter<FlipFriendsPlayer>();
            while (players.Next(out _, out var p))
            {
                int i = (int)p.Player;
                if (p.DamageCount != damage[i]) { Lines.Add($"{rel} 피격 P{i} {p.DamageCount}회"); damage[i] = p.DamageCount; ProjectileHits++; }
                if (p.RespawnCount != respawn[i]) { Lines.Add($"{rel} 복귀 P{i} 탄{count}"); respawn[i] = p.RespawnCount; }
            }
            if (frame.FlipFriendsCompletedFrame != 0 && completed == 0) { completed = frame.FlipFriendsCompletedFrame; Lines.Add($"{rel} 전원 완료 상대{completed - start}"); }
        }
    }

    public static async void Run()
    {
        var session = new QuantumGameSession();
        QuantumRunner replay = null;
        Status = "실행 중";
        var expected = new Dictionary<int, ulong>();
        var actual = new Dictionary<int, ulong>();
        var states = new SortedDictionary<int, string>();
        var replayStates = new Dictionary<int, string>();
        var events = new Events(); var replayEvents = new Events();
        var frozen = new Dictionary<int, string>();
        int errors = 0;
        Application.LogCallback log = (_, _, type) => { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors++; };
        Application.logMessageReceived += log;
        try
        {
            Require(EditorApplication.isPlaying, "Play 상태 필요");
            session.InputSource = (tick, slot) => InputFor(session.Runner?.Game.Frames.Verified, tick, slot);
            using (QuantumCallback.SubscribeManual<CallbackSimulateFinished>(callback =>
            {
                var frame = callback.Frame;
                if (!frame.IsVerified || frame.Number > Horizon) return;
                bool isRecord = callback.Game == session.Runner?.Game;
                if (!isRecord && callback.Game != replay?.Game) return;
                if (frame.Number % ChecksumInterval == 0) (isRecord ? expected : actual)[frame.Number] = frame.CalculateChecksum();
                (isRecord ? events : replayEvents).Observe(frame);
                string state = State(frame);
                if (isRecord) states[frame.Number] = state; else replayStates[frame.Number] = state;
                if (!isRecord) return;
                Require(frame.FlipFriendsProjectileSpawned - frame.FlipFriendsProjectileExpired == frame.FlipFriendsProjectileCount && frame.FlipFriendsDroppedShots == 0, "탄 수 불변식 " + frame.Number);
                var players = frame.Filter<Transform2D, FlipFriendsPlayer>();
                while (players.Next(out _, out var t, out var p))
                    Require(!FlipFriendsMotionSystem.Overlaps(frame, t.Position + FlipFriendsInteractionSystem.Offset(p), FlipFriendsInteractionSystem.HalfSize(p)), "지형 비관통 " + frame.Number);
                // 전원 도착 뒤에는 탄 상태가 멈춘다.
                if (frame.FlipFriendsCompletedFrame > 0) frozen[frame.Number] = string.Join(" ", state.Split(' ').Where(s => s.Contains("@") && s.Contains(".")));
            }))
            {
                await session.StartLocalAsync(Config());
                session.Runner.IsSessionUpdateDisabled = true;
                for (int i = 0; i < Horizon + 200 && !expected.ContainsKey(Horizon); i++) session.Runner.Service(1f / 60);
                var final = session.Runner.Game.Frames.Verified;
                int complete = final.FlipFriendsCompletedFrame, start = final.FlipFriendsMotionStartedFrame;
                Require(complete > 0, "전원 완료");
                Require(frozen.Values.Distinct().Count() == 1, "도착 뒤 탄 정지");
                Require(events.PassedWall && events.PassedFloor, $"지형 통과 벽={events.PassedWall} 바닥={events.PassedFloor}");
                Require(events.ProjectileHits >= 2, "탄 피격 2회 이상 " + events.ProjectileHits);
                Require(events.Lines.Any(l => l.Contains(" 복귀 P1 ")), "수동 복귀");
                int fast = CannonIndex(final, "빠른 대포"), left = CannonIndex(final, "왼쪽 대포");
                // 7.5틱 간격은 k번째 발사가 ceil(7.5k) 틱이어야 하며 누적 오차가 없어야 한다.
                var fastTicks = events.ShotTicks[fast];
                Require(fastTicks.Select((t, k) => t == (int)Math.Ceiling(7.5 * k)).All(x => x) && fastTicks.Count > 40, "빠른 대포 7.5틱 발사 " + string.Join(",", fastTicks.Take(8)));
                Require(events.ShotTicks[left].Select((t, k) => t == 120 * k).All(x => x), "왼쪽 대포 120틱 발사");
                Require(events.Lifetimes[left].All(l => l == 127) && events.Lifetimes[fast].All(l => l == 13), $"수명 왼쪽={string.Join(",", events.Lifetimes[left].Distinct())} 빠른={string.Join(",", events.Lifetimes[fast].Distinct())}");
                var record = session.Runner.Game.GetRecordedReplay(includeChecksums: true);
                var serializer = new QuantumUnityJsonSerializer();
                replay = QuantumRunner.StartGame(new SessionRunner.Arguments
                {
                    RunnerId = "FF_CANNON_REPLAY", RuntimeConfig = serializer.ConfigFromByteArray<RuntimeConfig>(record.RuntimeConfigData.Decode(), compressed: true),
                    SessionConfig = record.DeterministicConfig, ReplayProvider = record.CreateInputProvider(),
                    InitialTick = record.InitialTick, FrameData = record.InitialFrameData, GameMode = DeterministicGameMode.Replay, PlayerCount = 2,
                });
                replay.IsSessionUpdateDisabled = true;
                for (int i = 0; i < Horizon * 2 && !actual.ContainsKey(Horizon); i++) replay.Service(i % 2 == 0 ? 1f / 30 : 1f / 120);
                Require(expected.Count == 20 && expected.Count == actual.Count && expected.All(p => actual.TryGetValue(p.Key, out ulong checksum) && checksum == p.Value),
                    $"입력 기록/재생 확정 체크섬 (기록 {expected.Count}, 재생 {actual.Count})");
                var compared = states.Where(p => p.Key >= ChecksumInterval).ToArray();
                Require(compared.Length > 0 && compared.All(p => replayStates.TryGetValue(p.Key, out var value) && value == p.Value), "확정 프레임별 대포·탄·피격 상태");
                var recordedEvents = events.Lines.Where(l => int.Parse(l.Split(' ')[0]) >= ChecksumInterval - start).ToArray();
                Require(recordedEvents.SequenceEqual(replayEvents.Lines.Where(l => int.Parse(l.Split(' ')[0]) >= ChecksumInterval - start)), "상대 틱 사건(발사·소멸·피격·복귀·완료) 순서");
                File.WriteAllText("Logs/QuantumCannonReplay.json", JsonUtility.ToJson(record));
                File.WriteAllLines("Logs/QuantumCannonChecksums.txt", expected.Select(p => $"{p.Key}: {p.Value}"));
                File.WriteAllLines("Logs/QuantumCannonEvents.txt", events.Lines);
                int direct = QuantumCannonDirectChecks.Run(final);
                Require(errors == 0, "Unity 오류0");
                Status = $"통과: 별도 재생 확정20개·상태 {compared.Length}프레임·사건 {recordedEvents.Length}개 일치·탄 피격{events.ProjectileHits}·최대 동시탄{events.MaxAlive}·벽/바닥 통과·7.5틱 {fastTicks.Count}발 오차0·수명 127/13틱·전원 완료 상대{complete - start}(절대{complete})·직접{direct}종·확정2400={expected[Horizon]}";
            }
        }
        catch (Exception error) { Status = "실패: " + error; Debug.LogException(error); }
        finally
        {
            Application.logMessageReceived -= log;
            if (replay != null) await replay.ShutdownAsync();
            await session.StopAsync();
            File.WriteAllLines("Logs/QuantumCannonTrace.txt", events.Lines);
            File.WriteAllText("Logs/QuantumCannonChecks.txt", Status + "\n러너=" + QuantumRunner.ActiveRunners.Count() + " 입력=" + QuantumGameSession.ActiveInputSubscriptions);
        }
    }

    public static void RunMain() =>
        QuantumMotionChecks.RunOfficialMain(QuantumCannonSetup.StageId, InputFor, "QuantumCannonMain");

    public static void RunCloud() =>
        QuantumMotionChecks.RunOfficialCloud(Config, InputFor, State, "QuantumCannonCloud");
}
