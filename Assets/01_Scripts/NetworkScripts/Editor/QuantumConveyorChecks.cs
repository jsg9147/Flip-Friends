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

public static class QuantumConveyorChecks
{
    private const int Horizon = 2400;
    private const int ChecksumInterval = 120;
    public static string Status { get; private set; } = "미실행";

    private static QuantumOfficialStageAsset Asset => AssetDatabase.LoadAssetAtPath<QuantumOfficialStageAsset>(QuantumConveyorSetup.StagePath);

    private static RuntimeConfig Config()
    {
        var config = new RuntimeConfig();
        QuantumGameContent.Prepare(Asset, config);
        return config;
    }

    private static void Require(bool condition, string reason)
    {
        if (!condition) throw new InvalidOperationException("벨트 검사: " + reason);
    }

    public static void RunContent()
    {
        var read = QuantumOfficialStageWriter.ReadPrefab(QuantumConveyorSetup.PrefabPath);
        var terrain = ScriptableObject.CreateInstance<QuantumMapTerrain>();
        var results = new List<string>();
        try
        {
            terrain.Configure(read.Terrain, read.TerrainBounds, read.TerrainSignature);
            QuantumMapConversion Convert(QuantumOfficialStageData data) => QuantumOfficialStageConverter.Convert(data, terrain, null);
            var converted = Convert(read.Data);
            Require(converted.ContentHash == Asset.ContentHash && Asset.Agreement.StartsWith("공식3:") && converted.Conveyors.Count == 4, "재변환 해시/공식3/벨트4");
            Require(Config().CustomMapAgreement.StartsWith(Asset.Agreement + ":제품14:") && QuantumGameContent.NetworkVersion == "ff-product-14", "제품14 합의");
            var belts = converted.Conveyors;
            Require(belts.Count(b => b.TerrainAnchor >= 0) == 1 && belts.Count(b => b.BeltAnchor >= 0) == 1 && belts.Count(b => b.Group == "") == 2 &&
                converted.StageSwitches.Single().Target < 0, "고정2·지형그룹1·벨트전용그룹1·벨트 스위치");
            Array.Reverse(read.Data.conveyors);
            Require(Convert(read.Data).ContentHash == Asset.ContentHash, "작성 배열 순서와 무관한 해시");
            read.Data.conveyors.First(c => c.group == "").initialClockwise ^= true;
            Require(Convert(read.Data).ContentHash != Asset.ContentHash, "방향 해시");
            read.Data.conveyors.First(c => c.group == "").initialClockwise ^= true;
            results.Add("재변환·순서 독립·방향 해시·공식3·제품14");

            var mutations = new (string name, Action<QuantumOfficialStageData> apply)[]
            {
                ("누락", d => d.conveyors[0] = null),
                ("중복", d => d.conveyors[1].id = d.conveyors[0].id),
                ("발판 참조", d => d.conveyors[0].id = "missing"),
                ("간격", d => d.conveyors.First(c => c.group == "mixed").interval = 2),
                ("비유한", d => d.conveyors.First(c => c.group == "belt-manual").interval = float.NaN),
                ("고정 간격", d => d.conveyors.First(c => c.group == "").interval = 1),
                ("스위치 없는 수동", d => d.switches = Array.Empty<QuantumStageSwitchSource>()),
                ("문 대상", d => { d.switches[0].isDoor = true; d.switches[0].detectBoxes = true; d.switches[0].cooldown = 0; }),
                ("트랩 발판", d => d.motions.First(m => m.id == d.conveyors.First(c => c.group == "").id).kind = 2),
            };
            foreach (var (name, apply) in mutations)
            {
                var copy = JsonUtility.FromJson<QuantumOfficialStageData>(JsonUtility.ToJson(read.Data));
                apply(copy);
                bool rejected = false;
                try { Convert(copy); }
                catch (InvalidOperationException error) { rejected = error.Message.Contains("위치="); }
                Require(rejected, name + " 위치/원인 거부");
                results.Add("변환 거부 " + name);
            }

            foreach (var test in new[] { "트리거", "오프셋", "회전", "다중", "지형혼합", "트랩종류", "스위치참조" })
            {
                var previous = SceneManager.GetActiveScene();
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                SceneManager.SetActiveScene(scene);
                try
                {
                    var root = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(QuantumConveyorSetup.PrefabPath));
                    var fixedBelt = root.GetComponentsInChildren<QuantumStageConveyor>().First(b => b.name == "고정 벨트");
                    var movingBelt = root.GetComponentsInChildren<QuantumStageConveyor>().First(b => b.name == "이동 벨트");
                    if (test == "트리거") fixedBelt.GetComponent<BoxCollider2D>().isTrigger = true;
                    if (test == "오프셋") fixedBelt.GetComponent<BoxCollider2D>().offset = new Vector2(1.5f, 0);
                    if (test == "회전") fixedBelt.transform.eulerAngles = new Vector3(0, 0, 30);
                    if (test == "다중") fixedBelt.gameObject.AddComponent<CircleCollider2D>();
                    if (test == "지형혼합") fixedBelt.gameObject.AddComponent<QuantumStageTerrainToggle>();
                    if (test == "트랩종류")
                    {
                        var motion = new SerializedObject(movingBelt.GetComponent<QuantumStageMotion>());
                        motion.FindProperty("kind").enumValueIndex = (int)QuantumMotionKind.MovingTrap;
                        motion.ApplyModifiedPropertiesWithoutUndo();
                    }
                    if (test == "스위치참조")
                    {
                        var button = new SerializedObject(root.GetComponentInChildren<QuantumStageSwitch>());
                        button.FindProperty("target").objectReferenceValue = root.GetComponentInChildren<QuantumStageTerrainToggle>();
                        button.ApplyModifiedPropertiesWithoutUndo();
                    }
                    bool rejected = false;
                    try { QuantumOfficialStageWriter.Read(root); }
                    catch (InvalidOperationException error) { rejected = error.Message.Contains("위치=") || error.Message.Contains("/"); }
                    Require(rejected, "작성기 " + test);
                    results.Add("작성기 거부 " + test);
                }
                finally { SceneManager.SetActiveScene(previous); EditorSceneManager.CloseScene(scene, true); }
            }
            File.WriteAllText("Logs/QuantumConveyorContent.txt", "통과\n" + string.Join("\n", results));
        }
        finally { UnityEngine.Object.DestroyImmediate(terrain); }
    }

    public static Quantum.Input InputFor(Frame frame, int tick, int player)
    {
        if (frame == null || frame.FlipFriendsMotionStartedFrame == 0) return default;
        int age = tick - frame.FlipFriendsMotionStartedFrame;
        var players = frame.Filter<Transform2D, FlipFriendsPlayer>();
        while (players.Next(out _, out var transform, out var state))
        {
            if ((int)state.Player != player || state.IsFinished) continue;
            // 1P가 먼저 머리로 벨트 버튼을 눌러 수동 벨트를 오른쪽으로 바꾼 뒤 둘이 함께 출발한다.
            if (age < 80) return new Quantum.Input { Jump = state.HomePosition.X == -3 && age >= 45 && age < 65 };
            FP gap = QuantumConveyorSetup.GoalX - transform.Position.X;
            return new Quantum.Input
            {
                Horizontal = FPMath.Clamp(gap * 3 - state.Velocity.X * 2, -FP._1, FP._1),
                Vertical = FPMath.Abs(gap) < FP._0_50 ? 1 : 0,
            };
        }
        return default;
    }

    private static string State(Frame frame)
    {
        var parts = new List<string>();
        var belts = frame.Filter<FlipFriendsConveyor>();
        while (belts.Next(out _, out var belt)) parts.Add($"벨트{belt.ContentIndex}:시계={belt.Clockwise},세대={belt.Revision},홀짝={belt.Cycle}");
        var motions = frame.Filter<FlipFriendsMotion>();
        while (motions.Next(out _, out var motion))
            if (frame.RuntimeConfig.Motions[motion.ContentIndex].Kind == 4) parts.Add($"지형{motion.ContentIndex}:판정={motion.Collidable},세대={motion.Revision}");
        var buttons = frame.Filter<FlipFriendsStageSwitch>();
        while (buttons.Next(out _, out var button)) parts.Add($"스위치{button.ContentIndex}:누름={button.Active},접촉={button.Contacts}");
        return string.Join(" ", parts) + $" 완료={frame.FlipFriendsCompletedFrame}";
    }

    // 플레이어별 접촉 벨트와 적용 외부 속도(raw)는 매 틱 바뀌므로 확정 프레임마다 별도로 기록한다.
    private static string Contacts(Frame frame)
    {
        var parts = new List<string>();
        var players = frame.Filter<FlipFriendsPlayer>();
        while (players.Next(out _, out var p)) parts.Add($"P{(int)p.Player}:벨트={p.ConveyorContact - 1},외부={p.ConveyorVelocityX.RawValue}");
        return string.Join(" ", parts);
    }

    public static async void Run()
    {
        var session = new QuantumGameSession();
        QuantumRunner replay = null;
        Status = "실행 중";
        var expected = new Dictionary<int, ulong>();
        var actual = new Dictionary<int, ulong>();
        var trace = new List<string>();
        var replayTrace = new List<string>();
        var contacts = new SortedDictionary<int, string>();
        var replayContacts = new Dictionary<int, string>();
        var touched = new HashSet<int>();
        FP maxExternal = 0;
        string previous = null, previousReplay = null;
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
                if (isRecord) contacts[frame.Number] = Contacts(frame); else replayContacts[frame.Number] = Contacts(frame);
                if (isRecord)
                {
                    var players = frame.Filter<Transform2D, FlipFriendsPlayer>();
                    while (players.Next(out _, out var t, out var p))
                    {
                        Require(!FlipFriendsMotionSystem.Overlaps(frame, t.Position + FlipFriendsInteractionSystem.Offset(p), FlipFriendsInteractionSystem.HalfSize(p)), "벨트/장치 비관통 " + frame.Number);
                        if (p.ConveyorContact > 0) touched.Add(p.ConveyorContact - 1);
                        maxExternal = FPMath.Max(maxExternal, FPMath.Abs(p.ConveyorVelocityX));
                    }
                }
                if (frame.Number < ChecksumInterval) return;
                string state = State(frame);
                if (isRecord) { if (state != previous) trace.Add($"{frame.Number} {state}"); previous = state; }
                else { if (state != previousReplay) replayTrace.Add($"{frame.Number} {state}"); previousReplay = state; }
            }))
            {
                await session.StartLocalAsync(Config());
                session.Runner.IsSessionUpdateDisabled = true;
                for (int i = 0; i < Horizon + 200 && !expected.ContainsKey(Horizon); i++) session.Runner.Service(1f / 60);
                var final = session.Runner.Game.Frames.Verified;
                int complete = final.FlipFriendsCompletedFrame;
                var configs = final.RuntimeConfig.Conveyors;
                int manual = Array.FindIndex(configs, c => c.Group == "belt-manual"), mixed = Array.FindIndex(configs, c => c.Group == "mixed");
                int Revision(int index)
                {
                    var q = final.Filter<FlipFriendsConveyor>();
                    while (q.Next(out _, out var b)) if (b.ContentIndex == index) return b.Revision;
                    return -1;
                }
                Require(complete > 0 && Revision(manual) == 1 && Revision(mixed) >= 2, $"완료={complete} 수동벨트세대={Revision(manual)} 혼합벨트세대={Revision(mixed)}");
                Require(touched.Count >= 3 && maxExternal > FP._0_50, $"벨트 접촉={string.Join(",", touched)} 최대외부속도={maxExternal}");
                var record = session.Runner.Game.GetRecordedReplay(includeChecksums: true);
                var serializer = new QuantumUnityJsonSerializer();
                replay = QuantumRunner.StartGame(new SessionRunner.Arguments
                {
                    RunnerId = "FF_CONVEYOR_REPLAY", RuntimeConfig = serializer.ConfigFromByteArray<RuntimeConfig>(record.RuntimeConfigData.Decode(), compressed: true),
                    SessionConfig = record.DeterministicConfig, ReplayProvider = record.CreateInputProvider(),
                    InitialTick = record.InitialTick, FrameData = record.InitialFrameData, GameMode = DeterministicGameMode.Replay, PlayerCount = 2,
                });
                replay.IsSessionUpdateDisabled = true;
                for (int i = 0; i < Horizon * 2 && !actual.ContainsKey(Horizon); i++) replay.Service(i % 2 == 0 ? 1f / 30 : 1f / 120);
                Require(expected.Count == 20 && expected.Count == actual.Count &&
                    expected.All(p => actual.TryGetValue(p.Key, out ulong checksum) && checksum == p.Value),
                    $"입력 기록/재생 확정 체크섬 (기록 {expected.Count}, 재생 {actual.Count})");
                Require(trace.SequenceEqual(replayTrace), "벨트 방향·스위치·지형·완료 전이 프레임");
                // 두 러너의 첫 확정 프레임은 시작 틱 처리에 따라 다를 수 있어 기록 쪽의 체크섬 간격 이후 모든 프레임을 비교한다.
                var compared = contacts.Where(p => p.Key >= ChecksumInterval).ToArray();
                Require(compared.Length > 0 && compared.All(p => replayContacts.TryGetValue(p.Key, out var value) && value == p.Value), "확정 프레임별 벨트 접촉·적용 외부 속도");
                File.WriteAllText("Logs/QuantumConveyorReplay.json", JsonUtility.ToJson(record));
                File.WriteAllLines("Logs/QuantumConveyorChecksums.txt", expected.Select(p => $"{p.Key}: {p.Value}"));
                int direct = QuantumConveyorDirectChecks.Run(final);
                Require(errors == 0, "Unity 오류0");
                Status = $"통과: 별도 재생 확정20개·접촉/외부속도 {contacts.Count(p => p.Key >= ChecksumInterval)}프레임 일치·벨트 접촉{touched.Count}종·최대외부속도={maxExternal}·수동/혼합 방향 전이·전원 완료={complete}·직접{direct}종·확정2400={expected[Horizon]}";
            }
        }
        catch (Exception error) { Status = "실패: " + error; Debug.LogException(error); }
        finally
        {
            Application.logMessageReceived -= log;
            if (replay != null) await replay.ShutdownAsync();
            await session.StopAsync();
            File.WriteAllLines("Logs/QuantumConveyorTrace.txt", trace);
            File.WriteAllLines("Logs/QuantumConveyorReplayTrace.txt", replayTrace);
            File.WriteAllLines("Logs/QuantumConveyorContacts.txt", contacts.Select(p => p.Key + " " + p.Value));
            File.WriteAllText("Logs/QuantumConveyorChecks.txt", Status);
        }
    }

    public static void RunMain() =>
        QuantumMotionChecks.RunOfficialMain(QuantumConveyorSetup.StageId, InputFor, "QuantumConveyorMain");

    public static void RunCloud() =>
        QuantumMotionChecks.RunOfficialCloud(Config, InputFor, frame => State(frame) + " " + Contacts(frame), "QuantumConveyorCloud");
}
