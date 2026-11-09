namespace Quantum
{
    using Photon.Deterministic;

    public unsafe class FlipFriendsRespawnSystem : SystemMainThread
    {
        private readonly bool isAfterMovement;
        public FlipFriendsRespawnSystem(bool isAfterMovement) { this.isAfterMovement = isAfterMovement; }

        public static bool IsBlocked(Frame frame, FlipFriendsPlayer state) =>
            state.RespawnPending || state.RespawnFrame > 0 && state.RespawnFrame == frame.Number;

        public static bool IsOutside(RuntimeConfig config, FPVector2 position) =>
            config.EnableRespawn && (position.X < config.FallMinX || position.X > config.FallMaxX || position.Y < config.FallMinY);

        public override void Update(Frame frame)
        {
            if (!frame.RuntimeConfig.EnableRespawn || frame.Global->FlipFriendsCompletedFrame > 0) return;
            uint registered = frame.FlipFriendsRegisteredPlayerMask();
            for (int index = 0; registered != 0; index++, registered >>= 1)
            {
                if ((registered & 1) == 0) continue;
                var players = frame.Filter<Transform2D, FlipFriendsPlayer>();
                while (players.NextUnsafe(out var entity, out var transform, out var state))
                {
                    if ((int)state->Player != index || state->IsFinished) continue;
                    var pointer = frame.GetPlayerInput(state->Player);
                    var input = pointer == null ? default : *pointer;
                    bool isPressed = !isAfterMovement && input.Reset.IsDown && !state->WasResetHeld;
                    if (!isAfterMovement) state->WasResetHeld = input.Reset.IsDown;
                    // Mirror처럼 들린 플레이어의 직접 복귀 요청은 소비하되 운반자를 이동시키지 않는다.
                    if (!state->Carrier.IsValid && !state->RespawnPending && state->RespawnFrame != frame.Number &&
                        (isPressed || IsOutside(frame.RuntimeConfig, transform->Position))) QueuePlayer(frame, entity);
                    if (state->SectionPending) TryRestoreSection(frame, entity);
                    else if (state->RespawnPending && !state->Carrier.IsValid) TryRestorePlayer(frame, entity);
                    if (IsBlocked(frame, *state))
                    {
                        state->WasJumpHeld = input.Jump.IsDown;
                        state->WasInteractHeld = input.Interact.IsDown;
                    }
                }
            }
            var boxes = frame.Filter<Transform2D, FlipFriendsBox>();
            while (boxes.NextUnsafe(out var entity, out var transform, out var box))
            {
                if (!box->Carrier.IsValid && !box->RespawnPending && box->RespawnFrame != frame.Number &&
                    IsOutside(frame.RuntimeConfig, transform->Position)) QueueBox(frame, entity);
                if (box->RespawnPending && !box->Carrier.IsValid) TryRestoreBox(frame, entity);
            }
        }

        public static void QueuePlayer(Frame frame, EntityRef entity)
        {
            var state = frame.Unsafe.GetPointer<FlipFriendsPlayer>(entity);
            if (state->IsFinished || state->RespawnPending || state->RespawnFrame == frame.Number) return;
            if (state->Carrier.IsValid) Detach(frame, state->Carrier);
            var held = state->Held;
            if (held.IsValid && !FlipFriendsInteractionSystem.Release(frame, entity, true))
            {
                Detach(frame, entity);
                if (frame.Has<FlipFriendsPlayer>(held)) QueuePlayer(frame, held);
                else QueueBox(frame, held);
            }
            else if (held.IsValid && frame.Has<Transform2D>(held) && IsOutside(frame.RuntimeConfig, frame.Get<Transform2D>(held).Position))
            {
                if (frame.Has<FlipFriendsPlayer>(held)) QueuePlayer(frame, held);
                else QueueBox(frame, held);
            }
            // 조작 누름 이력과 관측 카운터만 보존하고 다음 틱에 영향을 주는 운동 상태를 함께 비운다.
            *state = new FlipFriendsPlayer
            {
                Player = state->Player, HomePosition = state->HomePosition, Facing = state->Facing,
                HasCheckpoint = state->HasCheckpoint, CheckpointId = state->CheckpointId, CheckpointPosition = state->CheckpointPosition,
                RespawnPosition = Destination(*state), SectionCount = state->SectionCount,
                WasResetHeld = state->WasResetHeld, WasJumpHeld = state->WasJumpHeld, WasInteractHeld = state->WasInteractHeld,
                RespawnPending = true, RespawnFrame = frame.Number, RespawnCount = state->RespawnCount + 1,
                StompCount = state->StompCount, PickupCount = state->PickupCount, ThrowCount = state->ThrowCount,
                PutDownCount = state->PutDownCount, EscapeCount = state->EscapeCount, DamageCount = state->DamageCount,
                PreviousPosition = Destination(*state)
            };
            frame.Unsafe.GetPointer<Transform2D>(entity)->Position = state->RespawnPosition;
            var players = frame.Filter<FlipFriendsPlayer>();
            while (players.NextUnsafe(out _, out var other))
                if (other->StompTarget == entity) other->StompTarget = default;
        }

        public static void QueueBox(Frame frame, EntityRef entity)
        {
            var box = frame.Unsafe.GetPointer<FlipFriendsBox>(entity);
            if (box->RespawnPending || box->RespawnFrame == frame.Number) return;
            if (box->Carrier.IsValid) Detach(frame, box->Carrier);
            *box = new FlipFriendsBox { ContentIndex = box->ContentIndex, HalfSize = box->HalfSize, HomePosition = box->HomePosition, RespawnPosition = box->HomePosition,
                RespawnPending = true, RespawnFrame = frame.Number, RespawnCount = box->RespawnCount + 1 };
            frame.Unsafe.GetPointer<Transform2D>(entity)->Position = box->HomePosition;
        }

        public static void Detach(Frame frame, EntityRef carrier)
        {
            if (!frame.Unsafe.TryGetPointer<FlipFriendsPlayer>(carrier, out var state)) return;
            var held = state->Held;
            state->Held = default;
            state->ReleaseFrame = 0;
            if (frame.Unsafe.TryGetPointer<FlipFriendsPlayer>(held, out var player)) player->Carrier = default;
            if (frame.Unsafe.TryGetPointer<FlipFriendsBox>(held, out var box)) box->Carrier = default;
        }

        private static void TryRestorePlayer(Frame frame, EntityRef entity)
        {
            var state = frame.Unsafe.GetPointer<FlipFriendsPlayer>(entity);
            if (IsOutside(frame.RuntimeConfig, state->RespawnPosition) ||
                !FlipFriendsInteractionSystem.CanOccupy(frame, state->RespawnPosition, FlipFriendsInteractionSystem.HalfSize(*state), entity, entity)) return;
            state->RespawnPending = false;
            state->RespawnFrame = frame.Number;
        }

        private static void TryRestoreBox(Frame frame, EntityRef entity)
        {
            var box = frame.Unsafe.GetPointer<FlipFriendsBox>(entity);
            if (IsOutside(frame.RuntimeConfig, box->RespawnPosition) ||
                !FlipFriendsInteractionSystem.CanOccupy(frame, box->RespawnPosition, box->HalfSize, entity, entity)) return;
            box->RespawnPending = false;
            box->RespawnFrame = frame.Number;
        }

        public static FPVector2 Destination(FlipFriendsPlayer state) => state.HasCheckpoint ? state.CheckpointPosition : state.HomePosition;

        public static bool QueueSection(Frame frame, EntityRef entity, FPVector2 destination, bool onlyBoxReset)
        {
            var state = frame.Unsafe.GetPointer<FlipFriendsPlayer>(entity);
            if (state->IsFinished || state->Carrier.IsValid || IsBlocked(frame, *state) ||
                onlyBoxReset && (!state->Held.IsValid || !frame.Has<FlipFriendsBox>(state->Held))) return false;
            var held = state->Held;
            ClearPlayer(frame, entity, destination, true);
            state->SectionPending = true;
            state->SectionCount++;
            if (held.IsValid)
            {
                var above = destination + FlipFriendsInteractionSystem.HeldOffset;
                if (frame.Has<FlipFriendsPlayer>(held)) ClearPlayer(frame, held, above, true);
                else ClearBox(frame, held, above, true);
            }
            TryRestoreSection(frame, entity);
            return true;
        }

        public static void ClearPlayer(Frame frame, EntityRef entity, FPVector2 destination, bool keepCarry)
        {
            var state = frame.Unsafe.GetPointer<FlipFriendsPlayer>(entity);
            var old = *state;
            var input = frame.FlipFriendsInput(old.Player);
            *state = new FlipFriendsPlayer { Player = old.Player, HomePosition = old.HomePosition,
                HasCheckpoint = old.HasCheckpoint, CheckpointId = old.CheckpointId, CheckpointPosition = old.CheckpointPosition,
                Facing = old.Facing, RespawnPosition = destination, PreviousPosition = destination,
                RespawnPending = true, RespawnFrame = frame.Number, RespawnCount = old.RespawnCount, SectionCount = old.SectionCount,
                Carrier = keepCarry ? old.Carrier : default, Held = keepCarry ? old.Held : default,
                WasResetHeld = input.Reset.IsDown, WasJumpHeld = input.Jump.IsDown, WasInteractHeld = input.Interact.IsDown,
                StompCount = old.StompCount, PickupCount = old.PickupCount, ThrowCount = old.ThrowCount,
                PutDownCount = old.PutDownCount, EscapeCount = old.EscapeCount, DamageCount = old.DamageCount,
                LaunchCount = old.LaunchCount };
            frame.Unsafe.GetPointer<Transform2D>(entity)->Position = destination;
            var players = frame.Filter<FlipFriendsPlayer>();
            while (players.NextUnsafe(out _, out var other)) if (other->StompTarget == entity) other->StompTarget = default;
        }

        public static void ClearBox(Frame frame, EntityRef entity, FPVector2 destination, bool keepCarry)
        {
            var box = frame.Unsafe.GetPointer<FlipFriendsBox>(entity);
            *box = new FlipFriendsBox { ContentIndex = box->ContentIndex, HalfSize = box->HalfSize, HomePosition = box->HomePosition,
                Carrier = keepCarry ? box->Carrier : default, RespawnPosition = destination, RespawnPending = true,
                RespawnFrame = frame.Number, RespawnCount = box->RespawnCount };
            frame.Unsafe.GetPointer<Transform2D>(entity)->Position = destination;
        }

        public static void TryRestoreSection(Frame frame, EntityRef entity)
        {
            var state = frame.Unsafe.GetPointer<FlipFriendsPlayer>(entity);
            if (!state->SectionPending) return;
            var held = state->Held;
            if (IsOutside(frame.RuntimeConfig, state->RespawnPosition) ||
                !FlipFriendsDeviceSystem.CanPlace(frame, state->RespawnPosition, FlipFriendsInteractionSystem.HalfSize(*state), entity, held)) return;
            if (held.IsValid)
            {
                bool isPlayer = frame.Unsafe.TryGetPointer<FlipFriendsPlayer>(held, out var player);
                var half = isPlayer ? FlipFriendsInteractionSystem.HalfSize(*player) : frame.Get<FlipFriendsBox>(held).HalfSize;
                var above = state->RespawnPosition + FlipFriendsInteractionSystem.HeldOffset;
                if (IsOutside(frame.RuntimeConfig, above) || !FlipFriendsDeviceSystem.CanPlace(frame, above, half, entity, held)) return;
                if (isPlayer)
                {
                    player->RespawnPending = false; player->RespawnFrame = frame.Number;
                    player->EscapeFrame = frame.Number + 90; player->EscapeRequestFrame = frame.Number + 15;
                }
                else
                {
                    var box = frame.Unsafe.GetPointer<FlipFriendsBox>(held);
                    box->RespawnPending = false; box->RespawnFrame = frame.Number;
                }
                state->ReleaseFrame = frame.Number + 9;
            }
            state->RespawnPending = false; state->SectionPending = false; state->RespawnFrame = frame.Number;
        }
    }
}
