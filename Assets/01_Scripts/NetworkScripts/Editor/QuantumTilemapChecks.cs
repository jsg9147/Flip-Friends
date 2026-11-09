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

public static class QuantumTilemapChecks
{
    public static string Status { get; private set; } = "미실행";
    public static RuntimeConfig Config()
    {
        var config = new RuntimeConfig();
        QuantumGameContent.Prepare(AssetDatabase.LoadAssetAtPath<QuantumCustomMapAsset>(QuantumTilemapSetup.ManifestPath), config); return config;
    }
    public static Quantum.Input InputFor(Frame frame, int tick, int player, bool finish = false)
    {
        if (frame == null || tick < 120) return default;
        var query = frame.Filter<Transform2D, FlipFriendsPlayer>();
        while (query.Next(out _, out var t, out var p))
        {
            if ((int)p.Player != player) continue;
            if (player == 0 && tick < 360) return default;
            FP target = finish ? -10 : player == 1 ? -8 : p.SectionCount > 0 ? 5 : 18;
            var input = new Quantum.Input();
            if (player == 0 && p.SectionCount == 0 && !p.Held.IsValid)
            {
                var boxes = frame.Filter<Transform2D, FlipFriendsBox>(); boxes.Next(out _, out var bt, out _);
                target = bt.Position.X - FP.FromString("0.9");
                FP gap = bt.Position.X - t.Position.X;
                input.Interact = tick % 30 < 5;
                if (gap >= FP.FromString("0.85") && gap < FP.FromString("0.98"))
                { input.Horizontal = FP.FromString("0.001"); return input; }
            }
            input.Horizontal = FPMath.Clamp((target - t.Position.X) * 4 - p.Velocity.X * 2, -FP._1, FP._1);
            input.Vertical = finish && FPMath.Abs(target - t.Position.X) < FP.FromString("0.3") ? 1 : 0;
            input.Jump = player == 1 && tick < 360 && tick % 90 < 45;
            return input;
        }
        return default;
    }
    public static async void RunStandalone()
    {
        int errors = 0; Application.LogCallback onLog = (_, _, type) => { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors++; };
        Application.logMessageReceived += onLog;
        try { await Run(); await CheckMain(); Require(errors == 0, "실행 중 Unity 오류0"); Status += "\nMain/재진입/메뉴종료·Unity 오류0 통과"; }
        catch (Exception error) { Status = "실패: " + error; Debug.LogException(error); }
        finally { Application.logMessageReceived -= onLog; File.WriteAllText("Logs/QuantumTilemapChecks.txt", Status); }
    }
    public static async Task Run()
    {
        CheckGeometry();
        var session = new QuantumGameSession(); QuantumRunner replay = null;
        var expected = new Dictionary<int, ulong>(); var actual = new Dictionary<int, ulong>(); var trace = new List<string>(); bool carriedReset = false;
        session.InputSource = (tick, player) => InputFor(session.Runner?.Game.Frames.Verified, tick, player);
        try
        {
            using (QuantumCallback.SubscribeManual<CallbackSimulateFinished>(callback =>
            {
                if (!callback.Frame.IsVerified || callback.Frame.Number > 1200) return;
                if (callback.Game == session.Runner?.Game)
                {
                    var query = callback.Frame.Filter<Transform2D, FlipFriendsPlayer>();
                    while (query.Next(out _, out var t, out var p))
                    {
                        if (!p.RespawnPending && !p.IsFinished)
                            Require(!FlipFriendsTerrain.Overlaps(callback.Frame.RuntimeConfig.TerrainContours, t.Position + FlipFriendsInteractionSystem.Offset(p), FlipFriendsInteractionSystem.HalfSize(p)), "실제 확정 몸체 지형 겹침 없음");
                        carriedReset |= p.SectionCount > 0 && p.Held.IsValid;
                        if (callback.Frame.Number % 30 == 0) trace.Add($"{callback.Frame.Number} P{p.Player} pos={t.Position} v={p.Velocity} section={p.SectionCount} held={p.Held} pending={p.SectionPending}");
                    }
                    if (callback.Frame.Number % 120 == 0) expected[callback.Frame.Number] = callback.Frame.CalculateChecksum();
                }
                if (callback.Game == replay?.Game && callback.Frame.Number % 120 == 0) actual[callback.Frame.Number] = callback.Frame.CalculateChecksum();
            }))
            {
                await session.StartLocalAsync(Config()); session.Runner.IsSessionUpdateDisabled = true;
                for (int i = 0; i < 1500 && !expected.ContainsKey(1200); i++) session.Runner.Service(1f / 60);
                File.WriteAllText("Logs/QuantumTilemapTrace.txt", string.Join("\n", trace)); Require(carriedReset, "실제 입력 상자 운반 Tilemap Reset");
                var record = session.Runner.Game.GetRecordedReplay(includeChecksums: true); var serializer = new QuantumUnityJsonSerializer();
                replay = QuantumRunner.StartGame(new SessionRunner.Arguments { RunnerId = "FF_TILE_REPLAY", RuntimeConfig = serializer.ConfigFromByteArray<RuntimeConfig>(record.RuntimeConfigData.Decode(), compressed: true),
                    SessionConfig = record.DeterministicConfig, ReplayProvider = record.CreateInputProvider(), InitialTick = record.InitialTick, FrameData = record.InitialFrameData, GameMode = DeterministicGameMode.Replay, PlayerCount = 2 });
                replay.IsSessionUpdateDisabled = true;
                for (int i = 0; i < 1500 && !actual.ContainsKey(1200); i++) replay.Service(i % 2 == 0 ? 1f / 30 : 1f / 120);
                Require(expected.Count == 10 && actual.Count == 10 && expected.All(p => actual[p.Key] == p.Value), "새 입력→새 러너 확정 체크섬10개");
                File.WriteAllText("Logs/QuantumTilemapReplay.json", JsonUtility.ToJson(record));
                CheckFrame(session.Runner.Game.Frames.Verified);
                Status = "통과: 원본 윤곽/구멍/경계·Reset 조각/운반/대기·입력 재생10개\n" + string.Join("\n", expected.Select(p => p.Key + ": " + p.Value));
            }
        }
        finally { if (replay != null) await replay.ShutdownAsync(); await session.StopAsync(); }
    }
    private static void CheckGeometry()
    {
        var config = Config(); var contours = config.TerrainContours;
        var half = new FPVector2(FP.FromString("0.4"), FP.FromString("0.5"));
        Require(contours.Length == 7 && contours.Sum(c => c.Vertices.Length) == 28 && config.Devices[0].ContactCenters.Length == 26, "실제 Stage1 윤곽/Stage9 Reset 형상 수");
        Require(!FlipFriendsTerrain.Overlaps(contours, new FPVector2(0, 0), half), "안쪽 구멍 보존");
        Require(FlipFriendsTerrain.Overlaps(contours, new FPVector2(0, FP.FromString("-3.5")), half), "지형 내부 점유 거부");
        Require(FlipFriendsTerrain.Limit(contours, new FPVector2(0, -2), half, -2, false) > -1, "바닥 관통 차단");
        Require(FlipFriendsTerrain.Limit(contours, new FPVector2(19, 0), half, 2, true) < 1, "오른벽 차단");
        Require(FlipFriendsTerrain.Limit(contours, new FPVector2(-19, 0), half, -2, true) > -1, "왼벽 차단");
        Require(FlipFriendsTerrain.Limit(contours, new FPVector2(0, 59), half, 2, false) < 1, "천장 차단");
        foreach (var item in new[] { (new FPVector2(19, 0), 2, true), (new FPVector2(-19, 0), -2, true), (new FPVector2(0, -2), -2, false), (new FPVector2(0, 59), 2, false) })
        {
            var delta = FlipFriendsTerrain.Limit(contours, item.Item1, half, item.Item2, item.Item3);
            var end = item.Item1 + (item.Item3 ? new FPVector2(delta, 0) : new FPVector2(0, delta));
            Require(!FlipFriendsTerrain.Overlaps(contours, end, half), "제한된 바닥/벽/미세 경사 천장 도착 좌표의 점유 일치");
        }
        var reset = config.Devices[0];
        Require(FlipFriendsDeviceSystem.Touches(reset, new FPVector2(19, FP.FromString("-1.5")), half), "인접 Reset 조각 동시 접촉");
        Require(!FlipFriendsDeviceSystem.Touches(reset, new FPVector2(FP.FromString("39.5"), FP.FromString("-1.5")), half), "Reset 빈 타일 구멍 보존");
        FP left = reset.ContactCenters.Min(c => c.X) - reset.ContactSizes[0].X / 2 - half.X;
        Require(!FlipFriendsDeviceSystem.Touches(reset, new FPVector2(left, FP.FromString("-1.5")), half) &&
            FlipFriendsDeviceSystem.Touches(reset, new FPVector2(left + FP.FromRaw(1), FP.FromString("-1.5")), half), "Reset 접촉1FP 경계");
        var reversed = contours.Reverse().Select(c => new FlipFriendsTerrainContour { Id = c.Id, Vertices = c.Vertices.Reverse().ToArray() }).ToArray();
        for (int x = -22; x <= 22; x++) for (int y = -5; y <= 62; y++)
            Require(FlipFriendsTerrain.Overlaps(contours, new FPVector2(x, y), half) == FlipFriendsTerrain.Overlaps(reversed, new FPVector2(x, y), half), "윤곽 순서/방향 독립");
        var manifest = AssetDatabase.LoadAssetAtPath<QuantumCustomMapAsset>(QuantumTilemapSetup.ManifestPath);
        Require(config.CustomMapAgreement.StartsWith(manifest.Agreement + ":제품13:"), "제품13/6:6 합의");
        CheckContent();
    }
    private static void CheckContent()
    {
        var source = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/QuantumUser/Resources/FlipFriendsTilemapMap.json");
        var palette = AssetDatabase.LoadAssetAtPath<MapEditorPalette>("Assets/07_ScriptableObject/MapEditorPalette.asset");
        var terrain = AssetDatabase.LoadAssetAtPath<QuantumMapTerrain>("Assets/QuantumUser/Resources/FlipFriendsTilemapTerrain.asset");
        var devices = AssetDatabase.LoadAssetAtPath<QuantumMapDevices>("Assets/QuantumUser/Resources/FlipFriendsTilemapDevices.asset");
        var cloneTerrain = UnityEngine.Object.Instantiate(terrain); var cloneDevices = UnityEngine.Object.Instantiate(devices);
        QuantumMapConversion Convert(QuantumMapTerrain t, QuantumMapDevices d, bool reverse = false)
        {
            var data = JsonUtility.FromJson<MapData>(source.text); if (reverse) data.objects.Reverse();
            var result = QuantumMapConverter.Convert(data, palette, true); d.Apply(result); t.Apply(result); return result;
        }
        void Reject(Action action)
        { bool rejected = false; try { action(); } catch (InvalidOperationException) { rejected = true; } Require(rejected, "잘못된 콘텐츠 전체 거부"); }
        try
        {
            var valid = Convert(terrain, devices); Array.Reverse(cloneTerrain.Sources);
            Array.Reverse(cloneDevices.Sources[0].contactCenters); Array.Reverse(cloneDevices.Sources[0].contactSizes);
            Require(valid.ContentHash == Convert(cloneTerrain, cloneDevices, true).ContentHash, "맵/윤곽/Reset 조각 순서 독립 해시");
            cloneDevices.Sources[0].destination.x += 1;
            Require(valid.ContentHash != Convert(terrain, cloneDevices).ContentHash, "목적지 해시 반영");
            cloneDevices.Sources[0].destination.y = -4; Reject(() => Convert(terrain, cloneDevices));
            UnityEngine.Object.DestroyImmediate(cloneDevices); cloneDevices = UnityEngine.Object.Instantiate(devices);
            cloneDevices.Sources[0].contactSizes[0].x = 0; Reject(() => Convert(terrain, cloneDevices));
            UnityEngine.Object.DestroyImmediate(cloneDevices); cloneDevices = UnityEngine.Object.Instantiate(devices);
            cloneDevices.Sources[0].contactCenters[0].x = 4096; Reject(() => Convert(terrain, cloneDevices));
            cloneDevices.Configure(Enumerable.Repeat(devices.Sources[0], 33).ToArray()); Reject(() => Convert(terrain, cloneDevices));
            cloneTerrain.Sources[0].id = cloneTerrain.Sources[1].id; Reject(() => Convert(cloneTerrain, devices));
            UnityEngine.Object.DestroyImmediate(cloneTerrain); cloneTerrain = UnityEngine.Object.Instantiate(terrain);
            cloneTerrain.Sources[0].vertices[1] = cloneTerrain.Sources[0].vertices[0] + new Vector2(0.25f, 1); Reject(() => Convert(cloneTerrain, devices));
            UnityEngine.Object.DestroyImmediate(cloneTerrain); cloneTerrain = UnityEngine.Object.Instantiate(terrain);
            cloneTerrain.Sources[0].vertices[0].x = float.NaN; Reject(() => Convert(cloneTerrain, devices));
            var large = new QuantumTerrainSource { id = "초과", vertices = Enumerable.Range(0, 513).Select(i => new Vector2(i, 0)).ToArray() };
            cloneTerrain.Configure(new[] { large }, new Bounds(Vector3.zero, Vector3.one * 1000), "상한검사"); Reject(() => Convert(cloneTerrain, devices));
            var culture = System.Globalization.CultureInfo.CurrentCulture;
            try
            {
                System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("fr-FR");
                Require(valid.ContentHash == Convert(terrain, devices).ContentHash, "문화권 독립 콘텐츠 해시");
            }
            finally { System.Globalization.CultureInfo.CurrentCulture = culture; }
        }
        finally { UnityEngine.Object.DestroyImmediate(cloneTerrain); UnityEngine.Object.DestroyImmediate(cloneDevices); }
    }
    private static void CheckFrame(Frame frame)
    {
        var players = frame.Filter<FlipFriendsPlayer>(); players.Next(out var a, out var pa); players.Next(out var b, out _);
        var boxes = frame.Filter<FlipFriendsBox>(); boxes.Next(out var held, out var box);
        pa.Held = held; pa.RespawnFrame = 0; pa.RespawnPending = false; pa.SectionPending = false; pa.SectionCount = 0; frame.Set(a, pa);
        box.Carrier = a; box.RespawnPending = false; box.RespawnFrame = 0; frame.Set(held, box);
        var config = frame.RuntimeConfig.Devices[0];
        var ceilingPosition = new FPVector2(0, FP.FromString("58.3"));
        var oldProxy = FlipFriendsMovementSystem.LimitAxis(frame, ceilingPosition + new FPVector2(0, FP.FromString("0.75")), FP.FromString("0.5"), false, new FPVector2(FP.FromString("0.4"), FP.FromString("0.25")));
        var correct = FlipFriendsMovementSystem.LimitHeldTerrain(frame, held, ceilingPosition, FP.FromString("0.5"), false);
        Require(oldProxy > correct && correct < FP.FromString("0.4"), "축소 운반자의 대리 판정 누락 재현·실제 상자 천장 차단");
        frame.Set(a, new Transform2D { Position = new FPVector2(19, FP.FromString("-1.5")) });
        frame.Set(b, new Transform2D { Position = new FPVector2(-8, -2) });
        var blocker = frame.Create(); frame.Add(blocker, new Transform2D { Position = config.Destination + FlipFriendsInteractionSystem.HeldOffset });
        frame.Add(blocker, new FlipFriendsBox { HalfSize = new FPVector2(1, 1), ContentIndex = 63 });
        try
        {
            var devices = frame.Filter<FlipFriendsDevice>(); while (devices.Next(out var e, out var d)) frame.Set(e, new FlipFriendsDevice { ContentIndex = d.ContentIndex });
            var system = new FlipFriendsDeviceSystem(); system.Update(frame); system.Update(frame);
            var pending = frame.Get<FlipFriendsPlayer>(a); Require(pending.SectionCount == 1 && pending.Held == held && pending.SectionPending, "다중 조각/다중 호출 복귀1회·운반 전체 대기");
            frame.Destroy(blocker); FlipFriendsRespawnSystem.TryRestoreSection(frame, a);
            var restored = frame.Get<FlipFriendsPlayer>(a);
            Require(!restored.SectionPending && restored.Held == held && frame.Get<Transform2D>(held).Position == config.Destination + FlipFriendsInteractionSystem.HeldOffset && restored.Velocity == default, "같은틱 원자 복귀·운동 정리");
            system.Update(frame); Require(frame.Get<FlipFriendsPlayer>(a).SectionCount == 1, "복귀 후 재Reset 방지");
            restored.Held = default; frame.Set(a, restored); frame.Set(a, new Transform2D { Position = new FPVector2(-12, -2) });
            box.Carrier = default; box.RespawnPending = false; box.RespawnFrame = 0; frame.Set(held, box);
            frame.Set(held, new Transform2D { Position = new FPVector2(19, FP.FromString("-1.5")) });
            system.Update(frame); var direct = frame.Get<FlipFriendsBox>(held); system.Update(frame);
            Require(direct.RespawnPending && !direct.Carrier.IsValid && direct.Equals(frame.Get<FlipFriendsBox>(held)), "상자 직접 다중 조각 Reset·동일틱 중복 불변");
        }
        finally { if (frame.Exists(blocker)) frame.Destroy(blocker); }
    }
    private static async Task CheckMain()
    {
        var flow = QuantumGameFlow.Instance; Click("Start Button"); Click("Tilemap 사용례 선택"); Click("오프라인 시작");
        await Wait(() => flow.State == QuantumGameState.Playing, 10000);
        Require(flow.SelectedMap == 5 && flow.Content.Version == 6, "Main 새 지원 묶음 선택");
        var session = flow.Session; bool carried = false;
        session.InputSource = (tick, slot) => InputFor(session.Runner?.Game.Frames.Verified, tick, slot, tick > 1200);
        using (QuantumCallback.SubscribeManual<CallbackSimulateFinished>(c =>
        {
            if (c.Game != session.Runner?.Game || !c.Frame.IsVerified) return;
            var query = c.Frame.Filter<FlipFriendsPlayer>(); while (query.Next(out _, out var p)) carried |= p.SectionCount > 0 && p.Held.IsValid;
        })) await Wait(() => flow.State == QuantumGameState.Ready && SceneManager.GetActiveScene().name == "Main", 55000);
        Require(carried && !QuantumRunner.ActiveRunners.Any() && QuantumGameSession.ActiveInputSubscriptions == 0, "Main 운반Reset·전원도착·정리");
        Click("Start Button"); Click("Tilemap 사용례 선택"); Click("오프라인 시작"); await Wait(() => flow.State == QuantumGameState.Playing, 10000);
        Click("플레이 종료"); await Wait(() => flow.State == QuantumGameState.Ready, 10000);
        Require(!QuantumRunner.ActiveRunners.Any() && QuantumGameSession.ActiveInputSubscriptions == 0, "재진입/메뉴종료 정리");
    }
    public static async void RunCloud()
    {
        var sessions = new[] { new QuantumGameSession(), new QuantumGameSession() }; var checks = new[] { new Dictionary<int, ulong>(), new Dictionary<int, ulong>() };
        var carried = new bool[2]; var clients = new Photon.Realtime.RealtimeClient[2];
        try
        {
            foreach (var s in sessions) s.InputSource = (tick, slot) => { var locals = s.Runner?.Game.GetLocalPlayers(); return locals == null || locals.Count <= slot ? default : InputFor(s.Runner.Game.Frames.Verified, tick, (int)locals[slot], tick > 1200); };
            using (QuantumCallback.SubscribeManual<CallbackSimulateFinished>(c =>
            {
                if (!c.Frame.IsVerified) return;
                for (int i = 0; i < 2; i++) if (c.Game == sessions[i].Runner?.Game)
                {
                    if (c.Frame.Number <= 1200 && c.Frame.Number % 120 == 0) checks[i][c.Frame.Number] = c.Frame.CalculateChecksum();
                    var query = c.Frame.Filter<FlipFriendsPlayer>(); while (query.Next(out _, out var p)) carried[i] |= p.SectionCount > 0 && p.Held.IsValid;
                }
            }))
            {
                string room = "ff-tilemap-" + Guid.NewGuid().ToString("N"); var first = sessions[0].StartDevelopmentAsync(Config(), room, false);
                await Wait(() => sessions[0].Client?.InRoom == true || first.IsFaulted, 15000); if (first.IsFaulted) await first;
                await Task.WhenAll(first, sessions[1].StartDevelopmentAsync(Config(), room, true)); clients[0] = sessions[0].Client; clients[1] = sessions[1].Client;
                await Wait(() => sessions.All(s => s.Runner.Game.Frames.Verified.FlipFriendsCompletedFrame > 0), 60000);
                var samples = checks[0].Where(p => p.Key >= 240).ToArray();
                Require(samples.Length == 9 && samples.All(p => checks[1].TryGetValue(p.Key, out var h) && p.Value == h) && carried.All(v => v), "Photon 확정9개/양쪽 운반Reset");
                Require(sessions[0].Runner.Game.Frames.Verified.FlipFriendsCompletedFrame == sessions[1].Runner.Game.Frames.Verified.FlipFriendsCompletedFrame, "Photon 완료 프레임 일치");
                Status = $"통과: Tilemap Photon 동일 PC 두 연결, 지역={clients[0].CurrentRegion}, RTT={clients[0].RealtimePeer.Stats.RoundtripTime}/{clients[1].RealtimePeer.Stats.RoundtripTime}ms, 지연/손실 주입 없음, 완료={sessions[0].Runner.Game.Frames.Verified.FlipFriendsCompletedFrame}\n" + string.Join("\n", samples.Select(p => p.Key + ": " + p.Value));
            }
        }
        catch (Exception error) { Status = "실패: " + error; Debug.LogException(error); }
        finally
        {
            foreach (var s in sessions) await s.StopAsync();
            Status += "\n러너/연결/입력 정리=" + sessions.All(s => s.Runner == null && s.Client == null && !s.HasInputSubscription);
            File.WriteAllText("Logs/QuantumTilemapCloud.txt", Status);
        }
    }
    private static void Click(string name) => UnityEngine.Object.FindObjectsByType<Button>(FindObjectsInactive.Exclude).Single(b => b.name == name).onClick.Invoke();
    private static async Task Wait(Func<bool> ready, int timeout)
    { var end = DateTime.UtcNow.AddMilliseconds(timeout); while (!ready() && DateTime.UtcNow < end && EditorApplication.isPlaying) await Task.Delay(20); Require(ready(), "비동기 제한시간"); }
    private static void Require(bool ok, string label) { if (!ok) throw new InvalidOperationException("Tilemap 검사: " + label); }
}
