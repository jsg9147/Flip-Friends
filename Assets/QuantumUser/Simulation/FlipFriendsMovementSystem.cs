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
            frame.Add(entity, new FlipFriendsPlayer { Player = player });
        }

        public override void Update(Frame frame, ref Filter filter)
        {
            var input = frame.GetPlayerInput(filter.Player->Player);
            if (input == null) return;
            var state = filter.Player;
            var horizontal = FPMath.Clamp(input->Horizontal, -FP._1, FP._1);
            var verticalInput = FPMath.Clamp(input->Vertical, -FP._1, FP._1);
            state->Velocity.X = horizontal * 4;
            // Mirror와 같이 이전 틱의 등반 상태에서 점프를 먼저 처리하고 진입 제한을 줄인다.
            if (input->Jump.WasPressed)
            {
                if (state->IsClimbed)
                {
                    state->Velocity.Y = JumpSpeed;
                    state->IsClimbed = false;
                    state->ClimbBlockTicks = RopeJumpClimbBlockTicks;
                }
                else if (state->IsGrounded) state->Velocity.Y = JumpSpeed;
            }
            if (state->ClimbBlockTicks > 0) state->ClimbBlockTicks--;
            state->RopeContactCount = CountRopeContacts(frame, filter.Transform->Position);
            if (state->RopeContactCount == 0) state->IsClimbed = false;
            else if (!state->IsClimbed && verticalInput != 0 && state->ClimbBlockTicks == 0)
                state->IsClimbed = true;
            state->Velocity.Y = FPMath.Max(state->Velocity.Y - Gravity * frame.DeltaTime, -12);
            // 기존 로프는 중심 고정이나 끝점 클램프 없이 양 축을 걷기 속도로 덮어쓴다.
            if (state->IsClimbed) state->Velocity = new FPVector2(horizontal, verticalInput) * 4;
            var delta = state->Velocity * frame.DeltaTime;
            var position = filter.Transform->Position;
            delta.X = LimitAxis(frame, position, delta.X, true);
            position.X += delta.X;
            var vertical = LimitAxis(frame, position, delta.Y, false);
            state->IsGrounded = delta.Y < 0 && vertical != delta.Y;
            state->IsTouchingCeiling = delta.Y > 0 && vertical != delta.Y;
            if (vertical != delta.Y) state->Velocity.Y = 0;
            position.Y += vertical;
            state->IsTouchingLeft = LimitAxis(frame, position, -Skin, true) != -Skin;
            state->IsTouchingRight = LimitAxis(frame, position, Skin, true) != Skin;
            filter.Transform->Position = position;
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

        private static FP LimitAxis(Frame frame, FPVector2 center, FP distance, bool isHorizontal)
        {
            if (distance == 0) return 0;
            FP sign = distance > 0 ? FP._1 : -FP._1;
            var direction = isHorizontal ? new FPVector2(sign, 0) : new FPVector2(0, sign);
            var extent = isHorizontal ? HalfWidth : HalfHeight;
            var side = (isHorizontal ? HalfHeight : HalfWidth) - Skin;
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
            return allowed * sign;
        }
    }
}
