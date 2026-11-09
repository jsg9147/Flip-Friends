namespace Quantum
{
    using Photon.Deterministic;

    // 원본 Conveyor/MovementHandler의 외부 속도를 FP 프레임 상태로 옮긴다. 판정·운반은 이동 장치의 고체 발판이 맡는다.
    public static unsafe class FlipFriendsConveyorSystem
    {
        // Player.prefab의 conveyorSpeed1·conveyorAccelerationSpeed1.
        private static readonly FP Speed = 1;
        private static readonly FP Acceleration = 1;
        private static readonly FP GroundDecay = 5;
        // 원본은 외부 속도를 50Hz 틱마다 속도에 더한다. 60Hz에서도 초당 가산량이 같도록 50×DeltaTime을 곱한다.
        private static readonly FP LegacyTickRate = 50;
        private static readonly FP Tolerance = FP.FromRaw(16);

        public static void Initialize(Frame frame)
        {
            var configs = frame.RuntimeConfig.Conveyors;
            if (configs == null) return;
            for (int i = 0; i < configs.Length; i++)
            {
                EntityRef motion = default;
                var motions = frame.Filter<FlipFriendsMotion>();
                while (motions.Next(out var entity, out var state))
                    if (state.ContentIndex == configs[i].Motion) { motion = entity; break; }
                var e = frame.Create();
                frame.Add(e, new FlipFriendsConveyor { ContentIndex = i, Motion = motion, Clockwise = configs[i].InitialClockwise });
            }
        }

        // 벨트만 있는 그룹의 수동 반전. 같은 틱의 여러 요청은 홀짝으로 합산되어 Update에서 한 번 적용된다.
        public static void Toggle(Frame frame, int anchor)
        {
            var belts = frame.Filter<FlipFriendsConveyor>();
            while (belts.NextUnsafe(out _, out var belt))
                if (belt->ContentIndex == anchor) { belt->Cycle ^= 1; return; }
        }

        public static bool GroupOdd(Frame frame, FlipFriendsConveyorConfig c)
        {
            if (string.IsNullOrEmpty(c.Group)) return false;
            if (c.TerrainAnchor >= 0)
            {
                // 지형이 있는 그룹은 실제 적용된 상태를 따른다. 점유 대기 중에는 지형과 함께 이전 방향을 유지한다.
                var motions = frame.Filter<FlipFriendsMotion>();
                while (motions.Next(out _, out var s))
                    if (s.ContentIndex == c.TerrainAnchor) return s.Collidable != frame.RuntimeConfig.Motions[c.TerrainAnchor].InitialActive;
                return false;
            }
            bool odd = c.GroupTicks > 0 && ((frame.Number - frame.FlipFriendsMotionStartedFrame) / c.GroupTicks & 1) != 0;
            var belts = frame.Filter<FlipFriendsConveyor>();
            while (belts.Next(out _, out var anchor))
                if (anchor.ContentIndex == c.BeltAnchor) { odd ^= anchor.Cycle != 0; break; }
            return odd;
        }

        public static void Update(Frame frame)
        {
            var configs = frame.RuntimeConfig.Conveyors;
            if (configs == null || configs.Length == 0) return;
            var belts = frame.Filter<FlipFriendsConveyor>();
            while (belts.NextUnsafe(out _, out var belt))
            {
                var c = configs[belt->ContentIndex];
                bool clockwise = c.InitialClockwise != GroupOdd(frame, c);
                if (clockwise != belt->Clockwise) belt->Revision++;
                belt->Clockwise = clockwise;
            }
            var switches = frame.RuntimeConfig.StageSwitches;
            if (switches == null) return;
            var buttons = frame.Filter<FlipFriendsStageSwitch>();
            while (buttons.NextUnsafe(out _, out var button))
            {
                var config = switches[button->ContentIndex];
                if (config.Target < 0) button->Active = GroupOdd(frame, configs[config.ConveyorAnchor]);
            }
        }

        public static bool TryClockwise(Frame frame, int index, out bool clockwise)
        {
            clockwise = false;
            var belts = frame.Filter<FlipFriendsConveyor>();
            while (belts.Next(out _, out var belt))
                if (belt.ContentIndex == index) { clockwise = belt.Clockwise; return true; }
            return false;
        }

        // 원본 CalculateMovement 순서: SmoothDamp·중력 뒤 외부 속도를 가감속하고, 로프 덮어쓰기 뒤 속도에 더한다.
        // 접촉은 이전 틱 이동 결과이며 벨트 방향은 이번 틱 전환을 반영한 값이다.
        public static void ApplyExternal(Frame frame, ref FlipFriendsPlayer state)
        {
            if (frame.RuntimeConfig.Conveyors == null || frame.RuntimeConfig.Conveyors.Length == 0) return;
            var dt = frame.DeltaTime;
            if (state.ConveyorContact > 0 && TryClockwise(frame, state.ConveyorContact - 1, out bool clockwise))
                state.ConveyorVelocityX = FPMath.Clamp(state.ConveyorVelocityX + (clockwise ? -Speed : Speed) * dt * Acceleration, -Speed, Speed);
            else
                state.ConveyorVelocityX -= state.ConveyorVelocityX * FPMath.Clamp(state.IsGrounded ? dt * GroundDecay : dt, 0, 1);
            state.Velocity.X += state.ConveyorVelocityX * LegacyTickRate * dt;
        }

        // 원본은 위·아래 세로 충돌의 벨트를 모두 접촉으로 본다. 여러 벨트면 원본의 마지막(오른쪽) 레이처럼 가장 오른쪽 벨트를 쓴다.
        public static int Contact(Frame frame, FPVector2 center, FPVector2 half, bool grounded, bool ceiling)
        {
            var configs = frame.RuntimeConfig.Conveyors;
            if (configs == null || configs.Length == 0 || !grounded && !ceiling) return 0;
            int found = 0; FP best = 0;
            var belts = frame.Filter<FlipFriendsConveyor>();
            while (belts.Next(out _, out var belt))
            {
                if (!frame.TryGet<FlipFriendsMotion>(belt.Motion, out var motion) || !motion.Collidable) continue;
                var bh = frame.RuntimeConfig.Motions[motion.ContentIndex].HalfSize;
                if (FPMath.Abs(center.X - motion.Position.X) >= half.X + bh.X - Tolerance) continue;
                bool below = grounded && FPMath.Abs(center.Y - half.Y - motion.Position.Y - bh.Y) <= Tolerance;
                bool above = ceiling && FPMath.Abs(center.Y + half.Y - motion.Position.Y + bh.Y) <= Tolerance;
                if (!below && !above || found != 0 && motion.Position.X <= best) continue;
                found = belt.ContentIndex + 1; best = motion.Position.X;
            }
            return found;
        }
    }
}
