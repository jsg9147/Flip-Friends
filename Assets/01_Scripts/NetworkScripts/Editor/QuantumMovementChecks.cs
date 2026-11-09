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

public static class QuantumMovementChecks
{
    public static string Summary { get; private set; }

    public static Quantum.Input InputFor(int frame, int player)
    {
        int tick = frame - 240;
        int direction = player == 0 ? 1 : -1;
        return new Quantum.Input
        {
            Horizontal = tick < 0 ? 0 : tick < 120 ? direction : tick < 240 ? 0 : tick < 420 ? -direction : tick < 540 ? direction : 0,
            Run = tick >= 300 && tick < 480,
            Jump = tick >= 30 && tick < 32 || tick >= 150 && tick < 220 || tick >= 390 && tick < 420
        };
    }

    public static async Task Run()
    {
        var config = new RuntimeConfig();
        QuantumGameContent.Prepare(AssetDatabase.LoadAssetAtPath<QuantumCustomMapAsset>(QuantumRopeTestSetup.ManifestPath), config);
        Require(config.CustomMapAgreement.Contains(":제품13:") && QuantumGameContent.NetworkVersion == "ff-product-13", "새 이동 규칙 버전");
        bool hasRejectedOldRules = false;
        try { QuantumMapAgreement.RequireMatch(config.CustomMapAgreement, config.CustomMapAgreement.Replace(":제품13:", ":제품1:")); }
        catch (InvalidOperationException) { hasRejectedOldRules = true; }
        Require(hasRejectedOldRules, "이전 제품 규칙 합의 거부");
        var session = new QuantumGameSession { InputSource = InputFor };
        QuantumRunner replay = null;
        var expected = new Dictionary<int, ulong>();
        var actual = new Dictionary<int, ulong>();
        var evidence = new Evidence();
        try
        {
            using (QuantumCallback.SubscribeManual<CallbackSimulateFinished>(callback =>
            {
                if (!callback.Frame.IsVerified || callback.Frame.Number > 960) return;
                if (callback.Game == session.Runner?.Game)
                {
                    evidence.Observe(callback.Frame);
                    if (callback.Frame.Number % 120 == 0) expected[callback.Frame.Number] = callback.Frame.CalculateChecksum();
                }
                if (callback.Game == replay?.Game && callback.Frame.Number % 120 == 0)
                    actual[callback.Frame.Number] = callback.Frame.CalculateChecksum();
            }))
            {
                await session.StartLocalAsync(config);
                var runner = session.Runner;
                runner.IsSessionUpdateDisabled = true;
                for (int i = 0; i < 1200 && !expected.ContainsKey(960); i++) runner.Service(1f / 60);
                evidence.AssertComplete();
                CheckTransitions(runner.Game.Frames.Verified);
                var record = runner.Game.GetRecordedReplay(includeChecksums: true);
                var serializer = new QuantumUnityJsonSerializer();
                replay = QuantumRunner.StartGame(new SessionRunner.Arguments
                {
                    RunnerId = "FF_MOVEMENT_REPLAY", RuntimeConfig = serializer.ConfigFromByteArray<RuntimeConfig>(record.RuntimeConfigData.Decode(), compressed: true),
                    SessionConfig = record.DeterministicConfig, ReplayProvider = record.CreateInputProvider(),
                    InitialTick = record.InitialTick, FrameData = record.InitialFrameData,
                    GameMode = DeterministicGameMode.Replay, PlayerCount = 2
                });
                replay.IsSessionUpdateDisabled = true;
                for (int i = 0; i < 1200 && !actual.ContainsKey(960); i++) replay.Service(1f / 60);
                Require(expected.Count == 8 && actual.Count == 8 && expected.All(p => actual[p.Key] == p.Value), "새 이동 입력 재생 8개 체크섬");
                Summary += "\n" + evidence.Summary + "\n" + string.Join("\n", expected.Select(p => p.Key + ": " + p.Value));
                File.WriteAllText("Logs/QuantumMovementChecks.txt", Summary);
                File.WriteAllText("Logs/QuantumMovementReplay.json", JsonUtility.ToJson(record));
            }
        }
        finally
        {
            if (replay != null) await replay.ShutdownAsync();
            await session.StopAsync();
            Require(session.Runner == null && !session.HasInputSubscription, "이동 검사 자원 정리");
        }
    }

    private static void CheckTransitions(Frame frame)
    {
        CheckKeyboard();
        var position = new Transform2D { Position = new FPVector2(0, FP.FromString("0.5")) };
        var state = new FlipFriendsPlayer { IsGrounded = true };
        var dt = frame.DeltaTime;
        var minSpeed = FPMath.Sqrt(FP.FromString("105.6"));
        var jumpSpeed = FP.FromString("13.2");
        var gravity = FP.FromString("44");
        void Step(Quantum.Input input) => FlipFriendsMovementSystem.Simulate(frame, ref position, ref state, input);
        void Reset(FP x, FP y)
        {
            position = new Transform2D { Position = new FPVector2(x, y) };
            state = new FlipFriendsPlayer { IsGrounded = true };
        }
        float referenceSpeed = 0, referenceSmoothing = 0, maxError = 0;
        for (int tick = 0; tick < 600; tick++)
        {
            int direction = tick < 120 ? 1 : tick < 240 ? 0 : tick < 360 ? -1 : tick < 480 ? 1 : 0;
            bool isRunning = tick >= 240 && tick < 480;
            float smooth = Mathf.Sign(direction) != Mathf.Sign(referenceSpeed) ? 0.4f * 0.7f : 0.4f;
            referenceSpeed = Mathf.SmoothDamp(referenceSpeed, direction * (isRunning ? 5.5f : 4f), ref referenceSmoothing, smooth, Mathf.Infinity, dt.AsFloat);
            Step(new Quantum.Input { Horizontal = direction, Run = isRunning });
            maxError = Mathf.Max(maxError, Mathf.Abs(referenceSpeed - state.Velocity.X.AsFloat));
            Require(maxError < 0.003f, "동일 DeltaTime의 Unity SmoothDamp 대조 " + tick);
        }
        Reset(0, FP.FromString("0.5"));
        Step(new Quantum.Input { Horizontal = 1 });
        Require(state.Velocity.X > 0 && state.Velocity.X < 1 && state.VelocityXSmoothing > 0, "정지에서 가속");
        for (int i = 0; i < 90; i++) Step(new Quantum.Input { Horizontal = 1 });
        Require(state.Velocity.X > FP.FromString("3.9") && state.Velocity.X <= 4, "걷기 속도 수렴");
        var speed = state.Velocity.X;
        Step(default);
        Require(state.Velocity.X > 0 && state.Velocity.X < speed && state.VelocityXSmoothing < 0, "해제 후 누적 감속");
        for (int i = 0; i < 180; i++) Step(default);
        Require(FPMath.Abs(state.Velocity.X) < FP.FromString("0.001"), "해제 후 정지 수렴");
        Reset(0, FP.FromString("0.5"));
        state.Velocity.X = 4;
        Step(new Quantum.Input { Horizontal = -1 });
        Require(state.Velocity.X > 0 && state.Velocity.X < 4 && state.VelocityXSmoothing < 0, "반전 시 관성·누적값");
        for (int i = 0; i < 90; i++) Step(new Quantum.Input { Horizontal = -1 });
        Require(state.Velocity.X < FP.FromString("-3.9"), "좌측 반전 완료");
        Reset(0, FP.FromString("0.5"));
        for (int i = 0; i < 120; i++) Step(new Quantum.Input { Horizontal = 1, Run = true });
        Require(state.Velocity.X > FP.FromString("5.4") && state.Velocity.X <= FP.FromString("5.5"), "달리기 속도 수렴");
        speed = state.Velocity.X;
        Step(new Quantum.Input { Horizontal = 1 });
        Require(state.Velocity.X < speed && state.Velocity.X > 4, "달리기 해제 감속");
        Reset(0, 3);
        state.IsGrounded = false;
        Step(new Quantum.Input { Horizontal = 1, Run = true });
        var air = state;
        Reset(0, FP.FromString("0.5"));
        Step(new Quantum.Input { Horizontal = 1, Run = true });
        Require(state.Velocity.X == air.Velocity.X && state.VelocityXSmoothing == air.VelocityXSmoothing, "공중/지상 동일 가속");
        for (int i = 0; i < 3; i++)
        {
            Reset(0, FP.FromString("0.5"));
            state.Velocity.X = i == 0 ? 0 : FP.FromString("5.5");
            Step(new Quantum.Input { Run = i != 2, Jump = true });
            Require(FPMath.Abs(state.Velocity.Y - (jumpSpeed + (i == 1 ? 2 : 0) - gravity * dt)) <= FP.FromRaw(2), "달리기 보너스의 키·이전 속도 조건 " + i);
        }
        Reset(0, FP.FromString("0.5"));
        state.Velocity.X = FP.FromString("4.75");
        Step(new Quantum.Input { Run = true, Jump = true });
        Require(FPMath.Abs(state.Velocity.Y - (jumpSpeed + 1 - gravity * dt)) <= FP.FromRaw(2), "달리기 보너스 중간 비율");
        Reset(0, FP.FromString("0.5"));
        Step(new Quantum.Input { Jump = true });
        Step(default);
        Require(FPMath.Abs(state.Velocity.Y - (minSpeed - gravity * dt)) <= FP.FromRaw(2), "점프 해제 컷·중력 순서");
        FP Height(int heldTicks)
        {
            Reset(0, FP.FromString("0.5"));
            FP top = position.Position.Y;
            int launches = 0;
            for (int tick = 0; tick < 150; tick++)
            {
                var old = state.Velocity.Y;
                Step(new Quantum.Input { Jump = tick < heldTicks });
                if (old <= 0 && state.Velocity.Y > 0) launches++;
                top = FPMath.Max(top, position.Position.Y);
            }
            Require(launches == 1 && state.IsGrounded, "점프 유지 중 재착지 중복 점프 금지");
            return top - FP.FromString("0.5");
        }
        var shortHeight = Height(1);
        var longHeight = Height(150);
        Require(longHeight > shortHeight + FP.FromString("0.4"), "짧은/긴 점프 높이 차이");
        Reset(FP.FromString("-5.1"), 1);
        state.IsClimbed = true;
        state.Velocity.X = FP.FromString("-5.5");
        Step(new Quantum.Input { Vertical = 1, Run = true, Jump = true });
        Require(!state.IsClimbed && state.ClimbBlockTicks == 17 && state.Velocity.Y == jumpSpeed - gravity * dt, "로프 분리에는 달리기 보너스 없음");
        for (int tick = 16; tick >= 0; tick--)
        {
            Step(new Quantum.Input { Vertical = 1, Jump = true });
            Require(state.ClimbBlockTicks == tick && (tick == 0 || !state.IsClimbed), "18틱 재등반 제한 " + tick);
        }
        Require(state.IsClimbed, "제한 만료 뒤 접촉 재진입");
        Reset(5, 1);
        state.Velocity.X = 4;
        state.VelocityXSmoothing = 2;
        for (int tick = 0; tick < 20; tick++) Step(new Quantum.Input { Horizontal = 1 });
        Require(state.IsTouchingRight && state.Velocity.X > 0 && state.VelocityXSmoothing != 0, "벽 충돌은 수평 속도·스무딩을 초기화하지 않음");
        Reset(0, FP.FromString("4.4"));
        state.IsGrounded = false;
        state.Velocity = new FPVector2(2, 8);
        state.VelocityXSmoothing = 1;
        Step(new Quantum.Input { Horizontal = 1 });
        Require(state.IsTouchingCeiling && state.Velocity.Y == 0 && state.VelocityXSmoothing != 0, "천장은 Y만 초기화");
        Step(default);
        Require(state.Velocity.Y < 0, "천장 충돌 다음 틱 중력");
        Reset(4, FP.FromString("0.5"));
        state.Velocity.X = 3;
        state.VelocityXSmoothing = 2;
        Step(new Quantum.Input { Vertical = 1, Horizontal = 1 });
        Require(state.IsFinished && state.Velocity == FPVector2.Zero && state.VelocityXSmoothing == 0, "도착 시 누적값 정리");
        var finishedPosition = position.Position;
        var finishedState = state;
        Step(new Quantum.Input { Jump = true, Run = true, Horizontal = -1, Interact = true });
        Require(position.Position == finishedPosition && state.Equals(finishedState), "도착 후 모든 입력 무시");
        Summary = $"통과: 규칙 버전 분리·키보드 두 슬롯·가속·감속·반전·달리기·공중·보너스 조건·해제 컷·유지 중복 금지·로프18틱·벽/천장 누적 상태·도착 정지\n짧은 점프={shortHeight}, 긴 점프={longHeight}, DeltaTime={dt}(원시 {dt.RawValue}), Unity SmoothDamp 600틱 최대 속도 오차={maxError}";
    }

    private static void CheckKeyboard()
    {
        var original = UnityEngine.InputSystem.Keyboard.current;
        var keyboard = UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Keyboard>();
        try
        {
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(keyboard, new UnityEngine.InputSystem.LowLevel.KeyboardState(
                UnityEngine.InputSystem.Key.D, UnityEngine.InputSystem.Key.S, UnityEngine.InputSystem.Key.Space, UnityEngine.InputSystem.Key.LeftShift, UnityEngine.InputSystem.Key.E, UnityEngine.InputSystem.Key.R));
            UnityEngine.InputSystem.InputSystem.Update();
            var first = QuantumPlayerInput.Read(0);
            var second = QuantumPlayerInput.Read(1);
            Require(first.Horizontal == 1 && first.Jump.IsDown && first.Run.IsDown && second.Horizontal == 0 && !second.Jump.IsDown && !second.Run.IsDown, "1P 키·슬롯 독립");
            Require(first.Reset.IsDown && !second.Reset.IsDown, "1P Reset 슬롯 독립");
            Require(first.Interact.IsDown && first.Vertical == -1 && !second.Interact.IsDown, "1P 아래+들기·슬롯 독립");
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(keyboard, new UnityEngine.InputSystem.LowLevel.KeyboardState(
                UnityEngine.InputSystem.Key.LeftArrow, UnityEngine.InputSystem.Key.DownArrow, UnityEngine.InputSystem.Key.RightCtrl, UnityEngine.InputSystem.Key.RightShift, UnityEngine.InputSystem.Key.Enter, UnityEngine.InputSystem.Key.Backspace));
            UnityEngine.InputSystem.InputSystem.Update();
            first = QuantumPlayerInput.Read(0); second = QuantumPlayerInput.Read(1);
            Require(first.Horizontal == 0 && !first.Jump.IsDown && !first.Run.IsDown && second.Horizontal == -1 && second.Jump.IsDown && second.Run.IsDown, "키 해제·2P 키·슬롯 독립");
            Require(!first.Reset.IsDown && second.Reset.IsDown, "Reset 해제와 2P 슬롯 독립");
            Require(!first.Interact.IsDown && second.Interact.IsDown && second.Vertical == -1, "2P 아래+들기·1P 해제");
        }
        finally
        {
            UnityEngine.InputSystem.InputSystem.RemoveDevice(keyboard);
            if (original != null) original.MakeCurrent();
        }
    }

    public sealed class Evidence
    {
        private readonly HashSet<string>[] passed = { new(), new() };
        private readonly FlipFriendsPlayer[] previous = new FlipFriendsPlayer[2];
        private static readonly string[] Required = { "가속", "감속", "반전", "달리기", "공중", "해제컷" };
        public string Summary => string.Join("\n", Enumerable.Range(0, 2).Select(i => $"플레이어 {i}: {string.Join(",", passed[i])}"));
        public void Observe(Frame frame)
        {
            var query = frame.Filter<Transform2D, FlipFriendsPlayer>();
            while (query.Next(out var entity, out var transform, out var player))
            {
                int slot = (int)player.Player;
                var old = previous[slot];
                void Mark(bool value, string key) { if (value) passed[slot].Add(key); }
                Mark(FPMath.Abs(player.Velocity.X) > 0 && FPMath.Abs(player.Velocity.X) < 1 && player.VelocityXSmoothing != 0, "가속");
                Mark(old.Velocity.X * old.VelocityXSmoothing < 0 && FPMath.Abs(player.Velocity.X) < FPMath.Abs(old.Velocity.X), "감속");
                Mark(old.Velocity.X * player.Velocity.X < 0, "반전");
                Mark(FPMath.Abs(player.Velocity.X) > 4, "달리기");
                Mark(!player.IsGrounded && !player.IsClimbed && player.VelocityXSmoothing != 0 && player.Velocity.Y > 0, "공중");
                Mark(old.WasJumpHeld && !player.WasJumpHeld && old.Velocity.Y > FPMath.Sqrt(FP.FromString("105.6")) && player.Velocity.Y > 0 && player.Velocity.Y < old.Velocity.Y, "해제컷");
                previous[slot] = player;
            }
        }
        public void AssertComplete()
        {
            for (int i = 0; i < 2; i++) Require(Required.All(passed[i].Contains), "확정 이동 전이 누락 " + i + ": " + Summary);
        }
    }

    private static void Require(bool value, string label)
    {
        if (!value) throw new InvalidOperationException("이동 자동 검사 실패: " + label);
    }
}
