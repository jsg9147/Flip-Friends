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

public static class QuantumInteractionChecks
{
    public static string Status { get; private set; } = "미실행";

    public static async Task RunStomp()
    {
        CheckConversion();
        var config = new RuntimeConfig();
        QuantumGameContent.Prepare(AssetDatabase.LoadAssetAtPath<QuantumCustomMapAsset>(QuantumRopeTestSetup.ManifestPath), config);
        var session = new QuantumGameSession { InputSource = (_, _) => default };
        try
        {
            await session.StartLocalAsync(config);
            session.Runner.IsSessionUpdateDisabled = true;
            for (int i = 0; i < 60; i++) session.Runner.Service(1f / 60);
            CheckStomp(session.Runner.Game.Frames.Verified);
            CheckCarry(session.Runner.Game.Frames.Verified);
            Status = "밟기: 진입/비대상/반복 차단·유지 배율·Shrink/복원 통과";
        }
        finally { await session.StopAsync(); }
    }

    private static void CheckConversion()
    {
        var palette = AssetDatabase.LoadAssetAtPath<MapEditorPalette>("Assets/07_ScriptableObject/MapEditorPalette.asset");
        var source = AssetDatabase.LoadAssetAtPath<UnityEngine.TextAsset>(QuantumInteractionSetup.SourcePath);
        MapData Fresh() => UnityEngine.JsonUtility.FromJson<MapData>(source.text);
        var baseline = QuantumMapConverter.Convert(Fresh(), palette);
        Require(baseline.Version == 3 && baseline.MovableBoxes.Count == 1 && baseline.Boxes.Count == 4, "상자는 v3 동적 엔티티·지형 베이크 분리");
        var reverse = Fresh(); reverse.objects.Reverse();
        Require(QuantumMapConverter.Convert(reverse, palette).ContentHash == baseline.ContentHash, "상자 역순 변환 합의");
        var moved = Fresh(); moved.objects.Single(item => item.prefabID == QuantumMapConverter.Box).position.x += 0.25f;
        Require(QuantumMapConverter.Convert(moved, palette).ContentHash != baseline.ContentHash, "상자 이동 해시");
        void Reject(Action<PlacedObjectData> change)
        {
            var data = Fresh(); change(data.objects.Single(item => item.prefabID == QuantumMapConverter.Box));
            bool isRejected = false;
            try { QuantumMapConverter.Convert(data, palette); } catch (InvalidOperationException) { isRejected = true; }
            Require(isRejected, "미지원 상자 구성 전체 거부");
        }
        Reject(box => box.rotation = 90);
        Reject(box => box.scale.x = 2);
        Reject(box => box.position.y = -0.25f);
        Reject(box => box.prefabID = "obstacle.cannon");
        var config = Config();
        Require(config.CustomMapAgreement.StartsWith("3:3:") && config.CustomMapAgreement.Contains(":제품6:"), "상자/제품 합의 버전");
    }

    public static RuntimeConfig Config()
    {
        var config = new RuntimeConfig();
        QuantumGameContent.Prepare(AssetDatabase.LoadAssetAtPath<QuantumCustomMapAsset>(QuantumInteractionSetup.ManifestPath), config);
        return config;
    }

    public static Quantum.Input InputFor(Frame frame, int number, int player)
    {
        if (frame == null || number < 120) return default;
        var players = frame.Filter<Transform2D, FlipFriendsPlayer>();
        EntityRef mine = default;
        Transform2D position = default, partnerPosition = default;
        FlipFriendsPlayer state = default;
        while (players.Next(out var entity, out var transform, out var item))
            if ((int)item.Player == player) { mine = entity; position = transform; state = item; } else partnerPosition = transform;
        if (!mine.IsValid || player != 0) return default;
        var input = new Quantum.Input();
        FP Aim(FP x) => FPMath.Clamp((x - position.Position.X) * 4 - state.Velocity.X * 2, -FP._1, FP._1);
        if (number < 240)
        {
            input.Horizontal = Aim(partnerPosition.Position.X);
            input.Jump = number >= 120 && number < 155;
        }
        else if (number < 600)
        {
            if (state.Held.IsValid)
                input.Interact = number >= 500 && number % 20 == 0;
            else if (state.ThrowCount == 0)
            {
                input.Horizontal = Aim(partnerPosition.Position.X - FP.FromString("0.9"));
                var gap = partnerPosition.Position.X - position.Position.X;
                if (gap >= FP.FromString("0.85") && gap < FP.FromString("0.98")) input.Horizontal = FP.FromString("0.001");
                input.Interact = number % 20 == 0;
            }
        }
        else
        {
            var boxes = frame.Filter<Transform2D, FlipFriendsBox>();
            if (boxes.Next(out var boxEntity, out var boxTransform, out var box))
            {
                if (state.Held == boxEntity)
                {
                    input.Interact = number % 20 == 0;
                    input.Vertical = state.PutDownCount == 0 ? -1 : 0;
                }
                else if (state.ThrowCount < 2)
                {
                    input.Horizontal = Aim(boxTransform.Position.X - FP.FromString("0.9"));
                    var gap = boxTransform.Position.X - position.Position.X;
                    if (gap >= FP.FromString("0.85") && gap < FP.FromString("0.98")) input.Horizontal = FP.FromString("0.001");
                    input.Interact = number % 20 == 0;
                }
            }
        }
        return input;
    }

    public sealed class Evidence
    {
        public bool HasStomp, HasPlayerCarry, HasBoxCarry, HasThrow, HasPutDown, HasBoxGround;
        public readonly List<string> Trace = new();
        public string Summary => $" 밟기={HasStomp}, 플레이어운반={HasPlayerCarry}, 상자운반={HasBoxCarry}, 던지기={HasThrow}, 내려놓기={HasPutDown}, 상자접지={HasBoxGround}";
        public void Observe(Frame frame)
        {
            var players = frame.Filter<Transform2D, FlipFriendsPlayer>();
            while (players.Next(out var entity, out var transform, out var state))
            {
                if (frame.Number % 20 == 0) Trace.Add($"{frame.Number} P{state.Player} {transform.Position} v={state.Velocity} stomp={state.StompTarget}/{state.StompCount} held={state.Held} pickup={state.PickupCount} throw={state.ThrowCount} put={state.PutDownCount}");
                HasStomp |= state.StompCount > 0;
                HasThrow |= state.ThrowCount >= 2;
                HasPutDown |= state.PutDownCount > 0;
                if (state.Held.IsValid)
                {
                    Require(!state.Carrier.IsValid, "운반 순환 없음");
                    if (frame.TryGet<FlipFriendsPlayer>(state.Held, out var carried))
                    { Require(carried.Carrier == entity, "플레이어 양방향 관계"); HasPlayerCarry = true; }
                    if (frame.TryGet<FlipFriendsBox>(state.Held, out var box))
                    { Require(box.Carrier == entity, "상자 양방향 관계"); HasBoxCarry = true; }
                    var target = frame.Get<Transform2D>(state.Held);
                    Require(target.Position == transform.Position + FlipFriendsInteractionSystem.HeldOffset, "운반 위치");
                }
                if (state.Carrier.IsValid) Require(frame.Get<FlipFriendsPlayer>(state.Carrier).Held == entity, "역방향 관계");
            }
            var boxes = frame.Filter<FlipFriendsBox>();
            while (boxes.Next(out _, out var box)) HasBoxGround |= box.IsGrounded && !box.Carrier.IsValid && box.Velocity.Y == 0;
        }
        public void AssertComplete() => Require(HasStomp && HasPlayerCarry && HasBoxCarry && HasThrow && HasPutDown && HasBoxGround, Summary);
    }

    public static async Task Run()
    {
        await RunStomp();
        var session = new QuantumGameSession();
        session.InputSource = (tick, player) => InputFor(session.Runner?.Game.Frames.Verified, tick, player);
        QuantumRunner replay = null;
        var expected = new Dictionary<int, ulong>();
        var actual = new Dictionary<int, ulong>();
        var evidence = new Evidence();
        try
        {
            using (QuantumCallback.SubscribeManual<CallbackSimulateFinished>(callback =>
            {
                if (!callback.Frame.IsVerified || callback.Frame.Number > 1440) return;
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
                for (int i = 0; i < 1680 && !expected.ContainsKey(1440); i++) session.Runner.Service(1f / 60);
                Status = evidence.Summary;
                File.WriteAllText("Logs/QuantumInteractionTrace.txt", string.Join("\n", evidence.Trace));
                evidence.AssertComplete();
                var record = session.Runner.Game.GetRecordedReplay(includeChecksums: true);
                var serializer = new QuantumUnityJsonSerializer();
                replay = QuantumRunner.StartGame(new SessionRunner.Arguments
                {
                    RunnerId = "FF_INTERACTION_REPLAY", RuntimeConfig = serializer.ConfigFromByteArray<RuntimeConfig>(record.RuntimeConfigData.Decode(), compressed: true),
                    SessionConfig = record.DeterministicConfig, ReplayProvider = record.CreateInputProvider(), InitialTick = record.InitialTick, FrameData = record.InitialFrameData,
                    GameMode = DeterministicGameMode.Replay, PlayerCount = 2
                });
                replay.IsSessionUpdateDisabled = true;
                for (int i = 0; i < 1680 && !actual.ContainsKey(1440); i++) replay.Service(1f / 60);
                Require(expected.Count == 12 && actual.Count == 12 && expected.All(p => actual[p.Key] == p.Value), "상호작용 재생 12개 확정 체크섬");
                Status = "통과: 상호작용 경계 검사·새 입력 재생12개" + evidence.Summary;
                File.WriteAllText("Logs/QuantumInteractionChecks.txt", Status + "\n" + string.Join("\n", expected.Select(p => p.Key + ": " + p.Value)));
                File.WriteAllText("Logs/QuantumInteractionReplay.json", UnityEngine.JsonUtility.ToJson(record));
            }
        }
        finally
        {
            if (replay != null) await replay.ShutdownAsync();
            await session.StopAsync();
        }
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
                    return isFinishing ? FinishInput(session, slot) : InputFor(session.Runner.Game.Frames.Verified, tick, (int)locals[slot]);
                };
            using (QuantumCallback.SubscribeManual<CallbackSimulateFinished>(callback =>
            {
                if (!callback.Frame.IsVerified) return;
                for (int i = 0; i < 2; i++)
                    if (callback.Game == sessions[i].Runner?.Game && callback.Frame.Number <= 1440)
                    {
                        evidence[i].Observe(callback.Frame);
                        if (callback.Frame.Number % 120 == 0) checks[i][callback.Frame.Number] = callback.Frame.CalculateChecksum();
                    }
            }))
            {
                string room = "ff-interaction-" + Guid.NewGuid().ToString("N");
                var first = sessions[0].StartDevelopmentAsync(Config(), room, false);
                await Wait(() => sessions[0].Client?.InRoom == true || first.IsFaulted, 15000);
                if (first.IsFaulted) await first;
                var second = sessions[1].StartDevelopmentAsync(Config(), room, true);
                await Task.WhenAll(first, second);
                clients[0] = sessions[0].Client; clients[1] = sessions[1].Client;
                await Wait(() => checks.All(check => check.ContainsKey(1440)), 45000);
                File.WriteAllText("Logs/QuantumInteractionCloudTrace.txt", string.Join("\n", evidence[0].Trace));
                foreach (var item in evidence) item.AssertComplete();
                var samples = checks[0].Where(pair => pair.Key >= 240).ToArray();
                Require(samples.Length == 11 && samples.All(p => checks[1].TryGetValue(p.Key, out var value) && value == p.Value), "개발 Photon 상호작용 확정 체크섬11개");
                isFinishing = true;
                await Wait(() => sessions.All(session => session.Runner.Game.Frames.Verified.FlipFriendsCompletedFrame > 0), 25000);
                int completed = sessions[0].Runner.Game.Frames.Verified.FlipFriendsCompletedFrame;
                Require(completed == sessions[1].Runner.Game.Frames.Verified.FlipFriendsCompletedFrame, "상호작용 Photon 전원 완료 프레임 일치");
                var result = $"통과: 새 상호작용 Photon 두 연결, 완료={completed}, 지역={clients[0].CurrentRegion}, RTT={clients[0].RealtimePeer.Stats.RoundtripTime}/{clients[1].RealtimePeer.Stats.RoundtripTime}ms, 같은 PC, 지연/손실 주입 없음\n" +
                    evidence[0].Summary + "\n" + evidence[1].Summary + "\n" + string.Join("\n", samples.Select(p => p.Key + ": " + p.Value));
                File.WriteAllText("Logs/QuantumInteractionCloud.txt", result);
                return result;
            }
        }
        finally
        {
            foreach (var session in sessions) await session.StopAsync();
            Require(sessions.All(session => session.Runner == null && session.Client == null && !session.HasInputSubscription) && clients.All(client => client == null || !client.IsConnected), "상호작용 Photon 종료 정리");
        }
    }

    public static Quantum.Input FinishInput(QuantumGameSession session, int slot)
    {
        var frame = session.Runner.Game.Frames.Verified;
        if (frame == null) return default;
        var locals = session.Runner.Game.GetLocalPlayers();
        if (locals.Count <= slot) return default;
        var query = frame.Filter<Transform2D, FlipFriendsPlayer>();
        while (query.Next(out _, out var transform, out var player))
            if (player.Player == locals[slot])
            {
                var distance = frame.RuntimeConfig.GoalCenters[0].X - transform.Position.X;
                bool isNearBox = false;
                var boxes = frame.Filter<Transform2D, FlipFriendsBox>();
                while (boxes.Next(out _, out var bt, out var box)) isNearBox |= !box.Carrier.IsValid && FPMath.Abs(bt.Position.X - transform.Position.X) < FP.FromString("1.4");
                return new Quantum.Input { Horizontal = FPMath.Clamp(distance * 2 - player.Velocity.X, -FP._1, FP._1), Jump = isNearBox && frame.Number % 90 < 40, Vertical = FPMath.Abs(distance) < FP.FromString("0.3") ? 1 : 0 };
            }
        return default;
    }

    private static async Task Wait(Func<bool> condition, int milliseconds)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(milliseconds);
        while (!condition() && DateTime.UtcNow < deadline && EditorApplication.isPlaying) await Task.Delay(20);
        Require(condition(), "상호작용 비동기 제한 시간");
    }

    private static void CheckCarry(Frame frame)
    {
        var players = frame.Filter<FlipFriendsPlayer>();
        Require(players.Next(out var a, out var ap), "운반 플레이어1");
        Require(players.Next(out var b, out var bp), "운반 플레이어2");
        ap = new FlipFriendsPlayer { Player = ap.Player, Facing = 1 };
        bp = new FlipFriendsPlayer { Player = bp.Player, Facing = -1 };
        frame.Set(a, ap); frame.Set(b, bp);
        frame.Set(a, new Transform2D { Position = new FPVector2(100, 1) });
        frame.Set(b, new Transform2D { Position = new FPVector2(FP.FromString("100.95"), 1) });
        var boxEntity = frame.Create();
        frame.Add(boxEntity, new Transform2D { Position = new FPVector2(FP.FromString("100.95"), 1) });
        frame.Add(boxEntity, new FlipFriendsBox { HalfSize = new FPVector2(FP.FromString("0.45"), FP.FromString("0.45")) });
        try
        {
            Require(FlipFriendsInteractionSystem.TryPickup(frame, a) && frame.Get<FlipFriendsPlayer>(a).Held == b, "플레이어 우선");
            Require(!FlipFriendsInteractionSystem.TryPickup(frame, b) && !FlipFriendsInteractionSystem.TryPickup(frame, a), "자기/순환/중복 운반 금지");
            var carried = frame.Get<FlipFriendsPlayer>(b);
            Require(carried.Carrier == a && carried.EscapeFrame == frame.Number + 90, "운반 관계·탈출90틱");
            var frozen = frame.Get<Transform2D>(b); var oldPosition = frozen.Position; var oldState = carried;
            FlipFriendsMovementSystem.Simulate(frame, ref frozen, ref carried, new Quantum.Input { Jump = true, Horizontal = 1, Interact = true });
            Require(frozen.Position == oldPosition && carried.Equals(oldState), "들린 동안 이동·입력 무시");
            Require(!FlipFriendsInteractionSystem.Interact(frame, a, false), "해제9틱 전 요청 무시");
            Require(!FlipFriendsInteractionSystem.RequestEscape(frame, b), "제한 전 탈출 거부");
            carried.EscapeFrame = frame.Number;
            frame.Set(b, carried);
            Require(FlipFriendsInteractionSystem.RequestEscape(frame, b), "제한 후 탈출");
            carried = frame.Get<FlipFriendsPlayer>(b);
            Require(!carried.Carrier.IsValid && !frame.Get<FlipFriendsPlayer>(a).Held.IsValid && carried.Velocity == new FPVector2(7, 12) && carried.UncontrollableTicks == 24, "플레이어 던지기·정리");
            var thrown = frame.Get<Transform2D>(b); FP smooth = 0;
            var expected = FlipFriendsMovementSystem.SmoothHorizontal(7, -4, ref smooth, 1, frame.DeltaTime);
            FlipFriendsMovementSystem.Simulate(frame, ref thrown, ref carried, new Quantum.Input { Horizontal = -1 });
            Require(carried.Velocity.X == expected && carried.Velocity.X > 0, "던진 뒤 입력·1초 공중 스무딩");
            frame.Set(b, new Transform2D { Position = new FPVector2(110, 1) });
            Require(FlipFriendsInteractionSystem.TryPickup(frame, a) && frame.Get<FlipFriendsPlayer>(a).Held == boxEntity, "상자 들기");
            Require(!FlipFriendsInteractionSystem.TryPickup(frame, b), "다른 플레이어 중복 소유 금지");
            Require(FlipFriendsInteractionSystem.Release(frame, a, true), "상자 놓기");
            Require(!frame.Get<FlipFriendsBox>(boxEntity).Carrier.IsValid && frame.Get<FlipFriendsBox>(boxEntity).Velocity == default, "놓기 속도·관계");
            var carrierState = frame.Get<FlipFriendsPlayer>(a);
            Require(FlipFriendsInteractionSystem.LimitBoxes(frame, frame.Get<Transform2D>(a).Position, FlipFriendsInteractionSystem.HalfSize(carrierState), 1, true, carrierState.Held) < 1, "해제 뒤 운반자 상자 충돌 복원");
            frame.Set(boxEntity, new Transform2D { Position = new FPVector2(FP.FromString("100.95"), 1) });
            Require(FlipFriendsInteractionSystem.TryPickup(frame, a), "상자 다시 들기");
            var blocker = frame.Create();
            frame.Add(blocker, new Transform2D { Position = new FPVector2(100, 1) });
            frame.Add(blocker, new FlipFriendsBox { HalfSize = new FPVector2(2, 2) });
            Require(!FlipFriendsInteractionSystem.Release(frame, a, true) && frame.Get<FlipFriendsBox>(boxEntity).Carrier == a, "빈 공간 없음·운반 유지");
            frame.Destroy(blocker);
            Require(FlipFriendsInteractionSystem.Release(frame, a, false) && frame.Get<FlipFriendsBox>(boxEntity).Velocity == new FPVector2(6, 6), "상자 던지기6/6");
            frame.Set(a, new Transform2D { Position = new FPVector2(FP.FromString("4.6"), FP.FromString("0.5")) });
            frame.Set(boxEntity, new Transform2D { Position = new FPVector2(FP.FromString("4.6"), FP.FromString("1.45")) });
            var boxState = frame.Get<FlipFriendsBox>(boxEntity); boxState.Carrier = a; frame.Set(boxEntity, boxState);
            var holder = frame.Get<FlipFriendsPlayer>(a); holder.Held = boxEntity; holder.Facing = 1; frame.Set(a, holder);
            Require(FlipFriendsInteractionSystem.Release(frame, a, true), "벽 근처 놓기 후보");
            var released = frame.Get<Transform2D>(boxEntity).Position;
            Require(released == new FPVector2(FP.FromString("4.6"), FP.FromString("0.5")) + FlipFriendsInteractionSystem.HeldOffset, "벽에서 앞 후보 거부·머리 위 선택");
            frame.Set(a, new Transform2D { Position = new FPVector2(0, FP.FromString("4.5")) });
            holder = frame.Get<FlipFriendsPlayer>(a); holder.Held = boxEntity; frame.Set(a, holder);
            boxState = frame.Get<FlipFriendsBox>(boxEntity); boxState.Carrier = a; frame.Set(boxEntity, boxState);
            Require(FlipFriendsInteractionSystem.Release(frame, a, false), "천장 근처 상자 던지기 후보");
            released = frame.Get<Transform2D>(boxEntity).Position;
            Require(released.X > 0 && released.Y < 5 && FlipFriendsInteractionSystem.CanOccupy(frame, released, boxState.HalfSize, a, boxEntity), "천장 후보 거부·앞쪽 안전 위치");
        }
        finally { frame.Destroy(boxEntity); }
    }

    private static void CheckStomp(Frame frame)
    {
        var players = frame.Filter<Transform2D, FlipFriendsPlayer>();
        Require(players.Next(out var a, out var at, out var ap), "첫 플레이어");
        Require(players.Next(out var b, out var bt, out var bp), "두번째 플레이어");
        ap = new FlipFriendsPlayer { Player = ap.Player, PreviousPosition = new FPVector2(100, 2) };
        bp = new FlipFriendsPlayer { Player = bp.Player, PreviousPosition = new FPVector2(100, 1) };
        at.Position = ap.PreviousPosition;
        bt.Position = bp.PreviousPosition;
        frame.Set(a, ap); frame.Set(b, bp); frame.Set(a, at); frame.Set(b, bt);
        Require(FlipFriendsInteractionSystem.LimitPlayers(frame, ref ap, at.Position, -FP._1) == 0 && ap.StompTarget == b, "머리 진입");
        frame.Set(a, ap);
        new FlipFriendsInteractionSystem().Update(frame);
        ap = frame.Get<FlipFriendsPlayer>(a); bp = frame.Get<FlipFriendsPlayer>(b);
        Require(ap.Velocity.Y == FP.FromString("13.2") && ap.StompCount == 1 && ap.JumpBlockTicks == 18 && ap.ClimbBlockTicks == 18, "기본 튕김/차단");
        Require(bp.ShrinkTicks == 30 && bp.JumpBlockTicks == 9 && FlipFriendsInteractionSystem.HalfSize(bp).Y == FP.FromString("0.25"), "밟힌 쪽 Shrink 판정");
        ap.StompTarget = b;
        frame.Set(a, ap);
        new FlipFriendsInteractionSystem().Update(frame);
        ap = frame.Get<FlipFriendsPlayer>(a); bp = frame.Get<FlipFriendsPlayer>(b);
        Require(ap.StompCount == 1, "차단 중 반복 접촉");
        for (int i = 0; i < 30; i++) new FlipFriendsInteractionSystem().Update(frame);
        bp = frame.Get<FlipFriendsPlayer>(b);
        Require(bp.ShrinkTicks == 0 && FlipFriendsInteractionSystem.HalfSize(bp).Y == FP.FromString("0.5"), "판정 복원");
        ap.StompTarget = default;
        Require(FlipFriendsInteractionSystem.LimitPlayers(frame, ref ap, new FPVector2(102, 2), -FP._1) == -FP._1 && !ap.StompTarget.IsValid, "옆 비접촉");
        Require(FlipFriendsInteractionSystem.LimitPlayers(frame, ref ap, at.Position, FP._1) == FP._1, "상향 비대상");
        ap.JumpBlockTicks = 0; ap.StompTarget = b; frame.Set(a, ap);
        Require(FlipFriendsInteractionSystem.TryStomp(frame, a, new Quantum.Input { Jump = true }) &&
            frame.Get<FlipFriendsPlayer>(a).Velocity.Y == FP.FromString("13.2") * FP.FromString("1.2"), "유지 입력 튕김 배율");
        var third = frame.Create();
        frame.Add(third, new Transform2D { Position = new FPVector2(FP.FromString("100.2"), 2) });
        frame.Add(third, new FlipFriendsPlayer { StompTarget = b });
        Require(FlipFriendsInteractionSystem.TryStomp(frame, third, default) && frame.Get<FlipFriendsPlayer>(b).ShrinkTicks == 30, "동시 두 밟기·축소 갱신");
        Require(FPMath.Abs(frame.Get<FlipFriendsPlayer>(third).Velocity.X - FP.FromString("0.1")) <= FP.FromRaw(1), "수평 밀림");
        frame.Destroy(third);
    }

    private static void Require(bool condition, string label)
    {
        if (!condition) throw new InvalidOperationException("Quantum 상호작용 검사: " + label);
    }
}
