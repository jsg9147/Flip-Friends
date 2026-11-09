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

// 공식 스테이지 변환 경로(프리팹→작성기→목록→Main 선택→재생/Photon)의 검사.
public static class QuantumOfficialStageChecks
{
    public const string StagePath = QuantumOfficialStageWriter.ContentRoot + "/" + QuantumOfficialTestStageSetup.StageId + "/Stage.asset";
    private const int RecordTicks = 1200;
    private const int MainTimeoutMs = 55000;
    public static string Status { get; private set; } = "미실행";

    public static QuantumOfficialStageAsset Stage() => AssetDatabase.LoadAssetAtPath<QuantumOfficialStageAsset>(StagePath);

    public static RuntimeConfig Config()
    {
        var config = new RuntimeConfig(); QuantumGameContent.Prepare(Stage(), config); return config;
    }

    // 확정 프레임만 보고 입력을 정하므로 같은 입력 기록을 재생하면 같은 결과가 나와야 한다.
    public static Quantum.Input InputFor(Frame frame, int tick, int player)
    {
        if (frame == null || tick < 120) return default;
        if (player == 0 && tick < 360) return default;
        var query = frame.Filter<Transform2D, FlipFriendsPlayer>();
        while (query.Next(out _, out var t, out var p))
        {
            if ((int)p.Player != player || p.IsFinished) continue;
            var input = new Quantum.Input();
            var goal = frame.RuntimeConfig.GoalCenters[0];
            FP target = goal.X;
            bool mayJump = player == 1 || p.SectionCount > 0;
            if (player == 0 && p.SectionCount == 0)
            {
                if (!p.Held.IsValid)
                {
                    var boxes = frame.Filter<Transform2D, FlipFriendsBox>(); boxes.Next(out _, out var bt, out _);
                    // 상자는 1P 뒤(왼쪽)에 있어 2P 경로를 막지 않는다. 오른쪽에서 다가가 왼쪽을 보고 든다.
                    target = bt.Position.X + FP.FromString("0.9");
                    FP gap = t.Position.X - bt.Position.X;
                    input.Interact = tick % 30 < 5;
                    if (gap >= FP.FromString("0.85") && gap < FP.FromString("0.98")) { input.Horizontal = FP.FromString("-0.001"); return input; }
                }
                else target = FP.FromString("120");
            }
            input.Horizontal = FPMath.Clamp((target - t.Position.X) * 4 - p.Velocity.X * 2, -FP._1, FP._1);
            FP x = t.Position.X;
            // 걷기 속도의 점프로는 구덩이 너비를 넘지 못하므로 도착점으로 갈 때 달리기를 쓴다.
            input.Run = mayJump && target == goal.X;
            input.Jump = mayJump && (x > FP.FromString("117.8") && x < FP.FromString("121.5") || x > FP.FromString("130.8") && x < FP.FromString("133.6"));
            input.Vertical = FPMath.Abs(target - x) < FP.FromString("0.3") && target == goal.X ? 1 : 0;
            return input;
        }
        return default;
    }

    public static async void RunStandalone()
    {
        int errors = 0; Application.LogCallback onLog = (_, _, type) => { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors++; };
        Application.logMessageReceived += onLog;
        try { Status = "Play 모드 검사"; await Run(); await CheckMain(); Require(errors == 0, "실행 중 Unity 오류0"); Status += "\nMain 공식 선택/완료/재진입/메뉴종료·Unity 오류0 통과"; }
        catch (Exception error) { Status = "실패: " + error; Debug.LogException(error); }
        finally { Application.logMessageReceived -= onLog; File.WriteAllText("Logs/QuantumOfficialStageChecks.txt", Status); }
    }

    // 임시 씬을 만들어 프리팹을 다시 읽으므로 정지 상태에서만 실행한다.
    public static void RunContent()
    {
        try { if (EditorApplication.isPlaying) throw new InvalidOperationException("정지 상태에서 실행합니다."); CheckContent(); }
        catch (Exception error) { Status = "실패: " + error; Debug.LogException(error); }
        finally { File.WriteAllText("Logs/QuantumOfficialStageContent.txt", Status); }
    }

    private static void CheckContent()
    {
        var stage = Stage(); var config = Config(); var conversion = stage.ValidateAndApply(new RuntimeConfig());
        Require(config.CustomMapAgreement.StartsWith(stage.Agreement + ":제품14:") && stage.Agreement.StartsWith("공식1:"), "공식1/제품14 합의");
        Require(conversion.TerrainContours.Length == 4 && conversion.Devices.Count == 3 && conversion.Devices.Single(d => d.Kind == 1).ContactCenters.Length == 2, "시험 프리팹 윤곽4·장치3·Reset 조각2");
        Require(config.CustomMapSpawns.All(s => s.X > 50) && config.FallMinX > 50, "MapData 배치 범위 밖 공식 배치·렌더러 경계");
        Require(QuantumOfficialStageCatalog.LoadStages().Contains(stage), "공식 목록 등록");
        var half = new FPVector2(FP.FromString("0.4"), FP.FromString("0.5"));
        Require(!FlipFriendsTerrain.Overlaps(config.TerrainContours, new FPVector2(127, FP.FromString("5.5")), new FPVector2(FP.FromString("0.4"), FP.FromString("0.4"))), "섬 안쪽 구멍 보존");
        Require(FlipFriendsTerrain.Overlaps(config.TerrainContours, new FPVector2(125, FP.FromString("4.5")), half), "섬 벽 점유");
        Require(!FlipFriendsTerrain.Overlaps(config.TerrainContours, new FPVector2(120, FP.FromString("-0.45")), half), "바닥 오목 구덩이 보존");

        var read = QuantumOfficialStageWriter.ReadPrefab(QuantumOfficialTestStageSetup.PrefabPath);
        var terrain = AssetDatabase.LoadAssetAtPath<QuantumMapTerrain>(QuantumOfficialStageWriter.ContentRoot + "/official-test/Terrain.asset");
        var devices = AssetDatabase.LoadAssetAtPath<QuantumMapDevices>(QuantumOfficialStageWriter.ContentRoot + "/official-test/Devices.asset");
        string Hash(QuantumOfficialStageData d, QuantumMapTerrain t = null, QuantumMapDevices v = null) => QuantumOfficialStageConverter.Convert(d, t ?? terrain, v ?? devices).ContentHash;
        QuantumOfficialStageData Clone() => JsonUtility.FromJson<QuantumOfficialStageData>(JsonUtility.ToJson(read.Data));
        Require(Hash(read.Data) == stage.ContentHash, "프리팹 재판독 해시 일치");
        var reversed = Clone(); Array.Reverse(reversed.spawns); Array.Reverse(reversed.goals);
        Require(Hash(reversed) == stage.ContentHash, "배치 순서 독립 해시");
        var culture = System.Globalization.CultureInfo.CurrentCulture;
        try { System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("fr-FR"); Require(Hash(read.Data) == stage.ContentHash, "문화권 독립 해시"); }
        finally { System.Globalization.CultureInfo.CurrentCulture = culture; }
        var moved = Clone(); moved.goals[0].center.x += 1; Require(Hash(moved) != stage.ContentHash, "도착 위치 해시 반영");

        var inTerrain = Clone(); inTerrain.spawns[0].y = -1.5f; Reject(() => Hash(inTerrain), "지형 안 시작점");
        var third = Clone(); third.spawns = third.spawns.Append(new Vector2(106, 0.6f)).ToArray(); Reject(() => Hash(third), "시작점 3개");
        var zero = Clone(); zero.goals[0].size = Vector2.zero; Reject(() => Hash(zero), "크기0 도착");
        var nan = Clone(); nan.boxes[0].x = float.NaN; Reject(() => Hash(nan), "비유한 상자 좌표");
        var badId = Clone(); badId.stageId = "Bad ID"; Reject(() => Hash(badId), "잘못된 스테이지 ID");
        Reject(() => QuantumOfficialStageConverter.Convert(read.Data, null, devices), "지형 없는 공식 스테이지");
        var slope = UnityEngine.Object.Instantiate(terrain);
        try { slope.Sources[0].vertices[1] = slope.Sources[0].vertices[0] + new Vector2(0.25f, 1); Reject(() => Hash(read.Data, slope), "45도 초과 경사"); }
        finally { UnityEngine.Object.DestroyImmediate(slope); }

        RejectPrefab(root => root.transform.Find("도착").gameObject.AddComponent<BoxCollider2D>(), "미지원 Collider/컴포넌트");
        RejectPrefab(root => root.transform.Find("상자").gameObject.AddComponent<RespawnHandler>(), "Mirror 원본 장치 스크립트");
        RejectPrefab(root => root.transform.Find("도착").gameObject.SetActive(false), "비활성 작성 컴포넌트");
        RejectPrefab(root => root.transform.Find("도착").rotation = Quaternion.Euler(0, 0, 90), "회전한 도착");
        RejectPrefab(root => UnityEngine.Object.Instantiate(root.transform.Find("Grid/Ground").gameObject, root.transform.Find("Grid")), "Ground Tilemap 2개");
        RejectPrefab(root =>
        {
            var spawner = new SerializedObject(root.GetComponentInChildren<QuantumStageBoxSpawner>());
            spawner.FindProperty("target").objectReferenceValue = null; spawner.ApplyModifiedPropertiesWithoutUndo();
        }, "소환 대상 누락");
        Status = "통과: 공식 콘텐츠·순서/문화권 독립 해시·거부 13종";
    }

    public static async Task Run()
    {
        var session = new QuantumGameSession(); QuantumRunner replay = null;
        var expected = new Dictionary<int, ulong>(); var actual = new Dictionary<int, ulong>(); var trace = new List<string>();
        bool carriedReset = false, checkpoint = false; int completed = 0;
        session.InputSource = (tick, player) => InputFor(session.Runner?.Game.Frames.Verified, tick, player);
        try
        {
            using (QuantumCallback.SubscribeManual<CallbackSimulateFinished>(callback =>
            {
                if (!callback.Frame.IsVerified || callback.Frame.Number > RecordTicks) return;
                if (callback.Game == session.Runner?.Game)
                {
                    var query = callback.Frame.Filter<Transform2D, FlipFriendsPlayer>();
                    while (query.Next(out _, out var t, out var p))
                    {
                        if (!p.RespawnPending && !p.IsFinished)
                            Require(!FlipFriendsTerrain.Overlaps(callback.Frame.RuntimeConfig.TerrainContours, t.Position + FlipFriendsInteractionSystem.Offset(p), FlipFriendsInteractionSystem.HalfSize(p)), "실제 확정 몸체 지형 겹침 없음");
                        carriedReset |= p.SectionCount > 0 && p.Held.IsValid;
                        checkpoint |= p.HasCheckpoint && p.CheckpointId == 1;
                        if (callback.Frame.Number % 30 == 0) trace.Add($"{callback.Frame.Number} P{p.Player} pos={t.Position} v={p.Velocity} section={p.SectionCount} held={p.Held} cp={p.CheckpointId} finished={p.IsFinished}");
                    }
                    if (completed == 0) completed = callback.Frame.FlipFriendsCompletedFrame;
                    if (callback.Frame.Number % 120 == 0) expected[callback.Frame.Number] = callback.Frame.CalculateChecksum();
                }
                if (callback.Game == replay?.Game && callback.Frame.Number % 120 == 0) actual[callback.Frame.Number] = callback.Frame.CalculateChecksum();
            }))
            {
                await session.StartLocalAsync(Config()); session.Runner.IsSessionUpdateDisabled = true;
                for (int i = 0; i < RecordTicks + 300 && !expected.ContainsKey(RecordTicks); i++) session.Runner.Service(1f / 60);
                File.WriteAllText("Logs/QuantumOfficialStageTrace.txt", string.Join("\n", trace));
                Require(carriedReset && checkpoint && completed > 0, $"실제 입력 운반 Reset·체크포인트·전원 도착 (운반Reset={carriedReset}, CP={checkpoint}, 완료={completed})");
                var record = session.Runner.Game.GetRecordedReplay(includeChecksums: true); var serializer = new QuantumUnityJsonSerializer();
                replay = QuantumRunner.StartGame(new SessionRunner.Arguments { RunnerId = "FF_OFFICIAL_REPLAY", RuntimeConfig = serializer.ConfigFromByteArray<RuntimeConfig>(record.RuntimeConfigData.Decode(), compressed: true),
                    SessionConfig = record.DeterministicConfig, ReplayProvider = record.CreateInputProvider(), InitialTick = record.InitialTick, FrameData = record.InitialFrameData, GameMode = DeterministicGameMode.Replay, PlayerCount = 2 });
                replay.IsSessionUpdateDisabled = true;
                for (int i = 0; i < RecordTicks + 300 && !actual.ContainsKey(RecordTicks); i++) replay.Service(i % 2 == 0 ? 1f / 30 : 1f / 120);
                Require(expected.Count == 10 && actual.Count == 10 && expected.All(p => actual[p.Key] == p.Value), "새 입력→새 러너 확정 체크섬10개");
                File.WriteAllText("Logs/QuantumOfficialStageReplay.json", JsonUtility.ToJson(record));
                Status += $"\n통과: 공식 시험 실제 입력 운반Reset·CP·완료{completed}·재생10개\n" + string.Join("\n", expected.Select(p => p.Key + ": " + p.Value));
            }
        }
        finally { if (replay != null) await replay.ShutdownAsync(); await session.StopAsync(); }
    }

    public static async Task CheckMain()
    {
        var flow = QuantumGameFlow.Instance;
        int index = flow.OfficialMapIndex(flow.OfficialStages.ToList().IndexOf(Stage()));
        SelectTest();
        Require(flow.SelectedMap == index && flow.SelectedContent == (IQuantumMapContent)Stage(), "Main 공식 스테이지 버튼 선택");
        Click("오프라인 시작");
        await Wait(() => flow.State == QuantumGameState.Playing, 10000);
        var session = flow.Session; bool carried = false;
        session.InputSource = (tick, slot) => InputFor(session.Runner?.Game.Frames.Verified, tick, slot);
        using (QuantumCallback.SubscribeManual<CallbackSimulateFinished>(c =>
        {
            if (c.Game != session.Runner?.Game || !c.Frame.IsVerified) return;
            var query = c.Frame.Filter<FlipFriendsPlayer>(); while (query.Next(out _, out var p)) carried |= p.SectionCount > 0 && p.Held.IsValid;
        })) await Wait(() => flow.State == QuantumGameState.Ready && SceneManager.GetActiveScene().name == "Main", MainTimeoutMs);
        Require(carried && !QuantumRunner.ActiveRunners.Any() && QuantumGameSession.ActiveInputSubscriptions == 0, "Main 공식 운반Reset·전원도착·정리");
        SelectTest(); Click("오프라인 시작"); await Wait(() => flow.State == QuantumGameState.Playing, 10000);
        Click("플레이 종료"); await Wait(() => flow.State == QuantumGameState.Ready, 10000);
        Require(!QuantumRunner.ActiveRunners.Any() && QuantumGameSession.ActiveInputSubscriptions == 0, "재진입/메뉴종료 정리");
        void SelectTest()
        {
            Click("Start Button");
            for (int i = 0; i < flow.OfficialStages.Count; i++)
            { Click("공식 스테이지 선택"); if (flow.SelectedMap == index) return; }
            Require(false, "시험 공식 목록 순환 선택");
        }
    }

    public static async void RunCloud()
    {
        var sessions = new[] { new QuantumGameSession(), new QuantumGameSession() }; var checks = new[] { new Dictionary<int, ulong>(), new Dictionary<int, ulong>() };
        var carried = new bool[2]; var clients = new Photon.Realtime.RealtimeClient[2];
        try
        {
            foreach (var s in sessions) s.InputSource = (tick, slot) => { var locals = s.Runner?.Game.GetLocalPlayers(); return locals == null || locals.Count <= slot ? default : InputFor(s.Runner.Game.Frames.Verified, tick, (int)locals[slot]); };
            using (QuantumCallback.SubscribeManual<CallbackSimulateFinished>(c =>
            {
                if (!c.Frame.IsVerified) return;
                for (int i = 0; i < 2; i++) if (c.Game == sessions[i].Runner?.Game)
                {
                    if (c.Frame.Number <= RecordTicks && c.Frame.Number % 120 == 0) checks[i][c.Frame.Number] = c.Frame.CalculateChecksum();
                    var query = c.Frame.Filter<FlipFriendsPlayer>(); while (query.Next(out _, out var p)) carried[i] |= p.SectionCount > 0 && p.Held.IsValid;
                }
            }))
            {
                string room = "ff-official-" + Guid.NewGuid().ToString("N"); var first = sessions[0].StartDevelopmentAsync(Config(), room, false);
                await Wait(() => sessions[0].Client?.InRoom == true || first.IsFaulted, 15000); if (first.IsFaulted) await first;
                await Task.WhenAll(first, sessions[1].StartDevelopmentAsync(Config(), room, true)); clients[0] = sessions[0].Client; clients[1] = sessions[1].Client;
                // 이 스테이지는 확정 1200 전에 끝날 수 있으므로 완료와 비교 구간 끝을 함께 기다린다.
                await Wait(() => sessions.All(s => s.Runner.Game.Frames.Verified.FlipFriendsCompletedFrame > 0) && checks.All(c => c.ContainsKey(RecordTicks)), 60000);
                var samples = checks[0].Where(p => p.Key >= 240).ToArray();
                Require(samples.Length == 9 && samples.All(p => checks[1].TryGetValue(p.Key, out var h) && p.Value == h), $"Photon 확정9개 일치 (표본={samples.Length})");
                Require(carried.All(v => v), $"Photon 양쪽 운반Reset ({carried[0]}/{carried[1]})");
                Require(sessions[0].Runner.Game.Frames.Verified.FlipFriendsCompletedFrame == sessions[1].Runner.Game.Frames.Verified.FlipFriendsCompletedFrame, "Photon 완료 프레임 일치");
                Status = $"통과: 공식 시험 Photon 동일 PC 두 연결, 지역={clients[0].CurrentRegion}, RTT={clients[0].RealtimePeer.Stats.RoundtripTime}/{clients[1].RealtimePeer.Stats.RoundtripTime}ms, 지연/손실 주입 없음, 완료={sessions[0].Runner.Game.Frames.Verified.FlipFriendsCompletedFrame}\n" + string.Join("\n", samples.Select(p => p.Key + ": " + p.Value));
            }
        }
        catch (Exception error) { Status = "실패: " + error; Debug.LogException(error); }
        finally
        {
            foreach (var s in sessions) await s.StopAsync();
            Status += "\n러너/연결/입력 정리=" + sessions.All(s => s.Runner == null && s.Client == null && !s.HasInputSubscription);
            File.WriteAllText("Logs/QuantumOfficialStageCloud.txt", Status);
        }
    }

    private static void RejectPrefab(Action<GameObject> mutate, string label)
    {
        var previous = SceneManager.GetActiveScene();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive); SceneManager.SetActiveScene(scene);
        try
        {
            var root = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(QuantumOfficialTestStageSetup.PrefabPath));
            QuantumOfficialStageWriter.Read(root);
            mutate(root); Physics2D.SyncTransforms();
            Reject(() => QuantumOfficialStageWriter.Read(root), label);
        }
        finally { SceneManager.SetActiveScene(previous); EditorSceneManager.CloseScene(scene, true); }
    }

    private static void Reject(Action action, string label)
    {
        bool rejected = false;
        try { action(); } catch (InvalidOperationException) { rejected = true; }
        Require(rejected, "거부: " + label);
    }

    private static void Click(string name) => UnityEngine.Object.FindObjectsByType<Button>(FindObjectsInactive.Exclude).Single(b => b.name == name).onClick.Invoke();
    private static async Task Wait(Func<bool> ready, int timeout)
    { var end = DateTime.UtcNow.AddMilliseconds(timeout); while (!ready() && DateTime.UtcNow < end && EditorApplication.isPlaying) await Task.Delay(20); Require(ready(), "비동기 제한시간"); }
    private static void Require(bool ok, string label) { if (!ok) throw new InvalidOperationException("공식 스테이지 검사: " + label); }
}
