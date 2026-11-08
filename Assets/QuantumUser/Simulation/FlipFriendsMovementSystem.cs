namespace Quantum
{
    using Photon.Deterministic;

    public unsafe class FlipFriendsMovementSystem : SystemMainThreadFilter<FlipFriendsMovementSystem.Filter>, ISignalOnPlayerAdded
    {
        private static readonly FP HalfWidth = FP.FromString("0.4");
        private static readonly FP HalfHeight = FP.FromString("0.5");
        private static readonly FP Skin = FP.FromString("0.02");
        private static readonly FP Gravity = FP.FromString("44");
        private static readonly FP JumpSpeed = FP.FromString("13.2");
        private static readonly FP MinJumpSpeed = FPMath.Sqrt(2 * Gravity * FP.FromString("1.2"));
        private static readonly FP WalkSpeed = 4;
        private static readonly FP RunSpeed = FP.FromString("5.5");
        private static readonly FP RunJumpBonus = 2;
        private static readonly FP SmoothTime = FP.FromString("0.4");
        private static readonly FP TurnMultiplier = FP.FromString("0.7");
        private const int RopeJumpClimbBlockTicks = 18;

        public struct Filter
        {
            public EntityRef Entity;
            public Transform2D* Transform;
            public FlipFriendsPlayer* Player;
        }

        public void OnPlayerAdded(Frame frame, PlayerRef player, bool firstTime)
        {
            if (!firstTime) return;
            var entity = frame.Create();
            var spawns = frame.RuntimeConfig.CustomMapSpawns;
            var position = spawns != null && spawns.Length == 2
                ? spawns[(int)player % 2] : new FPVector2((int)player * 2 - 3, 2);
            frame.Add(entity, new Transform2D { Position = position });
            frame.Add(entity, new FlipFriendsPlayer { Player = player, HomePosition = position });
        }

        public override void Update(Frame frame, ref Filter filter)
        {
            var input = frame.GetPlayerInput(filter.Player->Player);
            if (input == null) return;
            if (filter.Player->Carrier.IsValid) return;
            Simulate(frame, ref *filter.Transform, ref *filter.Player, *input);
            if (filter.Player->IsFinished && filter.Player->Held.IsValid && !FlipFriendsInteractionSystem.Release(frame, filter.Entity, true))
            {
                filter.Player->IsFinished = false;
                filter.Player->FinishedFrame = 0;
            }
        }

        public static bool TryFinish(Frame frame, ref Transform2D transform, ref FlipFriendsPlayer state, Input input)
        {
            if (state.IsFinished || state.Carrier.IsValid || FlipFriendsRespawnSystem.IsBlocked(frame, state)) return false;
            if (frame.RuntimeConfig.EnableGoals && input.Vertical > 0)
            {
                var centers = frame.RuntimeConfig.GoalCenters;
                var sizes = frame.RuntimeConfig.GoalSizes;
                for (int i = 0; i < centers.Length; i++)
                {
                    if (FPMath.Abs(transform.Position.X - centers[i].X) >= sizes[i].X / 2 + HalfWidth ||
                        FPMath.Abs(transform.Position.Y - centers[i].Y) >= sizes[i].Y / 2 + HalfHeight) continue;
                    state.IsFinished = true;
                    state.FinishedFrame = frame.Number;
                    state.Velocity = default;
                    state.VelocityXSmoothing = 0;
                    state.IsClimbed = false;
                    state.StompTarget = default;
                    state.ShrinkTicks = state.JumpBlockTicks = state.ClimbBlockTicks = state.UncontrollableTicks = 0;
                    return true;
                }
            }
            return false;
        }

        public static void Simulate(Frame frame, ref Transform2D transform, ref FlipFriendsPlayer state, Input input)
        {
            if (state.IsFinished || state.Carrier.IsValid || FlipFriendsRespawnSystem.IsBlocked(frame, state)) return;
            if ((frame.RuntimeConfig.Devices == null || frame.RuntimeConfig.Devices.Length == 0) && TryFinish(frame, ref transform, ref state, input)) return;
            var horizontal = FPMath.Clamp(input.Horizontal, -FP._1, FP._1);
            bool hasStomp = state.StompFrame == frame.Number && state.StompCount > 0;
            FP stompVelocity = state.Velocity.Y;
            if (hasStomp) state.Velocity.X -= state.StompPushX;
            var verticalInput = FPMath.Clamp(input.Vertical, -FP._1, FP._1);
            bool isJumpHeld = input.Jump.IsDown;
            // Mirror와 같이 이전 틱의 등반 상태에서 점프를 먼저 처리하고 진입 제한을 줄인다.
            // 기존 BlockJump는 반복 밟기만 막으며 일반 점프 입력에는 연결되어 있지 않다.
            if (isJumpHeld && !state.WasJumpHeld && (!state.IsClimbed || state.UncontrollableTicks == 0))
            {
                if (state.IsClimbed)
                {
                    state.Velocity.Y = JumpSpeed;
                    state.IsClimbed = false;
                    state.ClimbBlockTicks = RopeJumpClimbBlockTicks;
                }
                else if (state.IsGrounded)
                {
                    state.Velocity.Y = JumpSpeed;
                    if (input.Run.IsDown)
                        state.Velocity.Y += RunJumpBonus * FPMath.Clamp((FPMath.Abs(state.Velocity.X) - WalkSpeed) / (RunSpeed - WalkSpeed), 0, 1);
                }
            }
            if (!isJumpHeld && state.WasJumpHeld && state.Velocity.Y > MinJumpSpeed)
                state.Velocity.Y = MinJumpSpeed;
            state.WasJumpHeld = isJumpHeld;
            if (state.ClimbBlockTicks > 0) state.ClimbBlockTicks--;
            state.RopeContactCount = CountRopeContacts(frame, transform.Position);
            if (state.RopeContactCount == 0) state.IsClimbed = false;
            else if (!state.IsClimbed && verticalInput != 0 && state.ClimbBlockTicks == 0 && !state.Held.IsValid)
                state.IsClimbed = true;
            // Mathf.Sign(0)은 +1이다. 정지 입력과 음수 속도 조합도 기존 전환 시간을 따른다.
            var smoothTime = (horizontal >= 0) != (state.Velocity.X >= 0) ? SmoothTime * TurnMultiplier : SmoothTime;
            if (state.UncontrollableTicks > 0) smoothTime = 1;
            state.Velocity.X = SmoothHorizontal(state.Velocity.X, horizontal * (input.Run.IsDown ? RunSpeed : WalkSpeed),
                ref state.VelocityXSmoothing, smoothTime, frame.DeltaTime);
            state.Velocity.Y = FPMath.Max(state.Velocity.Y - Gravity * frame.DeltaTime, -12);
            if (hasStomp)
            {
                state.Velocity.Y = stompVelocity;
                state.Velocity.X += state.StompPushX;
            }
            // 기존 로프는 중심 고정이나 끝점 클램프 없이 양 축을 걷기 속도로 덮어쓴다.
            if (state.IsClimbed && state.UncontrollableTicks == 0) state.Velocity = new FPVector2(horizontal, verticalInput) * WalkSpeed;
            var delta = state.Velocity * frame.DeltaTime;
            var position = transform.Position;
            var half = FlipFriendsInteractionSystem.HalfSize(state);
            var offset = FlipFriendsInteractionSystem.Offset(state);
            delta.X = LimitAxis(frame, position + offset, delta.X, true, half);
            delta.X = FlipFriendsInteractionSystem.LimitBoxes(frame, position + offset, half, delta.X, true, state.Held);
            delta.X = FlipFriendsInteractionSystem.LimitPlayerBodies(frame, state, position + offset, half, delta.X, true);
            if (state.Held.IsValid) delta.X = LimitHeldTerrain(frame, state.Held, position, delta.X, true);
            position.X += delta.X;
            var vertical = LimitAxis(frame, position + offset, delta.Y, false, half);
            vertical = FlipFriendsInteractionSystem.LimitBoxes(frame, position + offset, half, vertical, false, state.Held);
            if (state.Held.IsValid) vertical = LimitHeldTerrain(frame, state.Held, position, vertical, false);
            vertical = FlipFriendsInteractionSystem.LimitPlayers(frame, ref state, position, vertical);
            if (vertical > 0) vertical = FlipFriendsInteractionSystem.LimitPlayerBodies(frame, state, position + offset, half, vertical, false);
            state.IsGrounded = delta.Y < 0 && vertical != delta.Y;
            state.IsTouchingCeiling = delta.Y > 0 && vertical != delta.Y;
            if (vertical != delta.Y) state.Velocity.Y = 0;
            position.Y += vertical;
            state.IsTouchingLeft = LimitAxis(frame, position + offset, -Skin, true, half) != -Skin;
            state.IsTouchingRight = LimitAxis(frame, position + offset, Skin, true, half) != Skin;
            transform.Position = position;
        }

        public static FP SmoothHorizontal(FP current, FP target, ref FP smoothing, FP smoothTime, FP deltaTime)
        {
            // 무제한 maxSpeed인 기존 SmoothDamp의 유리 근사식과 누적 미분값을 FP로 유지한다.
            var omega = 2 / smoothTime;
            var x = omega * deltaTime;
            var decay = FP._1 / (FP._1 + x + FP.FromString("0.48") * x * x + FP.FromString("0.235") * x * x * x);
            var change = current - target;
            var temp = (smoothing + omega * change) * deltaTime;
            smoothing = (smoothing - omega * temp) * decay;
            var result = target + (change + temp) * decay;
            if ((target - current > 0) == (result > target))
            {
                result = target;
                smoothing = 0;
            }
            return result;
        }

        private static int CountRopeContacts(Frame frame, FPVector2 position)
        {
            var centers = frame.RuntimeConfig.CustomMapRopeCenters;
            var sizes = frame.RuntimeConfig.CustomMapRopeSizes;
            if (centers == null || sizes == null) return 0;
            int count = 0;
            for (int i = 0; i < centers.Length; i++)
                if (FPMath.Abs(position.X - centers[i].X) < sizes[i].X / 2 + HalfWidth &&
                    FPMath.Abs(position.Y - centers[i].Y) < sizes[i].Y / 2 + HalfHeight)
                    count++;
            return count;
        }

        public static FP LimitAxis(Frame frame, FPVector2 center, FP distance, bool isHorizontal, FPVector2 half)
        {
            if (distance == 0) return 0;
            FP sign = distance > 0 ? FP._1 : -FP._1;
            var direction = isHorizontal ? new FPVector2(sign, 0) : new FPVector2(0, sign);
            var extent = isHorizontal ? half.X : half.Y;
            var side = (isHorizontal ? half.Y : half.X) - Skin;
            var allowed = FPMath.Abs(distance);
            // 좁은 테스트 지형용 세 줄 레이다. 경사·이동 발판·플레이어 상호작용은 별도 이식한다.
            for (int ray = -1; ray <= 1; ray++)
            {
                var origin = center + direction * (extent - Skin);
                if (isHorizontal) origin.Y += side * ray;
                else origin.X += side * ray;
                var hit = frame.Physics2D.Raycast(origin, direction, allowed + Skin, -1,
                    QueryOptions.HitStatics | QueryOptions.ComputeDetailedInfo);
                if (hit.HasValue)
                    allowed = FPMath.Min(allowed, FPMath.Max(0, (hit.Value.Point - origin).Magnitude - Skin));
            }
            return FlipFriendsTerrain.Limit(frame.RuntimeConfig.TerrainContours, center, half, allowed * sign, isHorizontal);
        }

        public static FP LimitHeldTerrain(Frame frame, EntityRef held, FPVector2 position, FP distance, bool horizontal)
        {
            var center = position + FlipFriendsInteractionSystem.HeldOffset;
            if (frame.TryGet<FlipFriendsBox>(held, out var box)) return LimitAxis(frame, center, distance, horizontal, box.HalfSize);
            if (frame.TryGet<FlipFriendsPlayer>(held, out var player))
                return LimitAxis(frame, center + FlipFriendsInteractionSystem.Offset(player), distance, horizontal, FlipFriendsInteractionSystem.HalfSize(player));
            return distance;
        }
    }
}
