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

public static class QuantumRespawnChecks
{
    public static string Status { get; private set; } = "미실행";
    private const int Target = 2400;

    public static RuntimeConfig Config()
    {
        var config = new RuntimeConfig();
        QuantumGameContent.Prepare(AssetDatabase.LoadAssetAtPath<QuantumCustomMapAsset>(QuantumRespawnSetup.ManifestPath), config);
        return config;
    }

    public static Quantum.Input InputFor(Frame frame, int number, int player)
    {
        if (frame == null || number < 120) return default;
        var query = frame.Filter<Transform2D, FlipFriendsPlayer>();
        FlipFriendsPlayer state = default; FPVector2 position = default;
        while (query.Next(out _, out var t, out var p)) if ((int)p.Player == player) { state = p; position = t.Position; }
        if (player == 1) return new Quantum.Input { Reset = number >= 340 && number < 360,
            Horizontal = number >= 460 ? FPMath.Clamp((-3 - position.X) * 4 - state.Velocity.X * 2, -FP._1, FP._1) : FP._0,
            Jump = number >= 460 && number < 490 };
        if (number < 240) return new Quantum.Input { Reset = number < 180 || number == 220 };
        if (number < 600)
        {
            if (state.Held.IsValid) return new Quantum.Input { Reset = number >= 420 && number < 450 };
            if (state.RespawnCount >= 3) return default;
            return QuantumInteractionChecks.InputFor(frame, number, player);
        }
        if (number >= 2200) return default;
        if (state.RespawnCount >= 5) return default;
        if (state.Held.IsValid) return number < 1100
            ? new Quantum.Input { Reset = number >= 900 && state.RespawnCount == 3 }
            : new Quantum.Input { Horizontal = 1, Run = true };
        var boxes = frame.Filter<Transform2D, FlipFriendsBox>();
        if (!boxes.Next(out _, out var bt, out _)) return default;
        FP gap = bt.Position.X - position.X;
        FP aim = FPMath.Clamp((gap - FP.FromString("0.9")) * 4 - state.Velocity.X * 2, -FP._1, FP._1);
        if (gap >= FP.FromString("0.85") && gap < FP.FromString("0.98")) aim = FP.FromString("0.001");
        return new Quantum.Input { Horizontal = aim, Interact = number >= 700 && number % 20 == 0, Jump = number >= 600 && number < 630 };
    }

    public sealed class Evidence
    {
        private readonly Dictionary<PlayerRef, FlipFriendsPlayer> previous = new();
        public bool HasPlayerCarryReset, HasBoxCarryReset, HasFall, HasBoxReturn;
        public int MaxReturns;
        public readonly List<string> Trace = new();
        public string Summary => $"복귀={MaxReturns}, 플레이어운반리셋={HasPlayerCarryReset}, 상자운반리셋={HasBoxCarryReset}, 추락={HasFall}, 상자복귀={HasBoxReturn}";
        public void Observe(Frame frame)
        {
            var players = frame.Filter<Transform2D, FlipFriendsPlayer>();
            while (players.Next(out var entity, out var transform, out var state))
            {
                if ((int)state.Player == 0 && frame.Number == 180) Require(state.RespawnCount == 1, "Reset 유지 중 한 번만 실행");
                if ((int)state.Player == 0 && frame.Number == 221) Require(state.RespawnCount == 2, "Reset 해제 후 다시 누름");
                if ((int)state.Player == 1 && frame.Number == 360) Require(state.RespawnCount == 0 && state.Carrier.IsValid, "들린 플레이어 Reset 소비·무시");
                MaxReturns = Math.Max(MaxReturns, state.RespawnCount);
                if (previous.TryGetValue(state.Player, out var old) && state.RespawnCount > old.RespawnCount)
                {
                    if (old.Held.IsValid)
                    {
                        HasPlayerCarryReset |= frame.Has<FlipFriendsPlayer>(old.Held);
                        HasBoxCarryReset |= frame.Has<FlipFriendsBox>(old.Held);
                    }
                    var input = InputAt(frame, state.Player);
                    HasFall |= !input.Reset.IsDown;
                    Require(transform.Position == state.HomePosition && state.Velocity == default && state.VelocityXSmoothing == 0, "복귀 위치·속도·스무딩");
                    Require(!state.Held.IsValid && !state.Carrier.IsValid && !state.IsClimbed && state.ShrinkTicks == 0 &&
                        state.ClimbBlockTicks == 0 && state.JumpBlockTicks == 0 && state.UncontrollableTicks == 0 && !state.StompTarget.IsValid, "복귀 관계·타이머·접촉 정리");
                }
                if (state.Held.IsValid)
                {
                    if (frame.TryGet<FlipFriendsPlayer>(state.Held, out var carried)) Require(carried.Carrier == entity, "플레이어 양방향 관계");
                    if (frame.TryGet<FlipFriendsBox>(state.Held, out var box)) Require(box.Carrier == entity, "상자 양방향 관계");
                }
                if (state.Carrier.IsValid) Require(frame.Get<FlipFriendsPlayer>(state.Carrier).Held == entity, "역방향 관계");
                if (frame.Number % 20 == 0)
                {
                    var input = InputAt(frame, state.Player);
                    Trace.Add($"{frame.Number} P{state.Player} {transform.Position} v={state.Velocity} held={state.Held} reset={state.RespawnCount} pending={state.RespawnPending} input=({input.Horizontal},{input.Vertical}) jump={input.Jump.IsDown} interact={input.Interact.IsDown} reset={input.Reset.IsDown}");
                }
                previous[state.Player] = state;
            }
            var boxes = frame.Filter<FlipFriendsBox>();
            while (boxes.Next(out var entity, out var box))
            {
                HasBoxReturn |= box.RespawnCount > 0;
                if (box.Carrier.IsValid) Require(frame.Get<FlipFriendsPlayer>(box.Carrier).Held == entity, "상자 역방향 관계");
            }
        }
        public void AssertComplete() => Require(MaxReturns >= 5 && HasPlayerCarryReset && HasBoxCarryReset && HasFall && HasBoxReturn, Summary);
    }

    public static async Task Run()
    {
        CheckContent();
        await CheckBoundaries();
        var session = new QuantumGameSession();
        session.InputSource = (tick, player) => InputFor(session.Runner?.Game.Frames.Verified, tick, player);
        QuantumRunner replay = null;
        var expected = new Dictionary<int, ulong>(); var actual = new Dictionary<int, ulong>(); var evidence = new Evidence();
        try
        {
            using (QuantumCallback.SubscribeManual<CallbackSimulateFinished>(callback =>
            {
                if (!callback.Frame.IsVerified || callback.Frame.Number > Target) return;
                if (callback.Game == session.Runner?.Game)
                {
                    evidence.Observe(callback.Frame);
                    if (callback.Frame.Number % 120 == 0) expected[callback.Frame.Number] = callback.Frame.CalculateChecksum();
                }
                if (callback.Game == replay?.Game && callback.Frame.Number % 120 == 0) actual[callback.Frame.Number] = callback.Frame.CalculateChecksum();
            }))
            {
                await session.StartLocalAsync(Config());
                session.Runner.IsSessionUpdateDisabled = true;
                for (int i = 0; i < Target + 240 && !expected.ContainsKey(Target); i++) session.Runner.Service(1f / 60);
                File.WriteAllText("Logs/QuantumRespawnTrace.txt", string.Join("\n", evidence.Trace));
                evidence.AssertComplete();
                var record = session.Runner.Game.GetRecordedReplay(includeChecksums: true);
                var serializer = new QuantumUnityJsonSerializer();
                replay = QuantumRunner.StartGame(new SessionRunner.Arguments
                {
                    RunnerId = "FF_RESPAWN_REPLAY", RuntimeConfig = serializer.ConfigFromByteArray<RuntimeConfig>(record.RuntimeConfigData.Decode(), compressed: true),
                    SessionConfig = record.DeterministicConfig, ReplayProvider = record.CreateInputProvider(), InitialTick = record.InitialTick,
                    FrameData = record.InitialFrameData, GameMode = DeterministicGameMode.Replay, PlayerCount = 2
                });
                replay.IsSessionUpdateDisabled = true;
                for (int i = 0; i < Target + 240 && !actual.ContainsKey(Target); i++) replay.Service(1f / 60);
                Require(expected.Count == 20 && actual.Count == 20 && expected.All(p => actual[p.Key] == p.Value), "복귀 입력 새 러너 재생20개");
                Status = "통과: 복귀 경계/관계 검사·새 입력 재생20개 " + evidence.Summary;
                File.WriteAllText("Logs/QuantumRespawnChecks.txt", Status + "\n" + string.Join("\n", expected.Select(p => p.Key + ": " + p.Value)));
                File.WriteAllText("Logs/QuantumRespawnReplay.json", UnityEngine.JsonUtility.ToJson(record));
            }
        }
        finally { if (replay != null) await replay.ShutdownAsync(); await session.StopAsync(); }
    }

    public static async Task<string> RunCloud()
    {
        var sessions = new[] { new QuantumGameSession(), new QuantumGameSession() };
        var checks = new[] { new Dictionary<int, ulong>(), new Dictionary<int, ulong>() };
        var evidence = new[] { new Evidence(), new Evidence() };
        var clients = new Photon.Realtime.RealtimeClient[2];
        bool isFinishing = false;
        try
        {
            foreach (var session in sessions)
                session.InputSource = (tick, slot) =>
                {
                    var locals = session.Runner?.Game.GetLocalPlayers();
                    if (locals == null || locals.Count <= slot) return default;
                    return isFinishing ? QuantumInteractionChecks.FinishInput(session, slot) : InputFor(session.Runner.Game.Frames.Verified, tick, (int)locals[slot]);
                };
            using (QuantumCallback.SubscribeManual<CallbackSimulateFinished>(callback =>
            {
                if (!callback.Frame.IsVerified) return;
                for (int i = 0; i < 2; i++)
                    if (callback.Game == sessions[i].Runner?.Game && callback.Frame.Number <= 2400)
                    {
                        evidence[i].Observe(callback.Frame);
                        if (callback.Frame.Number % 120 == 0) checks[i][callback.Frame.Number] = callback.Frame.CalculateChecksum();
                    }
            }))
            {
                string room = "ff-respawn-" + Guid.NewGuid().ToString("N");
                var first = sessions[0].StartDevelopmentAsync(Config(), room, false);
                await Wait(() => sessions[0].Client?.InRoom == true || first.IsFaulted, 15000);
                if (first.IsFaulted) await first;
                var second = sessions[1].StartDevelopmentAsync(Config(), room, true);
                await Task.WhenAll(first, second);
                clients[0] = sessions[0].Client; clients[1] = sessions[1].Client;
                await Wait(() => checks.All(check => check.ContainsKey(2400)), 65000);
                File.WriteAllText("Logs/QuantumRespawnCloudTrace.txt", string.Join("\n", evidence[0].Trace));
                foreach (var item in evidence) item.AssertComplete();
                var samples = checks[0].Where(pair => pair.Key >= 240).ToArray();
                Require(samples.Length == 19 && samples.All(p => checks[1].TryGetValue(p.Key, out var value) && value == p.Value), "개발 Photon 복귀 확정 체크섬19개");
                isFinishing = true;
                await Wait(() => sessions.All(session => session.Runner.Game.Frames.Verified.FlipFriendsCompletedFrame > 0), 25000);
                int completed = sessions[0].Runner.Game.Frames.Verified.FlipFriendsCompletedFrame;
                Require(completed == sessions[1].Runner.Game.Frames.Verified.FlipFriendsCompletedFrame, "복귀 Photon 전원 완료 프레임 일치");
                var result = $"통과: 새 복귀 Photon 두 연결, 완료={completed}, 지역={clients[0].CurrentRegion}, RTT={clients[0].RealtimePeer.Stats.RoundtripTime}/{clients[1].RealtimePeer.Stats.RoundtripTime}ms, 같은 PC, 지연/손실 주입 없음\n" +
                    evidence[0].Summary + "\n" + evidence[1].Summary + "\n" + string.Join("\n", samples.Select(p => p.Key + ": " + p.Value));
                File.WriteAllText("Logs/QuantumRespawnCloud.txt", result);
                return result;
            }
        }
        finally
        {
            foreach (var session in sessions) await session.StopAsync();
            Require(sessions.All(session => session.Runner == null && session.Client == null && !session.HasInputSubscription) && clients.All(client => client == null || !client.IsConnected), "복귀 Photon 종료 정리");
        }
    }

    public static async void RunStandalone()
    {
        try { await Run(); }
        catch (Exception error) { Status = "실패: " + error; UnityEngine.Debug.LogException(error); }
    }

    private static void CheckContent()
    {
        var palette = AssetDatabase.LoadAssetAtPath<MapEditorPalette>("Assets/07_ScriptableObject/MapEditorPalette.asset");
        var source = AssetDatabase.LoadAssetAtPath<UnityEngine.TextAsset>(QuantumRespawnSetup.SourcePath).text;
        var data = UnityEngine.JsonUtility.FromJson<MapData>(source);
        var conversion = QuantumMapConverter.Convert(data, palette, true);
        data.objects.Reverse();
        Require(conversion.ContentHash == QuantumMapConverter.Convert(data, palette, true).ContentHash, "순서와 무관한 복귀 해시");
        var config = Config();
        Require(config.CustomMapAgreement.StartsWith("4:4:") && config.CustomMapAgreement.Contains(":제품15:") && QuantumGameContent.NetworkVersion == "ff-product-15", "새 콘텐츠/입력/제품 합의");
        FallBoundary.TryCreate(data.objects, palette, out var boundary);
        Require(config.FallMinX == QuantumMapConverter.Q(boundary.MinX) && config.FallMaxX == QuantumMapConverter.Q(boundary.MaxX) && config.FallMinY == QuantumMapConverter.Q(boundary.MinY), "Mirror 렌더러 경계+10 대조");
        data.objects.Single(x => x.prefabID == QuantumMapConverter.Spawn && x.position.x == -1).position.x -= 1;
        Require(conversion.ContentHash != QuantumMapConverter.Convert(data, palette, true).ContentHash, "복귀 위치 해시 반영");
        data = UnityEngine.JsonUtility.FromJson<MapData>(source);
        data.objects.Single(x => x.prefabID == QuantumMapConverter.Ground && x.position.y > 0).scale.x += 2;
        var expanded = QuantumMapConverter.Convert(data, palette, true);
        Require(conversion.ContentHash != expanded.ContentHash && conversion.MaxX != expanded.MaxX, "경계 해시 반영");
        var isolated = UnityEngine.Object.Instantiate(palette);
        isolated.entries = palette.entries.Select(entry => new PaletteEntry { id = entry.id, prefab = entry.prefab,
            category = entry.category, thumbnail = entry.thumbnail, validationRule = entry.validationRule }).ToList();
        try
        {
            foreach (var type in new[] { typeof(SavePoint), typeof(RespawnHandler), typeof(ObjSummonSwitch) })
            {
                var root = new UnityEngine.GameObject("미지원 복귀 장치 검사");
                try
                {
                    root.AddComponent(type);
                    isolated.entries.Single(entry => entry.id == QuantumMapConverter.Spawn).prefab = root;
                    bool isRejected = false;
                    try { QuantumMapConverter.Convert(UnityEngine.JsonUtility.FromJson<MapData>(source), isolated, true); }
                    catch (InvalidOperationException error) { isRejected = error.Message.Contains("위치=") && error.Message.Contains(type.Name); }
                    Require(isRejected, "미지원 장치 명시적 콘텐츠 거부: " + type.Name);
                }
                finally { UnityEngine.Object.DestroyImmediate(root); }
            }
        }
        finally { UnityEngine.Object.DestroyImmediate(isolated); }
    }

    private static async Task CheckBoundaries()
    {
        var session = new QuantumGameSession { InputSource = (_, _) => default };
        try
        {
            await session.StartLocalAsync(Config()); session.Runner.IsSessionUpdateDisabled = true;
            for (int i = 0; i < 80; i++) session.Runner.Service(1f / 60);
            CheckFrame(session.Runner.Game.Frames.Verified);
        }
        finally { await session.StopAsync(); }
    }

    private static void CheckFrame(Frame frame)
    {
        var config = frame.RuntimeConfig;
        var edge = new FPVector2(config.FallMinX, config.FallMinY);
        Require(!FlipFriendsRespawnSystem.IsOutside(config, edge), "정확한 경계는 내부");
        Require(FlipFriendsRespawnSystem.IsOutside(config, edge - new FPVector2(FP.FromRaw(1), 0)), "왼쪽1raw 밖");
        Require(FlipFriendsRespawnSystem.IsOutside(config, new FPVector2(config.FallMaxX + FP.FromRaw(1), 0)), "오른쪽1raw 밖");
        Require(FlipFriendsRespawnSystem.IsOutside(config, edge - new FPVector2(0, FP.FromRaw(1))), "아래1raw 밖");
        Require(!FlipFriendsRespawnSystem.IsOutside(config, new FPVector2(0, 1000)), "위쪽 제외");
        var players = frame.Filter<FlipFriendsPlayer>(); players.Next(out var a, out var ap); players.Next(out var b, out var bp);
        var boxes = frame.Filter<FlipFriendsBox>(); boxes.Next(out var boxEntity, out var box);
        var pre = new FlipFriendsRespawnSystem(false); var post = new FlipFriendsRespawnSystem(true);
        void Place(EntityRef entity, FPVector2 p) => frame.Set(entity, new Transform2D { Position = p });
        void ResetState()
        {
            frame.Set(a, new FlipFriendsPlayer { Player = ap.Player, HomePosition = config.CustomMapSpawns[(int)ap.Player], Facing = 1 });
            frame.Set(b, new FlipFriendsPlayer { Player = bp.Player, HomePosition = config.CustomMapSpawns[(int)bp.Player] });
            frame.Set(boxEntity, new FlipFriendsBox { HomePosition = box.HomePosition, HalfSize = box.HalfSize });
            Place(a, new FPVector2(1, 1)); Place(b, new FPVector2(-3, 1)); Place(boxEntity, box.HomePosition);
        }
        ResetState();
        var state = frame.Get<FlipFriendsPlayer>(a);
        state.Velocity = new FPVector2(5, -12); state.VelocityXSmoothing = 8; state.IsClimbed = true;
        state.ShrinkTicks = state.JumpBlockTicks = state.ClimbBlockTicks = state.UncontrollableTicks = 20;
        state.StompTarget = b; state.ReleaseFrame = state.EscapeFrame = state.EscapeRequestFrame = 300;
        frame.Set(a, state); Place(a, edge - new FPVector2(1, 1));
        pre.Update(frame); post.Update(frame); pre.Update(frame);
        state = frame.Get<FlipFriendsPlayer>(a);
        Require(state.RespawnCount == 1 && !state.RespawnPending && state.Velocity == default && state.VelocityXSmoothing == 0 &&
            !state.IsClimbed && state.ShrinkTicks == 0 && state.RopeContactCount == 0 && state.JumpBlockTicks == 0 && state.ClimbBlockTicks == 0 &&
            state.UncontrollableTicks == 0 && state.ReleaseFrame == 0 && state.EscapeFrame == 0 && state.EscapeRequestFrame == 0 && !state.StompTarget.IsValid, "중복 복귀와 운동 상태 정리");
        var t = frame.Get<Transform2D>(a); var before = t.Position;
        FlipFriendsMovementSystem.Simulate(frame, ref t, ref state, new Quantum.Input { Vertical = 1, Jump = true, Interact = true });
        Require(t.Position == before && !state.IsFinished && !FlipFriendsInteractionSystem.TryPickup(frame, a), "같은 틱 도착/들기/점프 차단");
        ResetState(); state = frame.Get<FlipFriendsPlayer>(a); state.Held = b; frame.Set(a, state);
        var carried = frame.Get<FlipFriendsPlayer>(b); carried.Carrier = a; frame.Set(b, carried);
        Place(a, edge - new FPVector2(1, 1)); Place(b, edge - new FPVector2(1, 0)); pre.Update(frame); post.Update(frame);
        Require(!frame.Get<FlipFriendsPlayer>(a).Held.IsValid && !frame.Get<FlipFriendsPlayer>(b).Carrier.IsValid, "운반자 추락 양방향 해제");
        ResetState(); state = frame.Get<FlipFriendsPlayer>(a); state.Held = b; frame.Set(a, state);
        carried = frame.Get<FlipFriendsPlayer>(b); carried.Carrier = a; frame.Set(b, carried);
        Place(b, edge - new FPVector2(1, 1)); pre.Update(frame);
        Require(frame.Get<FlipFriendsPlayer>(b).Carrier == a && frame.Get<FlipFriendsPlayer>(b).RespawnCount == 0, "들린 플레이어 개별 추락 무시");
        ResetState(); state = frame.Get<FlipFriendsPlayer>(a); state.Held = b; frame.Set(a, state);
        carried = frame.Get<FlipFriendsPlayer>(b); carried.Carrier = a; frame.Set(b, carried);
        var blocker = frame.Create();
        frame.Add(blocker, new Transform2D { Position = new FPVector2(1, 1) });
        frame.Add(blocker, new FlipFriendsBox { HalfSize = new FPVector2(2, 2) });
        try
        {
            FlipFriendsRespawnSystem.QueuePlayer(frame, a); post.Update(frame);
            Require(!frame.Get<FlipFriendsPlayer>(a).Held.IsValid && !frame.Get<FlipFriendsPlayer>(b).Carrier.IsValid &&
                frame.Get<FlipFriendsPlayer>(b).RespawnPending, "놓을 공간 없음·양방향 해제 후 안전 복귀 대기");
        }
        finally { frame.Destroy(blocker); }
        post.Update(frame);
        Require(!frame.Get<FlipFriendsPlayer>(a).RespawnPending && !frame.Get<FlipFriendsPlayer>(b).RespawnPending, "막힘 해제 후 두 초기 위치 복귀");
        ResetState(); state = frame.Get<FlipFriendsPlayer>(a); state.Held = boxEntity; frame.Set(a, state);
        box = frame.Get<FlipFriendsBox>(boxEntity); box.Carrier = a; box.Velocity = new FPVector2(6, 6); frame.Set(boxEntity, box);
        FlipFriendsRespawnSystem.QueueBox(frame, boxEntity); post.Update(frame);
        box = frame.Get<FlipFriendsBox>(boxEntity);
        Require(!frame.Get<FlipFriendsPlayer>(a).Held.IsValid && !box.Carrier.IsValid && box.Velocity == default && box.VelocityXSmoothing == 0, "운반 중 상자 복귀 정리");
        ResetState(); state = frame.Get<FlipFriendsPlayer>(a); state.Held = boxEntity; frame.Set(a, state);
        box = frame.Get<FlipFriendsBox>(boxEntity); box.Carrier = a; frame.Set(boxEntity, box); Place(boxEntity, edge - new FPVector2(1, 1)); pre.Update(frame);
        Require(frame.Get<FlipFriendsBox>(boxEntity).Carrier == a && frame.Get<FlipFriendsBox>(boxEntity).RespawnCount == 0, "운반 중 상자 개별 추락 무시");
        ResetState(); Place(b, box.HomePosition); FlipFriendsRespawnSystem.QueueBox(frame, boxEntity); post.Update(frame);
        Require(frame.Get<FlipFriendsBox>(boxEntity).RespawnPending, "상자 목적지 점유 대기");
        Place(b, new FPVector2(-3, 1)); post.Update(frame);
        Require(!frame.Get<FlipFriendsBox>(boxEntity).RespawnPending && frame.Get<Transform2D>(boxEntity).Position == box.HomePosition, "상자 목적지 점유 해제 후 복귀");
        ResetState(); state = frame.Get<FlipFriendsPlayer>(a); Place(b, state.HomePosition);
        FlipFriendsRespawnSystem.QueuePlayer(frame, a); post.Update(frame);
        Require(frame.Get<FlipFriendsPlayer>(a).RespawnPending, "점유된 목적지 충돌 없는 대기");
        Place(b, new FPVector2(-3, 1)); post.Update(frame);
        Require(!frame.Get<FlipFriendsPlayer>(a).RespawnPending && frame.Get<FlipFriendsPlayer>(a).RespawnCount == 1, "점유 해제 후 중복 없는 복귀");
        ResetState(); state = frame.Get<FlipFriendsPlayer>(a); state.HomePosition = new FPVector2(0, FP.FromString("-0.5")); frame.Set(a, state);
        FlipFriendsRespawnSystem.QueuePlayer(frame, a); post.Update(frame);
        Require(frame.Get<FlipFriendsPlayer>(a).RespawnPending, "지형 속 목적지에는 생성하지 않음");
        ResetState(); state = frame.Get<FlipFriendsPlayer>(a); state.HomePosition = edge - new FPVector2(1, 1); frame.Set(a, state);
        FlipFriendsRespawnSystem.QueuePlayer(frame, a); pre.Update(frame); post.Update(frame);
        Require(frame.Get<FlipFriendsPlayer>(a).RespawnPending && frame.Get<FlipFriendsPlayer>(a).RespawnCount == 1, "잘못된 목적지 무한 재추락 차단");
        ResetState(); state = frame.Get<FlipFriendsPlayer>(a); state.IsFinished = true; frame.Set(a, state); Place(a, edge - new FPVector2(1, 1)); pre.Update(frame);
        Require(frame.Get<FlipFriendsPlayer>(a).RespawnCount == 0, "도착한 플레이어 재처리 없음");
    }

    private static Quantum.Input InputAt(Frame frame, PlayerRef player) => frame.FlipFriendsInput(player);

    private static async Task Wait(Func<bool> condition, int milliseconds)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(milliseconds);
        while (!condition() && DateTime.UtcNow < deadline && EditorApplication.isPlaying) await Task.Delay(20);
        Require(condition(), "복귀 비동기 제한 시간");
    }

    private static void Require(bool value, string label) { if (!value) throw new InvalidOperationException("Quantum 복귀 검사: " + label); }
}
