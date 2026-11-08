namespace Quantum
{
    using Photon.Deterministic;

    public unsafe class FlipFriendsInteractionSystem : SystemMainThread
    {
        public static readonly FP Skin = FP.FromString("0.02");
        public static readonly FPVector2 HeldOffset = new FPVector2(0, FP.FromString("0.95"));
        public static FPVector2 HalfSize(FlipFriendsPlayer state) => new FPVector2(FP.FromString("0.4"), state.ShrinkTicks > 0 ? FP.FromString("0.25") : FP.FromString("0.5"));
        public static FPVector2 Offset(FlipFriendsPlayer state) => new FPVector2(0, state.ShrinkTicks > 0 ? FP.FromString("-0.25") : FP._0);

        public override void Update(Frame frame)
        {
            InitializeBoxes(frame);
            var players = frame.Filter<Transform2D, FlipFriendsPlayer>();
            while (players.NextUnsafe(out var entity, out var transform, out var state))
            {
                state->PreviousPosition = transform->Position;
                if (state->IsFinished || FlipFriendsRespawnSystem.IsBlocked(frame, *state)) continue;
                if (state->JumpBlockTicks > 0) state->JumpBlockTicks--;
                if (state->ShrinkTicks > 0) state->ShrinkTicks--;
                if (state->UncontrollableTicks > 0 && !state->Carrier.IsValid) state->UncontrollableTicks--;
                // 원본 TickTimers는 이동이 꺼진 운반 중에는 돌지 않으므로 무적도 들린 동안 멈춘다.
                if (state->InvincibleTicks > 0 && !state->Carrier.IsValid) state->InvincibleTicks--;
            }
            // 동일 틱의 접촉은 전역 PlayerRef 순서로 처리해 생성/접속 순서와 분리한다.
            for (int player = 0; player < frame.PlayerCount; player++)
            {
                players = frame.Filter<Transform2D, FlipFriendsPlayer>();
                while (players.NextUnsafe(out var entity, out var transform, out var state))
                {
                    if ((int)state->Player != player) continue;
                    var input = frame.GetPlayerInput(state->Player);
                    var controls = input == null ? default : *input;
                    if (state->IsFinished || FlipFriendsRespawnSystem.IsBlocked(frame, *state)) continue;
                    bool isPressed = controls.Interact.IsDown && !state->WasInteractHeld;
                    state->WasInteractHeld = controls.Interact.IsDown;
                    if (state->Carrier.IsValid)
                    {
                        state->StompTarget = default;
                        state->WasJumpHeld = controls.Jump.IsDown;
                        if (frame.Number >= state->EscapeFrame && frame.Number >= state->EscapeRequestFrame &&
                            (controls.Horizontal != 0 || controls.Vertical != 0 || controls.Jump.IsDown))
                        {
                            RequestEscape(frame, entity);
                        }
                        continue;
                    }
                    if (controls.Horizontal != 0 && state->UncontrollableTicks == 0) state->Facing = controls.Horizontal > 0 ? 1 : -1;
                    if (state->Facing == 0) state->Facing = 1;
                    if (isPressed) Interact(frame, entity, controls.Vertical < 0);
                    TryStomp(frame, entity, controls);
                }
            }
        }

        public static bool TryStomp(Frame frame, EntityRef entity, Input input)
        {
            var state = frame.Unsafe.GetPointer<FlipFriendsPlayer>(entity);
            var target = state->StompTarget;
            state->StompTarget = default;
            if (state->IsFinished || FlipFriendsRespawnSystem.IsBlocked(frame, *state) || state->Carrier.IsValid || state->JumpBlockTicks > 0 || !frame.Unsafe.TryGetPointer<FlipFriendsPlayer>(target, out var below) || below->IsFinished || FlipFriendsRespawnSystem.IsBlocked(frame, *below) || below->Carrier.IsValid) return false;
            state->Velocity.Y = FP.FromString("13.2") * (input.Jump.IsDown ? FP.FromString("1.2") : FP._1);
            state->StompPushX = (frame.Get<Transform2D>(entity).Position.X - below->PreviousPosition.X) * FP.FromString("0.5");
            state->Velocity.X += state->StompPushX;
            state->StompFrame = frame.Number;
            state->IsClimbed = false;
            state->ClimbBlockTicks = 18;
            state->JumpBlockTicks = 18;
            state->StompCount++;
            below->ShrinkTicks = 30;
            below->JumpBlockTicks = System.Math.Max(below->JumpBlockTicks, 9);
            return true;
        }

        public static bool RequestEscape(Frame frame, EntityRef entity)
        {
            var state = frame.Unsafe.GetPointer<FlipFriendsPlayer>(entity);
            if (FlipFriendsRespawnSystem.IsBlocked(frame, *state) || !state->Carrier.IsValid || frame.Number < state->EscapeFrame || frame.Number < state->EscapeRequestFrame) return false;
            state->EscapeRequestFrame = frame.Number + 15;
            if (!Release(frame, state->Carrier, false)) return false;
            state->EscapeCount++;
            return true;
        }

        public static bool Interact(Frame frame, EntityRef entity, bool isPutDown)
        {
            var state = frame.Unsafe.GetPointer<FlipFriendsPlayer>(entity);
            if (state->IsFinished || FlipFriendsRespawnSystem.IsBlocked(frame, *state) || state->Carrier.IsValid) return false;
            if (!state->Held.IsValid) return TryPickup(frame, entity);
            return frame.Number >= state->ReleaseFrame && Release(frame, entity, isPutDown);
        }

        private static void InitializeBoxes(Frame frame)
        {
            if (frame.Global->FlipFriendsBoxesInitialized) return;
            frame.Global->FlipFriendsBoxesInitialized = true;
            var centers = frame.RuntimeConfig.BoxCenters;
            var sizes = frame.RuntimeConfig.BoxSizes;
            if (centers == null || sizes == null) return;
            for (int i = 0; i < centers.Length; i++)
            {
                var entity = frame.Create();
                frame.Add(entity, new Transform2D { Position = centers[i] });
                frame.Add(entity, new FlipFriendsBox { ContentIndex = i, HalfSize = sizes[i] / 2, HomePosition = centers[i] });
            }
        }

        public static bool TryPickup(Frame frame, EntityRef carrier)
        {
            var state = frame.Unsafe.GetPointer<FlipFriendsPlayer>(carrier);
            if (state->IsFinished || FlipFriendsRespawnSystem.IsBlocked(frame, *state) || state->Held.IsValid || state->Carrier.IsValid) return false;
            var position = frame.Get<Transform2D>(carrier).Position;
            var half = HalfSize(*state);
            var center = position + Offset(*state);
            // 기존 머리 위 상자 감지 영역은 들기 종류와 무관하게 비어 있어야 한다.
            if (!CanOccupy(frame, position + HeldOffset, new FPVector2(FP.FromString("0.45"), FP.FromString("0.45")), carrier, default)) return false;
            EntityRef selected = default;
            int bestRay = int.MaxValue;
            FP bestDistance = FP.MaxValue;
            int bestPlayer = int.MaxValue;
            var players = frame.Filter<Transform2D, FlipFriendsPlayer>();
            while (players.Next(out var entity, out var transform, out var target))
            {
                if (entity == carrier || target.IsFinished || FlipFriendsRespawnSystem.IsBlocked(frame, target) || target.Carrier.IsValid || target.Held.IsValid) continue;
                if (!Search(center, half, state->Facing, transform.Position + Offset(target), HalfSize(target), out int ray, out var distance)) continue;
                if (ray < bestRay || ray == bestRay && (distance < bestDistance || distance == bestDistance && (int)target.Player < bestPlayer))
                { selected = entity; bestRay = ray; bestDistance = distance; bestPlayer = (int)target.Player; }
            }
            if (!selected.IsValid)
            {
                var boxes = frame.Filter<Transform2D, FlipFriendsBox>();
                while (boxes.Next(out var entity, out var transform, out var box))
                {
                    if (box.RespawnPending || box.RespawnFrame == frame.Number || box.Carrier.IsValid || !Search(center, half, state->Facing, transform.Position, box.HalfSize, out int ray, out var distance)) continue;
                    if (ray < bestRay || ray == bestRay && distance < bestDistance)
                    { selected = entity; bestRay = ray; bestDistance = distance; }
                }
            }
            if (!selected.IsValid) return false;
            state->Held = selected;
            state->ReleaseFrame = frame.Number + 9;
            state->PickupCount++;
            if (frame.Unsafe.TryGetPointer<FlipFriendsPlayer>(selected, out var carried))
            {
                carried->Carrier = carrier;
                carried->EscapeFrame = frame.Number + 90;
                carried->EscapeRequestFrame = frame.Number;
                carried->Velocity = default;
                carried->VelocityXSmoothing = 0;
                carried->IsClimbed = false;
                carried->StompTarget = default;
            }
            else
            {
                var box = frame.Unsafe.GetPointer<FlipFriendsBox>(selected);
                box->Carrier = carrier;
                box->Velocity = default;
                box->VelocityXSmoothing = 0;
            }
            return true;
        }

        private static bool Search(FPVector2 center, FPVector2 half, int facing, FPVector2 target, FPVector2 targetHalf, out int ray, out FP distance)
        {
            FP spacing = half.X * 2 / 8;
            FP edge = center.X + facing * half.X;
            distance = FPMath.Max(0, facing > 0 ? target.X - targetHalf.X - edge : edge - target.X - targetHalf.X);
            for (ray = 0; ray < 10; ray++)
            {
                FP y = center.Y - half.Y + spacing * (ray - 1);
                if (y < target.Y - targetHalf.Y || y > target.Y + targetHalf.Y) continue;
                if (distance <= FP.FromString("0.2") && (target.X - edge) * facing >= -targetHalf.X) return true;
            }
            return false;
        }

        public static bool Release(Frame frame, EntityRef carrier, bool isPutDown)
        {
            if (!frame.Unsafe.TryGetPointer<FlipFriendsPlayer>(carrier, out var state) || !state->Held.IsValid) return true;
            if (state->SectionPending) return false;
            var target = state->Held;
            if (!frame.Has<Transform2D>(target)) { state->Held = default; return true; }
            bool isPlayer = frame.Unsafe.TryGetPointer<FlipFriendsPlayer>(target, out var carried);
            var box = isPlayer ? null : frame.Unsafe.GetPointer<FlipFriendsBox>(target);
            var half = isPlayer ? HalfSize(*carried) : box->HalfSize;
            var offset = isPlayer ? Offset(*carried) : default;
            var position = frame.Get<Transform2D>(carrier).Position;
            var center = position + Offset(*state);
            var front = new FPVector2(center.X + state->Facing * (HalfSize(*state).X + half.X + Skin), center.Y - HalfSize(*state).Y + half.Y + Skin) - offset;
            var above = position + HeldOffset;
            FPVector2 release;
            bool Fits(FPVector2 candidate) => CanOccupy(frame, candidate + offset, half, carrier, target);
            if (!isPutDown && Fits(above)) release = above;
            else if (!isPutDown && isPlayer && Fits(position)) release = position;
            else if (Fits(front)) release = front;
            else if (Fits(above)) release = above;
            else if (Fits(position)) release = position;
            else return false;
            frame.Unsafe.GetPointer<Transform2D>(target)->Position = release;
            state->Held = default;
            if (isPutDown) state->PutDownCount++; else state->ThrowCount++;
            if (isPlayer)
            {
                carried->Carrier = default;
                carried->PreviousPosition = release;
                carried->Velocity = isPutDown ? default : new FPVector2(state->Facing * 7, 12);
                carried->VelocityXSmoothing = 0;
                carried->UncontrollableTicks = isPutDown ? 0 : 24;
                carried->IsGrounded = false;
                carried->IsClimbed = false;
                carried->StompTarget = default;
            }
            else
            {
                box->Carrier = default;
                box->Velocity = isPutDown ? default : new FPVector2(state->Facing * 6, 6);
                box->VelocityXSmoothing = 0;
                box->IsGrounded = false;
            }
            return true;
        }

        public static bool CanOccupy(Frame frame, FPVector2 center, FPVector2 half, EntityRef carrier, EntityRef target)
        {
            if (FlipFriendsTerrain.Overlaps(frame.RuntimeConfig.TerrainContours, center, half)) return false;
            var map = frame.Map.StaticColliders2D;
            for (int i = 0; i < map.Length; i++)
                if (Overlaps(center, half - new FPVector2(Skin * 2, Skin * 2), map[i].Position, map[i].BoxExtents)) return false;
            var boxes = frame.Filter<Transform2D, FlipFriendsBox>();
            while (boxes.Next(out var entity, out var transform, out var box))
                if (entity != target && entity != carrier && !box.RespawnPending && Overlaps(center, half, transform.Position, box.HalfSize)) return false;
            var players = frame.Filter<Transform2D, FlipFriendsPlayer>();
            while (players.Next(out var entity, out var transform, out var player))
                if (entity != carrier && entity != target && !player.IsFinished && !player.RespawnPending &&
                    Overlaps(center, half - new FPVector2(Skin * 2, Skin * 2), transform.Position + Offset(player), HalfSize(player))) return false;
            return true;
        }

        private static bool Overlaps(FPVector2 a, FPVector2 ah, FPVector2 b, FPVector2 bh) =>
            FPMath.Abs(a.X - b.X) < ah.X + bh.X && FPMath.Abs(a.Y - b.Y) < ah.Y + bh.Y;

        public static FP LimitBoxes(Frame frame, FPVector2 center, FPVector2 half, FP delta, bool horizontal, EntityRef ignored)
        {
            if (delta == 0) return 0;
            FP sign = delta > 0 ? FP._1 : -FP._1;
            FP allowed = FPMath.Abs(delta);
            var boxes = frame.Filter<Transform2D, FlipFriendsBox>();
            while (boxes.Next(out var entity, out var transform, out var box))
            {
                if (entity == ignored || box.RespawnPending || box.Carrier.IsValid) continue;
                var d = transform.Position - center;
                if (FPMath.Abs(horizontal ? d.Y : d.X) >= (horizontal ? half.Y + box.HalfSize.Y : half.X + box.HalfSize.X) - Skin) continue;
                var gap = (horizontal ? d.X : d.Y) * sign - (horizontal ? half.X + box.HalfSize.X : half.Y + box.HalfSize.Y);
                if (gap >= -Skin) allowed = FPMath.Min(allowed, FPMath.Max(0, gap));
            }
            return allowed * sign;
        }

        public static FP LimitPlayers(Frame frame, ref FlipFriendsPlayer state, FPVector2 position, FP delta)
        {
            if (delta >= 0) return delta;
            var half = HalfSize(state);
            var center = position + Offset(state);
            FP allowed = -delta;
            var players = frame.Filter<FlipFriendsPlayer>();
            int selected = int.MaxValue;
            EntityRef target = default;
            while (players.Next(out var entity, out var other))
            {
                if (other.Player == state.Player || other.IsFinished || FlipFriendsRespawnSystem.IsBlocked(frame, other) || other.Carrier.IsValid || state.Carrier.IsValid || state.Held == entity) continue;
                var otherHalf = HalfSize(other);
                var otherCenter = other.PreviousPosition + Offset(other);
                if (FPMath.Abs(center.X - otherCenter.X) >= half.X + otherHalf.X - Skin) continue;
                var gap = center.Y - half.Y - otherCenter.Y - otherHalf.Y;
                if (gap < -Skin || gap > allowed + Skin || position.Y <= other.PreviousPosition.Y + half.Y) continue;
                var distance = FPMath.Max(0, gap);
                if (distance < allowed || distance == allowed && (int)other.Player < selected)
                {
                    allowed = distance;
                    selected = (int)other.Player;
                    target = entity;
                }
            }
            if (target.IsValid) state.StompTarget = target;
            return -allowed;
        }

        public static FP LimitPlayerBodies(Frame frame, FlipFriendsPlayer state, FPVector2 center, FPVector2 half, FP delta, bool horizontal, EntityRef ignored = default, bool isBox = false)
        {
            if (delta == 0) return 0;
            FP sign = delta > 0 ? FP._1 : -FP._1;
            FP allowed = FPMath.Abs(delta);
            var players = frame.Filter<FlipFriendsPlayer>();
            while (players.Next(out var entity, out var other))
            {
                if (entity == ignored || !isBox && state.Player == other.Player || other.IsFinished || FlipFriendsRespawnSystem.IsBlocked(frame, other) || other.Carrier.IsValid || state.Held == entity) continue;
                var otherHalf = HalfSize(other);
                var d = other.PreviousPosition + Offset(other) - center;
                if (FPMath.Abs(horizontal ? d.Y : d.X) >= (horizontal ? half.Y + otherHalf.Y : half.X + otherHalf.X) - Skin) continue;
                FP gap = (horizontal ? d.X : d.Y) * sign - (horizontal ? half.X + otherHalf.X : half.Y + otherHalf.Y);
                if (gap >= -Skin) allowed = FPMath.Min(allowed, FPMath.Max(0, gap));
            }
            return allowed * sign;
        }
    }
}
