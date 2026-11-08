namespace Quantum
{
    using Photon.Deterministic;

    // Mirror PlayerController2D.HandleDamage → MovementHandler.OnDamaged의 정적 트랩 피격을 이동 뒤 위치로 판정한다.
    // 원본도 이동을 마친 물리 단계의 OnTriggerStay에서 속도를 바꾸므로 넉백은 다음 틱 이동부터 반영된다.
    public unsafe class FlipFriendsHazardSystem : SystemMainThread
    {
        // Player.prefab의 damagedMove(20,20)·invincibilityDuration1초와 코드 상수 로프 차단·조작 제한 1초를 60Hz 틱으로 둔다.
        public static readonly FPVector2 KnockbackScale = new FPVector2(20, 20);
        public const int InvincibleTicks = 60;
        public const int ClimbBlockTicks = 60;
        public const int UncontrollableTicks = 60;

        public override void Update(Frame frame)
        {
            var traps = frame.RuntimeConfig.Traps;
            if (traps == null || traps.Length == 0 || frame.Global->FlipFriendsCompletedFrame > 0) return;
            // 이번 틱 시작 시 들려 있던 플레이어는 운반자가 같은 틱에 맞아 내려놓아도 다음 틱부터 판정한다. PlayerRef 처리 순서와 무관하게 하기 위함이다.
            ulong carriedMask = 0;
            var query = frame.Filter<FlipFriendsPlayer>();
            while (query.Next(out _, out var player))
                if (player.Carrier.IsValid) carriedMask |= 1UL << (int)player.Player;
            for (int index = 0; index < frame.PlayerCount; index++)
            {
                if ((carriedMask & (1UL << index)) != 0) continue;
                var players = frame.Filter<Transform2D, FlipFriendsPlayer>();
                while (players.NextUnsafe(out var entity, out _, out var state))
                    if ((int)state->Player == index) TryDamage(frame, entity);
            }
        }

        public static bool TryDamage(Frame frame, EntityRef entity)
        {
            var state = frame.Unsafe.GetPointer<FlipFriendsPlayer>(entity);
            if (state->IsFinished || state->Carrier.IsValid || state->InvincibleTicks > 0 || FlipFriendsRespawnSystem.IsBlocked(frame, *state)) return false;
            var traps = frame.RuntimeConfig.Traps;
            if (traps == null) return false;
            var position = frame.Get<Transform2D>(entity).Position;
            var center = position + FlipFriendsInteractionSystem.Offset(*state);
            var half = FlipFriendsInteractionSystem.HalfSize(*state);
            // 원본은 먼저 처리된 트랩 하나만 맞고 나머지는 무적으로 무시된다. 변환기가 ID 순으로 정렬한 첫 접촉 트랩을 쓴다.
            for (int i = 0; i < traps.Length; i++)
            {
                if (!Overlaps(traps[i].Vertices, center, half)) continue;
                Apply(frame, entity, Direction(traps[i], position));
                return true;
            }
            return false;
        }

        // 원본 knockbackDir은 정규화하지 않은 값을 그대로 곱한다. 0이면 트랩 Transform에서 플레이어 Transform으로 향하는 단위 벡터다.
        public static FPVector2 Direction(FlipFriendsTrapConfig trap, FPVector2 playerPosition)
        {
            if (trap.Direction != default) return trap.Direction;
            var away = playerPosition - trap.Origin;
            return away == default ? default : away.Normalized;
        }

        public static void Apply(Frame frame, EntityRef entity, FPVector2 direction)
        {
            var state = frame.Unsafe.GetPointer<FlipFriendsPlayer>(entity);
            // 원본 OnDamaged는 속도만 덮어쓰고 수평 SmoothDamp 누적값은 유지한다.
            state->Velocity = new FPVector2(direction.X * KnockbackScale.X, direction.Y * KnockbackScale.Y);
            state->IsClimbed = false;
            state->ClimbBlockTicks = System.Math.Max(state->ClimbBlockTicks, ClimbBlockTicks);
            state->InvincibleTicks = InvincibleTicks;
            state->UncontrollableTicks = System.Math.Max(state->UncontrollableTicks, UncontrollableTicks);
            if (direction.X != 0) state->Facing = direction.X < 0 ? -1 : 1;
            state->DamageCount++;
            state->DamageFrame = frame.Number;
            // 원본 DropAll은 넉백 방향으로 바뀐 바라보는 쪽에 내려놓는다. 놓을 자리가 모두 막히면 기존 놓기 규칙대로 운반을 유지한다.
            if (state->Held.IsValid) FlipFriendsInteractionSystem.Release(frame, entity, true);
        }

        // 축 정렬 몸체와 볼록 다각형의 분리축 검사. 경계에 닿기만 한 경우는 기존 겹침 판정처럼 접촉으로 보지 않는다.
        public static bool Overlaps(FPVector2[] vertices, FPVector2 center, FPVector2 half)
        {
            if (vertices == null || vertices.Length < 3) return false;
            if (!Separated(vertices, center, half, new FPVector2(1, 0)) && !Separated(vertices, center, half, new FPVector2(0, 1)))
            {
                for (int i = 0; i < vertices.Length; i++)
                {
                    var edge = vertices[(i + 1) % vertices.Length] - vertices[i];
                    if (Separated(vertices, center, half, new FPVector2(-edge.Y, edge.X))) return false;
                }
                return true;
            }
            return false;
        }

        private static bool Separated(FPVector2[] vertices, FPVector2 center, FPVector2 half, FPVector2 axis)
        {
            FP min = FP.MaxValue, max = FP.MinValue;
            for (int i = 0; i < vertices.Length; i++)
            {
                var value = FPVector2.Dot(vertices[i], axis);
                if (value < min) min = value;
                if (value > max) max = value;
            }
            var mid = FPVector2.Dot(center, axis);
            var radius = FPMath.Abs(axis.X) * half.X + FPMath.Abs(axis.Y) * half.Y;
            return max <= mid - radius || min >= mid + radius;
        }
    }
}
