namespace Quantum
{
    using Photon.Deterministic;

    public unsafe class FlipFriendsMovementSystem : SystemMainThreadFilter<FlipFriendsMovementSystem.Filter>, ISignalOnPlayerAdded
    {
        private static readonly FP HalfWidth = FP.FromString("0.4");
        private static readonly FP HalfHeight = FP.FromString("0.5");
        private static readonly FP Skin = FP.FromString("0.02");
        private static readonly FP MicroStep = FlipFriendsTerrain.MicroStep;
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
            var spawns = frame.RuntimeConfig.CustomMapSpawns;
            int slot = player;
            bool hasPosition = true;
            var position = new FPVector2(slot * 2 - 3, 2);
            if (spawns != null && spawns.Length > 0)
            {
                position = spawns[slot % spawns.Length];
                if (slot >= spawns.Length) hasPosition = TryFindExtraSpawn(frame, spawns, ref position);
            }
            var entity = frame.Create();
            frame.Add(entity, new Transform2D { Position = position });
            frame.Add(entity, new FlipFriendsPlayer { Player = player, HomePosition = position, RespawnPosition = hasPosition ? default : position, RespawnPending = !hasPosition });
        }

        private static bool TryFindExtraSpawn(Frame frame, FPVector2[] authored, ref FPVector2 position)
        {
            var half = new FPVector2(HalfWidth, HalfHeight);
            FlipFriendsMotionSystem.Initialize(frame);
            FlipFriendsInteractionSystem.InitializeBoxes(frame);
            const int searchDistance = 16;
            for (int distance = 1; distance <= searchDistance; distance++)
            {
                for (int direction = 1; direction >= -1; direction -= 2)
                {
                    var candidate = position + new FPVector2(distance * direction, 0);
                    if (FlipFriendsRespawnSystem.IsOutside(frame.RuntimeConfig, candidate)) continue;
                    bool isReserved = false;
                    foreach (var spawn in authored)
                        isReserved |= FlipFriendsMotionSystem.Overlap(candidate, half, spawn, half);
                    if (isReserved || !FlipFriendsInteractionSystem.CanOccupy(frame, candidate, half, default, default)) continue;
                    position = candidate;
                    return true;
                }
            }
            return false;
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
                    state.ConveyorVelocityX = 0;
                    state.ConveyorContact = 0;
                    state.IsClimbed = false;
                    state.StompTarget = default;
                    state.ShrinkTicks = state.JumpBlockTicks = state.ClimbBlockTicks = state.UncontrollableTicks = state.InvincibleTicks = 0;
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
            bool onSteep = SteepSupport(frame, transform.Position, state, out var steepNormal);
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
                    if (onSteep)
                    {
                        if (horizontal != -FPMath.Sign(steepNormal.X)) state.Velocity = steepNormal * JumpSpeed;
                    }
                    else state.Velocity.Y = JumpSpeed;
                    if (!onSteep && input.Run.IsDown)
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
            FlipFriendsConveyorSystem.ApplyExternal(frame, ref state);
            var delta = state.Velocity * frame.DeltaTime;
            var position = transform.Position;
            var half = FlipFriendsInteractionSystem.HalfSize(state);
            var offset = FlipFriendsInteractionSystem.Offset(state);
            if (onSteep && delta.Y < 0 && !state.IsClimbed)
                delta.X = FPMath.Sign(steepNormal.X) * FPMath.Abs(delta.Y) * steepNormal.Y / FPMath.Abs(steepNormal.X);
            PrepareSlope(frame, ref position, ref delta, offset, half, state, default, state.IsClimbed);
            delta.X = LimitHorizontalWithMicroStep(frame, ref position, offset, delta.X, half);
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
            if (vertical != delta.Y)
            {
                if (state.IsGrounded && onSteep) state.Velocity.Y += steepNormal.Y * Gravity * frame.DeltaTime;
                else state.Velocity.Y = 0;
            }
            position.Y += vertical;
            state.IsTouchingLeft = LimitAxis(frame, position + offset, -Skin, true, half) != -Skin;
            state.IsTouchingRight = LimitAxis(frame, position + offset, Skin, true, half) != Skin;
            transform.Position = position;
            state.ConveyorContact = FlipFriendsConveyorSystem.Contact(frame, position + offset, half, state.IsGrounded, state.IsTouchingCeiling);
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

        public static void PrepareSlope(Frame frame, ref FPVector2 position, ref FPVector2 delta, FPVector2 offset,
            FPVector2 half, FlipFriendsPlayer player, EntityRef boxEntity, bool isClimbed = false)
        {
            if (isClimbed || delta.X == 0) return;
            var contours = frame.RuntimeConfig.TerrainContours;
            var center = position + offset;
            var foot = center.Y - half.Y;
            var reach = FPMath.Abs(delta.X);
            if (!FlipFriendsTerrain.Support(contours, center, half, foot - FPMath.Max(0, -delta.Y) - MicroStep,
                foot + MicroStep, out var current, out var oldEdge)) return;
            var next = center + new FPVector2(delta.X, 0);
            if (!FlipFriendsTerrain.Support(contours, next, half, current - reach - MicroStep, current + reach + MicroStep,
                out var target, out var edge) || !FlipFriendsTerrain.IsSlope(edge) && !FlipFriendsTerrain.IsSlope(oldEdge)) return;
            // 위로 빠져나가는 점프는 붙잡지 않는다. 원본도 점프 상승량이 경사 상승량보다 크면 ClimbSlope를 적용하지 않는다.
            var tangent = target > current ? edge : oldEdge;
            if (!FlipFriendsTerrain.IsSlope(tangent)) tangent = edge;
            if (!boxEntity.IsValid && !FlipFriendsTerrain.IsPlayerWalkable(tangent)) return;
            var cosine = FPMath.Abs(tangent.X) / tangent.Magnitude;
            var distance = delta.X * cosine;
            if (target > current && !FlipFriendsTerrain.IsSlope(oldEdge))
            {
                var start = FPMath.Clamp(reach - (target - current) / FPMath.Abs(tangent.Y / tangent.X), 0, reach);
                distance = FPMath.Sign(delta.X) * (start + (reach - start) * cosine);
            }
            if (delta.Y > 0 && target <= current) return;
            if (delta.Y > FPMath.Abs(distance) * FPMath.Abs(tangent.Y / tangent.X)) return;

            FP Limit(FPVector2 at, FP amount, bool horizontal)
            {
                var value = LimitAxis(frame, at + offset, amount, horizontal, half);
                value = FlipFriendsInteractionSystem.LimitBoxes(frame, at + offset, half, value, horizontal,
                    boxEntity.IsValid ? boxEntity : player.Held);
                value = FlipFriendsInteractionSystem.LimitPlayerBodies(frame, player, at + offset, half, value, horizontal, isBox: boxEntity.IsValid);
                if (player.Held.IsValid) value = LimitHeldTerrain(frame, player.Held, at, value, horizontal);
                return value;
            }

            // 상승과 수평을 모두 검사한 뒤에만 위치를 반영한다. 막힌 머리/운반물 때문에 경사 속으로 들어가지 않는다.
            var originalPosition = position;
            FP low = 0, high = FPMath.Abs(distance), acceptedRise = 0;
            FP sign = distance > 0 ? FP._1 : -FP._1;
            bool CanMove(FP length, out FP rise)
            {
                rise = 0;
                var destination = center + new FPVector2(length * sign, 0);
                if (!FlipFriendsTerrain.Support(contours, destination, half, current - reach - MicroStep,
                    current + reach + MicroStep, out var floor, out _)) return false;
                // 유리수 변 교차의 바깥쪽 반올림과 수평 검사의 안쪽 반올림 사이 1raw 차이를 피한다.
                // 아래 축 검사에서 즉시 지지면으로 돌아오므로 이 여유가 누적되지는 않는다.
                rise = floor > foot ? floor - foot + FP.FromRaw(2) : FP._0;
                if (Limit(originalPosition, rise, false) != rise) return false;
                var raised = originalPosition + new FPVector2(0, rise);
                return Limit(raised, length * sign, true) == length * sign;
            }
            if (CanMove(high, out var fullRise)) { low = high; acceptedRise = fullRise; }
            else for (int i = 0; i < 16 && high.RawValue - low.RawValue > 1; i++)
            {
                var mid = (low + high) / 2;
                if (CanMove(mid, out var rise)) { low = mid; acceptedRise = rise; } else high = mid;
            }
            delta.X = low * sign;
            position.Y += acceptedRise;
            if (acceptedRise > 0 && delta.Y > 0) delta.Y = -FP.FromRaw(2);
            if (delta.Y <= 0 && FlipFriendsTerrain.Support(contours, center + new FPVector2(delta.X, 0), half,
                current - reach - MicroStep, current + reach + MicroStep, out var destinationFloor, out _))
                delta.Y -= FPMath.Max(0, foot - destinationFloor);
        }

        private static bool SteepSupport(Frame frame, FPVector2 position, FlipFriendsPlayer state, out FPVector2 normal)
        {
            normal = default;
            var half = FlipFriendsInteractionSystem.HalfSize(state);
            var center = position + FlipFriendsInteractionSystem.Offset(state);
            var foot = center.Y - half.Y;
            if (!FlipFriendsTerrain.Support(frame.RuntimeConfig.TerrainContours, center, half, foot - MicroStep, foot + MicroStep,
                out _, out var edge) || FlipFriendsTerrain.IsPlayerWalkable(edge)) return false;
            var tangent = edge / edge.Magnitude;
            if (tangent.X < 0) tangent = -tangent;
            normal = new FPVector2(-tangent.Y, tangent.X);
            return true;
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

        public static FP LimitAxis(Frame frame, FPVector2 center, FP distance, bool isHorizontal, FPVector2 half, int ignoredMotion = -1)
        {
            if (distance == 0) return 0;
            FP sign = distance > 0 ? FP._1 : -FP._1;
            var direction = isHorizontal ? new FPVector2(sign, 0) : new FPVector2(0, sign);
            var extent = isHorizontal ? half.X : half.Y;
            var side = (isHorizontal ? half.Y : half.X) - Skin;
            var allowed = FPMath.Abs(distance);
            // SDK 정적 판정은 세 줄 레이로 검사하고 윤곽은 아래의 연속 AABB 검사로 보완한다.
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
            var terrain = FlipFriendsTerrain.Limit(frame.RuntimeConfig.TerrainContours, center, half, allowed * sign, isHorizontal);
            return FlipFriendsMotionSystem.Limit(frame, center, half, terrain, isHorizontal, ignoredMotion);
        }

        // Composite가 만든 미세 기울기(작성기 허용 8raw 이하)가 수평 이동의 벽이 되지 않게, 막혔을 때만 그 높이까지 올라서 다시 잰다.
        // 실제 벽은 8raw 위로 올라도 그대로 막히므로 결과가 같고 위치도 바뀌지 않는다.
        public static FP LimitHorizontalWithMicroStep(Frame frame, ref FPVector2 position, FPVector2 offset, FP distance, FPVector2 half)
        {
            var limited = LimitAxis(frame, position + offset, distance, true, half);
            // 직렬화 뒤 null 배열이 빈 배열로 돌아오므로 둘 다 윤곽 지형 없음으로 본다. 기존 박스 맵 동작은 바꾸지 않는다.
            if (limited == distance || frame.RuntimeConfig.TerrainContours == null || frame.RuntimeConfig.TerrainContours.Length == 0) return limited;
            var lift = LimitAxis(frame, position + offset, MicroStep, false, half);
            if (lift <= 0) return limited;
            var lifted = position + new FPVector2(0, lift);
            var stepped = LimitAxis(frame, lifted + offset, distance, true, half);
            if (FPMath.Abs(stepped) <= FPMath.Abs(limited)) return limited;
            position = lifted;
            return stepped;
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
