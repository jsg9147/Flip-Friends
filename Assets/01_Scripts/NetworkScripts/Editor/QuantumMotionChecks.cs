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
using LogType = UnityEngine.LogType;
using Button = UnityEngine.UI.Button;

public static class QuantumMotionChecks
{
    public static string Status { get; private set; } = "미실행";
    public static string IntegrationStatus { get; private set; } = "미실행";
    private const int Horizon = 2400;
    public static RuntimeConfig Config()
    {
        var c = new RuntimeConfig(); QuantumGameContent.Prepare(AssetDatabase.LoadAssetAtPath<QuantumOfficialStageAsset>(QuantumMotionSetup.StagePath), c); return c;
    }
    private static void Require(bool valid, string why) { if (!valid) throw new InvalidOperationException("이동 장치 검사: " + why); }

    public static void RunContent()
    {
        var read = QuantumOfficialStageWriter.ReadPrefab(QuantumMotionSetup.PrefabPath);
        var terrain = ScriptableObject.CreateInstance<QuantumMapTerrain>();
        var results = new List<string>();
        try
        {
            terrain.Configure(read.Terrain, read.TerrainBounds, read.TerrainSignature);
            var asset = AssetDatabase.LoadAssetAtPath<QuantumOfficialStageAsset>(QuantumMotionSetup.StagePath);
            QuantumMapConversion Convert() => QuantumOfficialStageConverter.Convert(read.Data, terrain, null);
            Require(Convert().ContentHash == asset.ContentHash && Convert().Motions.Count == 7 && Convert().MotionResets.Count == 1, "작성/재로드/해시/7장치/리셋");
            Require(Config().CustomMapAgreement.Contains(":제품12:") && QuantumGameContent.NetworkVersion == "ff-product-12", "제품12 합의");
            read.Data.motions = read.Data.motions.Reverse().ToArray(); Require(Convert().ContentHash == asset.ContentHash, "ID 정렬 해시");
            var s = read.Data.motions.First(m => m.kind == 0);
            float saved = s.speed; s.speed += .125f; Require(Convert().ContentHash != asset.ContentHash, "속도 해시"); s.speed = saved;
            var point = s.waypoints[1]; s.waypoints[1] += Vector2.up; Require(Convert().ContentHash != asset.ContentHash, "경유점 해시"); s.waypoints[1] = point;
            results.Add("프리팹 재로드·ID순 독립·속도/경유점 해시·제품12");
            foreach (var test in new[] { "속도", "대기", "경유점", "크기", "랜덤", "참조" })
            {
                var copy = JsonUtility.FromJson<QuantumOfficialStageData>(JsonUtility.ToJson(read.Data));
                var motion = copy.motions.First(m => m.kind == 0);
                if (test == "속도") motion.speed = float.NaN;
                if (test == "대기") motion.waitTime = -1;
                if (test == "경유점") motion.waypoints[0] = Vector2.one;
                if (test == "크기") motion.size = Vector2.zero;
                if (test == "랜덤") motion.randomRotation = true;
                if (test == "참조") copy.motionResets[0].target = "missing";
                bool rejected = false; try { QuantumOfficialStageConverter.Convert(copy, terrain, null); } catch (InvalidOperationException e) { rejected = e.Message.Contains("위치="); }
                Require(rejected, test + " 위치/원인 거부"); results.Add("변환 거부 " + test);
            }
            foreach (var test in new[] { "회전", "다중", "트리거", "오프셋", "물리", "원본", "참조" })
            {
                var previous = SceneManager.GetActiveScene(); var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive); SceneManager.SetActiveScene(scene);
                try
                {
                    var root = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(QuantumMotionSetup.PrefabPath));
                    var target = root.GetComponentsInChildren<QuantumStageMotion>().First(m => m.name == "왕복 발판");
                    if (test == "회전") target.transform.eulerAngles = new Vector3(0, 0, 30);
                    if (test == "다중") target.gameObject.AddComponent<CircleCollider2D>();
                    if (test == "트리거") target.GetComponent<BoxCollider2D>().isTrigger = true;
                    if (test == "오프셋") target.GetComponent<BoxCollider2D>().offset = Vector2.up;
                    if (test == "물리") target.gameObject.AddComponent<Rigidbody2D>();
                    if (test == "원본") { target.gameObject.AddComponent<Mirror.NetworkIdentity>(); target.gameObject.AddComponent<MovingTrap>(); }
                    if (test == "참조") { var b = root.GetComponentInChildren<QuantumStageMotionReset>(); var so = new SerializedObject(b); so.FindProperty("target").objectReferenceValue = null; so.ApplyModifiedPropertiesWithoutUndo(); }
                    bool rejected = false; try { QuantumOfficialStageWriter.Read(root); } catch (InvalidOperationException) { rejected = true; }
                    Require(rejected, "작성기 " + test); results.Add("작성기 거부 " + test);
                }
                finally { SceneManager.SetActiveScene(previous); EditorSceneManager.CloseScene(scene, true); }
            }
            File.WriteAllText("Logs/QuantumMotionContent.txt", "통과\n" + string.Join("\n", results));
        }
        finally { UnityEngine.Object.DestroyImmediate(terrain); }
    }

    public static Quantum.Input InputFor(Frame frame, int tick, int player)
    {
        if (frame == null) return default;
        if (frame.FlipFriendsMotionStartedFrame == 0) return default;
        tick -= frame.FlipFriendsMotionStartedFrame;
        var q = frame.Filter<Transform2D, FlipFriendsPlayer>();
        while (q.Next(out _, out var t, out var p))
        {
            if ((int)p.Player != player || p.IsFinished) continue;
            if (tick < 160)
            {
                if (player == 0 && tick < 40) return new Quantum.Input { Horizontal = -FP.FromString("0.001"), Interact = tick >= 6 && tick < 12 };
                return default;
            }
            var gap = (FP)16 - t.Position.X;
            return new Quantum.Input { Horizontal = FPMath.Clamp(gap * 3 - p.Velocity.X * 2, -FP._1, FP._1),
                Run = p.DamageCount > 0,
                Jump = tick < 190 || p.IsGrounded && tick % (p.DamageCount > 0 ? 90 : 120) < 20 && t.Position.X < 14,
                Vertical = FPMath.Abs(gap) < FP._0_50 ? 1 : 0 };
        }
        return default;
    }

    public static async void Run()
    {
        int errors = 0; Application.LogCallback log = (_, _, t) => { if (t == LogType.Error || t == LogType.Exception || t == LogType.Assert) errors++; }; Application.logMessageReceived += log;
        try { Require(EditorApplication.isPlaying, "Play 상태 필요"); Status = "재생 검사 중"; Status = "통과: " + await Replay(); Require(errors == 0, "Unity 오류0"); }
        catch (Exception e) { Status = "실패: " + e; Debug.LogException(e); }
        finally { Application.logMessageReceived -= log; File.WriteAllText("Logs/QuantumMotionChecks.txt", Status); }
    }

    private static async Task<string> Replay()
    {
        var session = new QuantumGameSession(); QuantumRunner replay = null;
        session.InputSource = (tick, slot) => InputFor(session.Runner?.Game.Frames.Verified, tick, slot);
        var expected = new Dictionary<int, ulong>(); var actual = new Dictionary<int, ulong>();
        var trace = new List<string>(); bool multi = false, held = false, jump = false; int completed = 0, damage = 0;
        try
        {
            using (QuantumCallback.SubscribeManual<CallbackSimulateFinished>(c =>
            {
                if (!c.Frame.IsVerified || c.Frame.Number > Horizon) return;
                if (c.Game == session.Runner?.Game)
                {
                    var f = c.Frame; var q = f.Filter<Transform2D, FlipFriendsPlayer>(); int riding = 0;
                    while (q.Next(out _, out var t, out var p))
                    {
                        Require(!FlipFriendsMotionSystem.Overlaps(f, t.Position + FlipFriendsInteractionSystem.Offset(p), FlipFriendsInteractionSystem.HalfSize(p)), "발판 비관통 " + f.Number);
                        held |= p.Held.IsValid && f.Number > 60; damage = Math.Max(damage, p.DamageCount);
                        if (f.Number == f.FlipFriendsMotionStartedFrame + 150 && t.Position.X > ((int)p.Player == 0 ? -1 : 1) + FP._0_50) riding++;
                        if (f.Number > f.FlipFriendsMotionStartedFrame + 160 && f.Number < f.FlipFriendsMotionStartedFrame + 200 && p.Velocity.Y > 0 && !p.IsGrounded) jump = true;
                        if (f.Number % 120 == 0 || f.Number < 180 && f.Number % 30 == 0) trace.Add($"{f.Number} P{(int)p.Player} {t.Position} 운반={p.Held.IsValid} 들기={p.PickupCount} 피해={p.DamageCount} 도착={p.IsFinished}");
                    }
                    if (riding == 2) multi = true;
                    if (f.Number % 120 == 0) expected[f.Number] = f.CalculateChecksum();
                    completed = f.FlipFriendsCompletedFrame;
                }
                if (c.Game == replay?.Game && c.Frame.Number % 120 == 0) actual[c.Frame.Number] = c.Frame.CalculateChecksum();
            }))
            {
                await session.StartLocalAsync(Config()); session.Runner.IsSessionUpdateDisabled = true;
                for (int i = 0; i < Horizon + 200 && !expected.ContainsKey(Horizon); i++) session.Runner.Service(1f / 60);
                File.WriteAllText("Logs/QuantumMotionTrace.txt", string.Join("\n", trace));
                int direct = QuantumMotionDirectChecks.Run(session.Runner.Game.Frames.Verified);
                Require(multi && jump && held && completed > 0, $"다중승객={multi} 점프분리={jump} 운반={held} 완료={completed}");
                var record = session.Runner.Game.GetRecordedReplay(includeChecksums: true); var serializer = new QuantumUnityJsonSerializer();
                replay = QuantumRunner.StartGame(new SessionRunner.Arguments { RunnerId = "FF_MOTION_REPLAY", RuntimeConfig = serializer.ConfigFromByteArray<RuntimeConfig>(record.RuntimeConfigData.Decode(), compressed: true),
                    SessionConfig = record.DeterministicConfig, ReplayProvider = record.CreateInputProvider(), InitialTick = record.InitialTick, FrameData = record.InitialFrameData, GameMode = DeterministicGameMode.Replay, PlayerCount = 2 });
                replay.IsSessionUpdateDisabled = true;
                for (int i = 0; i < Horizon * 2 && !actual.ContainsKey(Horizon); i++) replay.Service(i % 2 == 0 ? 1f / 30 : 1f / 120);
                Require(expected.Count == 20 && expected.Count == actual.Count && expected.All(p => actual[p.Key] == p.Value), "확정 체크섬 일치");
                File.WriteAllText("Logs/QuantumMotionReplay.json", JsonUtility.ToJson(record)); File.WriteAllText("Logs/QuantumMotionChecksums.txt", string.Join("\n", expected.Select(p => p.Key + ": " + p.Value)));
                return $"기록/별도 러너 재생20개·다중승객/운반/점프분리·완료={completed}·피격={damage}·직접{direct}종·확정2400={expected[Horizon]}";
            }
        }
        finally { if (replay != null) await replay.ShutdownAsync(); await session.StopAsync(); }
    }

    public static void RunMain() => RunOfficialMain(QuantumMotionSetup.StageId, InputFor, "QuantumMotionMain");

    internal static async void RunOfficialMain(string stageId, Func<Frame, int, int, Quantum.Input> input, string logName)
    {
        try
        {
            IntegrationStatus = "Main 검사 중"; var flow = QuantumGameFlow.Instance;
            var stage = AssetDatabase.LoadAssetAtPath<QuantumOfficialStageAsset>($"{QuantumOfficialStageWriter.ContentRoot}/{stageId}/Stage.asset");
            void Select()
            {
                if (UnityEngine.Object.FindAnyObjectByType<ScreenNavigator>().CurrentScreen.ScreenId != "quantum-prepare") Click("Start Button");
                for (int i = 0; i <= flow.OfficialStages.Count; i++) { Click("공식 스테이지 선택"); if (flow.SelectedContent == (IQuantumMapContent)stage) return; }
                Require(false, "Main 공식 시험 선택 " + stageId);
            }
            Select(); Click("오프라인 시작"); await Wait(() => flow.State == QuantumGameState.Playing, 10000);
            var session = flow.Session; session.InputSource = (tick, slot) => input(session.Runner?.Game.Frames.Verified, tick, slot);
            await Wait(() => flow.State == QuantumGameState.Ready && SceneManager.GetActiveScene().name == "Main", 90000);
            Require(!QuantumRunner.ActiveRunners.Any() && QuantumGameSession.ActiveInputSubscriptions == 0, "완료 복귀/정리");
            Select(); Click("오프라인 시작"); await Wait(() => flow.State == QuantumGameState.Playing, 10000);
            Click("플레이 종료"); await Wait(() => flow.State == QuantumGameState.Ready, 10000);
            Require(!QuantumRunner.ActiveRunners.Any() && QuantumGameSession.ActiveInputSubscriptions == 0, "재진입 종료/정리");
            IntegrationStatus = "통과: Main 공식 선택·플레이·전원 완료·복귀·재진입·종료·러너/입력0";
        }
        catch (Exception e) { IntegrationStatus = "실패: " + e; Debug.LogException(e); }
        finally { File.WriteAllText("Logs/" + logName + ".txt", IntegrationStatus); }
    }

    public static void RunCloud() => RunOfficialCloud(Config, InputFor, MotionState, "QuantumMotionCloud");

    private static string MotionState(Frame frame)
    {
        var q = frame.Filter<FlipFriendsMotion>();
        var parts = new List<string>();
        while (q.Next(out _, out var m)) parts.Add($"{m.ContentIndex}:{m.Position}/{m.Angle}/{m.RotationSpeed}/{m.Revision}/{m.Phase}");
        return string.Join(" ", parts);
    }

    internal static async void RunOfficialCloud(Func<RuntimeConfig> config, Func<Frame, int, int, Quantum.Input> input,
        Func<Frame, string> state, string logName)
    {
        var sessions = new[] { new QuantumGameSession(), new QuantumGameSession() };
        var checks = new[] { new Dictionary<int, ulong>(), new Dictionary<int, ulong>() };
        var states = new[] { new Dictionary<int, string>(), new Dictionary<int, string>() };
        try
        {
            IntegrationStatus = "Photon 검사 중";
            foreach (var s in sessions) s.InputSource = (tick, slot) => { var local = s.Runner?.Game.GetLocalPlayers(); return local == null || slot >= local.Count ? default : input(s.Runner.Game.Frames.Verified, tick, (int)local[slot]); };
            using (QuantumCallback.SubscribeManual<CallbackSimulateFinished>(c =>
            {
                if (!c.Frame.IsVerified || c.Frame.Number > Horizon || c.Frame.Number % 120 != 0) return;
                for (int i = 0; i < 2; i++) if (c.Game == sessions[i].Runner?.Game)
                {
                    checks[i][c.Frame.Number] = c.Frame.CalculateChecksum();
                    states[i][c.Frame.Number] = state(c.Frame);
                }
            }))
            {
                string room = "ff-stage-" + Guid.NewGuid().ToString("N"); var first = sessions[0].StartDevelopmentAsync(config(), room, false);
                await Wait(() => sessions[0].Client?.InRoom == true || first.IsFaulted, 15000); if (first.IsFaulted) await first;
                await Task.WhenAll(first, sessions[1].StartDevelopmentAsync(config(), room, true));
                await Wait(() => checks.All(c => c.ContainsKey(Horizon)) && sessions.All(s => s.Runner.Game.Frames.Verified.FlipFriendsCompletedFrame > 0), 120000);
                var samples = checks[0].Where(p => p.Key >= 240).ToArray();
                Require(samples.Length == 19 && samples.All(p => checks[1].TryGetValue(p.Key, out var h) && p.Value == h), "Photon 확정 체크섬");
                Require(states[0].All(p => states[1][p.Key] == p.Value), "Photon 장치 상태");
                int complete = sessions[0].Runner.Game.Frames.Verified.FlipFriendsCompletedFrame;
                Require(complete == sessions[1].Runner.Game.Frames.Verified.FlipFriendsCompletedFrame, "Photon 완료 프레임");
                IntegrationStatus = $"통과: 동일PC 두 연결·지역={sessions[0].Client.CurrentRegion}·RTT={sessions[0].Client.RealtimePeer.Stats.RoundtripTime}/{sessions[1].Client.RealtimePeer.Stats.RoundtripTime}ms·확정19개·장치 상태 일치·완료={complete}·지연/손실 주입 없음\n" + string.Join("\n", samples.Select(p => p.Key + ": " + p.Value + " " + states[0][p.Key]));
            }
        }
        catch (Exception e) { IntegrationStatus = "실패: " + e; Debug.LogException(e); }
        finally { foreach (var s in sessions) await s.StopAsync(); IntegrationStatus += "\n러너/연결/입력 정리=" + sessions.All(s => s.Runner == null && s.Client == null && !s.HasInputSubscription); File.WriteAllText("Logs/" + logName + ".txt", IntegrationStatus); }
    }

    private static void Click(string name) => UnityEngine.Object.FindObjectsByType<Button>(FindObjectsInactive.Exclude).Single(b => b.name == name).onClick.Invoke();
    private static async Task Wait(Func<bool> ready, int timeout)
    { var end = DateTime.UtcNow.AddMilliseconds(timeout); while (!ready() && DateTime.UtcNow < end && EditorApplication.isPlaying) await Task.Delay(20); Require(ready(), "비동기 제한시간"); }
}
