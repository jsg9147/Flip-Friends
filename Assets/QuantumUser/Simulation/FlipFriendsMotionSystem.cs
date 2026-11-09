namespace Quantum
{
    using Photon.Deterministic;

    public unsafe class FlipFriendsMotionSystem : SystemMainThread
    {
        private static readonly FP Tolerance = FP.FromRaw(16);

        public override void Update(Frame frame)
        {
            Initialize(frame);
            if (frame.Global->FlipFriendsCompletedFrame > 0) return;
            if (frame.Global->FlipFriendsMotionStartedFrame == 0)
            {
                int count = 0; var players = frame.Filter<FlipFriendsPlayer>(); while (players.Next(out _, out _)) count++;
                if (count < frame.PlayerCount) return;
                frame.Global->FlipFriendsMotionStartedFrame = frame.Number;
                var motions = frame.Filter<FlipFriendsMotion>();
                while (motions.NextUnsafe(out _, out var state)) state->ReadyFrame = frame.Number + 12;
            }
            Buttons(frame);
            SampleSwitches(frame);
            ToggleTerrain(frame);
            var configs = frame.RuntimeConfig.Motions;
            if (configs == null) return;
            // 콘텐츠 ID 순으로 생성한 엔티티 순서를 유지한다. 모든 승객의 가능 여부를 먼저 조사한 뒤 한 번에 이동한다.
            var query = frame.Filter<FlipFriendsMotion>();
            while (query.NextUnsafe(out _, out var state))
            {
                if (state->LastUpdateFrame == frame.Number) continue;
                state->LastUpdateFrame = frame.Number;
                state->Delta = default;
                var c = configs[state->ContentIndex];
                if (c.Kind == 4) continue;
                if (state->ResetPending)
                {
                    if (CanReset(frame, c, state->ContentIndex)) Reset(frame, c, state);
                    continue;
                }
                if (state->Phase == 0)
                {
                    if (c.DetectPlayer && frame.Number >= state->ReadyFrame && HasPassenger(frame, c, *state))
                    { state->Phase = 1; state->ElapsedTicks = -c.InitialTicks; }
                    continue;
                }
                int tick = state->ElapsedTicks + 1;
                var position = Sample(c, tick, out bool completed, out bool returned, out int cycle);
                bool collidable = c.Kind == 0 && !(c.DisableOnComplete && completed && !returned);
                bool teleport = returned || cycle != state->Cycle;
                var delta = position - state->Position;
                if (c.Kind == 0 && (teleport ? !CanReset(frame, c, state->ContentIndex) : !CanAdvance(frame, c, *state, delta, collidable)))
                { state->BlockedTicks++; continue; }
                if (c.Kind == 0 && !teleport && state->Collidable && collidable) Transport(frame, c, *state, delta);
                state->Position = position; state->Delta = teleport ? default : delta;
                state->Collidable = collidable; state->Cycle = cycle; state->ElapsedTicks = tick;
                state->HasReturned = returned;
                state->Phase = tick < 0 ? 1 : completed ? 3 : 2;
                state->Angle = (state->Angle + state->RotationSpeed * frame.DeltaTime) % (FP.Pi * 2);
                if (returned && c.DetectPlayer) Reset(frame, c, state);
            }
        }

        public static void Initialize(Frame frame)
        {
            if (frame.Global->FlipFriendsMotionsInitialized) return;
            var configs = frame.RuntimeConfig.Motions;
            if (configs != null) for (int i = 0; i < configs.Length; i++)
            {
                var c = configs[i]; var e = frame.Create();
                var speed = c.RotationSpeed;
                if (c.RandomRotation)
                {
                    speed += frame.RNG->Next(-c.RandomRange, c.RandomRange);
                    speed = FPMath.Abs(speed) * (frame.RNG->Next(0, 2) == 0 ? -1 : 1);
                }
                frame.Add(e, new FlipFriendsMotion { ContentIndex = i, Position = c.Origin, Collidable = c.Kind == 0 || c.Kind == 4 && c.InitialActive,
                    Phase = c.DetectPlayer ? 0 : 2, ReadyFrame = frame.Number + 12, RotationSpeed = speed * FP.Pi / 180 });
            }
            var buttons = frame.RuntimeConfig.MotionResets;
            if (buttons != null) for (int i = 0; i < buttons.Length; i++) { var e = frame.Create(); frame.Add(e, new FlipFriendsMotionReset { ContentIndex = i }); }
            var switches = frame.RuntimeConfig.StageSwitches;
            if (switches != null) for (int i = 0; i < switches.Length; i++)
            {
                var entity = frame.Create();
                frame.Add(entity, new FlipFriendsStageSwitch { ContentIndex = i });
            }
            frame.Global->FlipFriendsMotionsInitialized = true;
        }

        private static void SampleSwitches(Frame frame)
        {
            var configs = frame.RuntimeConfig.StageSwitches;
            if (configs == null || configs.Length == 0) return;
            var switches = frame.Filter<FlipFriendsStageSwitch>();
            while (switches.NextUnsafe(out _, out var state))
            {
                var config = configs[state->ContentIndex];
                int contacts = 0;
                if (config.DetectPlayers)
                {
                    var players = frame.Filter<Transform2D, FlipFriendsPlayer>();
                    while (players.Next(out _, out var transform, out var player))
                    {
                        if (player.IsFinished || FlipFriendsRespawnSystem.IsBlocked(frame, player)) continue;
                        var center = transform.Position + FlipFriendsInteractionSystem.Offset(player);
                        var half = FlipFriendsInteractionSystem.HalfSize(player);
                        bool touching = config.IsDoor ? Overlap(center, half, config.Center, config.HalfSize) :
                            !player.Carrier.IsValid && player.IsTouchingCeiling &&
                            FPMath.Abs(center.X - config.Center.X) < half.X + config.HalfSize.X - Tolerance &&
                            FPMath.Abs(center.Y + half.Y - config.Center.Y + config.HalfSize.Y) <= Tolerance;
                        if (touching) contacts++;
                    }
                }
                if (config.DetectBoxes)
                {
                    var boxes = frame.Filter<Transform2D, FlipFriendsBox>();
                    while (boxes.Next(out _, out var transform, out var box))
                        if (!box.RespawnPending && box.RespawnFrame != frame.Number &&
                            Overlap(transform.Position, box.HalfSize, config.Center, config.HalfSize)) contacts++;
                }
                state->Contacts = contacts;
                if (config.IsDoor)
                {
                    state->Active = contacts > 0;
                    continue;
                }
                if (contacts == 0 || frame.Number < state->ReadyFrame) continue;
                state->ReadyFrame = frame.Number + config.CooldownTicks;
                var motions = frame.Filter<FlipFriendsMotion>();
                while (motions.NextUnsafe(out _, out var target))
                    if (target->ContentIndex == config.Target) { target->Cycle ^= 1; break; }
            }
        }

        private static void ToggleTerrain(Frame f)
        {
            var configs = f.RuntimeConfig.Motions;
            if (configs == null) return;
            for (int i = 0; i < configs.Length; i++)
            {
                var c = configs[i]; if (c.Kind != 4) continue;
                bool first = true;
                for (int j = 0; j < i; j++) if (configs[j].Kind == 4 && configs[j].ToggleGroup == c.ToggleGroup) first = false;
                if (!first) continue;
                // 같은 버튼의 접촉은 한 요청이다. 서로 다른 버튼의 반전과 자동 위상을 합산한 뒤 한 번만 적용한다.
                bool odd = c.ToggleTicks > 0 && ((f.Number - f.FlipFriendsMotionStartedFrame) / c.ToggleTicks & 1) != 0;
                bool canChange = true;
                var switches = f.Filter<FlipFriendsStageSwitch>();
                while (switches.Next(out _, out var button))
                    if (f.RuntimeConfig.StageSwitches[button.ContentIndex].Target == i &&
                        f.RuntimeConfig.StageSwitches[button.ContentIndex].IsDoor) odd = button.Active;
                var query = f.Filter<FlipFriendsMotion>();
                while (query.Next(out _, out var target))
                    if (target.ContentIndex == i) { odd ^= target.Cycle != 0; break; }
                query = f.Filter<FlipFriendsMotion>();
                while (query.Next(out _, out var s))
                {
                    var part = configs[s.ContentIndex];
                    if (part.Kind != 4 || part.ToggleGroup != c.ToggleGroup || part.InitialActive == odd || s.Collidable) continue;
                    if (FlipFriendsTerrain.Overlaps(f.RuntimeConfig.TerrainContours, s.Position, part.HalfSize)) canChange = false;
                    var players = f.Filter<Transform2D, FlipFriendsPlayer>();
                    while (players.Next(out _, out var t, out var p)) if (!p.IsFinished && !p.RespawnPending && Overlap(s.Position, part.HalfSize, t.Position + FlipFriendsInteractionSystem.Offset(p), FlipFriendsInteractionSystem.HalfSize(p))) canChange = false;
                    var boxes = f.Filter<Transform2D, FlipFriendsBox>();
                    while (boxes.Next(out _, out var t, out var b)) if (!b.RespawnPending && Overlap(s.Position, part.HalfSize, t.Position, b.HalfSize)) canChange = false;
                    var others = f.Filter<FlipFriendsMotion>();
                    while (others.Next(out _, out var other))
                    {
                        var oc = configs[other.ContentIndex];
                        if (oc.Kind == 4 && oc.ToggleGroup == c.ToggleGroup || !other.Collidable) continue;
                        if (Overlap(s.Position, part.HalfSize, other.Position, oc.HalfSize)) canChange = false;
                    }
                }
                query = f.Filter<FlipFriendsMotion>();
                while (query.NextUnsafe(out _, out var s))
                {
                    var part = configs[s->ContentIndex]; if (part.Kind != 4 || part.ToggleGroup != c.ToggleGroup) continue;
                    s->Delta = default; s->ElapsedTicks = f.Number - f.FlipFriendsMotionStartedFrame;
                    if (!canChange) { s->BlockedTicks++; continue; }
                    bool active = part.InitialActive != odd;
                    if (s->Collidable != active) s->Revision++;
                    s->Collidable = active; s->Phase = active ? 2 : 0;
                }
                switches = f.Filter<FlipFriendsStageSwitch>();
                while (switches.NextUnsafe(out _, out var button))
                {
                    var config = f.RuntimeConfig.StageSwitches[button->ContentIndex];
                    if (config.Target == i && !config.IsDoor && canChange) button->Active = odd;
                }
            }
        }

        // elapsed는 Unity 시간과 무관한 프레임 상태다. 이징 표도 콘텐츠 해시에 포함된 FP 값만 읽는다.
        public static FPVector2 Sample(FlipFriendsMotionConfig c, int elapsed, out bool completed, out bool returned, out int cycle)
        {
            completed = returned = false; cycle = 0;
            if (elapsed <= 0 || c.Speed <= 0 || c.Kind == 2) return c.Origin;
            if (c.Kind == 1)
            {
                var d = c.Points[1]; var length = d.Magnitude;
                if (length == 0) return c.Origin;
                var travelled = ((FP)elapsed / 60 * c.Speed) % (length * 2);
                return c.Origin + d / length * (travelled <= length ? travelled : length * 2 - travelled);
            }
            int legs = c.Points.Length - 1, legTicks = c.SegmentTicks + c.WaitTicks;
            int end = legs * legTicks - c.WaitTicks;
            if (!c.Cyclic && !c.Repeat && elapsed >= end)
            {
                completed = true; returned = elapsed >= end + c.ReturnTicks;
                return c.Origin + c.Points[returned ? 0 : legs];
            }
            int route = (elapsed - 1) / (legs * legTicks), time = elapsed - route * legs * legTicks;
            int leg = System.Math.Min(legs - 1, time / legTicks), phase = time - leg * legTicks;
            bool reverse = c.Cyclic && (route & 1) != 0;
            int from = reverse ? legs - leg : leg, to = reverse ? from - 1 : from + 1;
            FP t = FPMath.Min(1, (FP)phase / 60 * c.Speed);
            FP scaled = t * 256;
            int index = System.Math.Min(255, (int)scaled);
            var eased = c.Ease[index] + (c.Ease[index + 1] - c.Ease[index]) * (scaled - index);
            cycle = c.Cyclic ? 0 : route;
            return c.Origin + c.Points[from] + (c.Points[to] - c.Points[from]) * eased;
        }

        private static bool Passenger(FlipFriendsMotionConfig c, FlipFriendsMotion s, Transform2D t, FlipFriendsPlayer p)
        {
            var half = FlipFriendsInteractionSystem.HalfSize(p);
            var center = t.Position + FlipFriendsInteractionSystem.Offset(p);
            return s.Collidable && p.IsGrounded && !p.Carrier.IsValid && !p.IsFinished && !p.RespawnPending &&
                FPMath.Abs(center.X - s.Position.X) < half.X + c.HalfSize.X &&
                FPMath.Abs(center.Y - half.Y - s.Position.Y - c.HalfSize.Y) <= Tolerance;
        }

        private static bool HasPassenger(Frame frame, FlipFriendsMotionConfig c, FlipFriendsMotion s)
        {
            var q = frame.Filter<Transform2D, FlipFriendsPlayer>();
            while (q.Next(out _, out var t, out var p)) if (Passenger(c, s, t, p) && !FlipFriendsRespawnSystem.IsBlocked(frame, p)) return true;
            return false;
        }

        private static FPVector2 Push(FlipFriendsMotionConfig c, FlipFriendsMotion s, FPVector2 center, FPVector2 half, FPVector2 d)
        {
            var result = default(FPVector2);
            if (d.X != 0 && FPMath.Abs(center.Y - s.Position.Y) < half.Y + c.HalfSize.Y - Tolerance)
            {
                FP sign = FPMath.Sign(d.X), gap = (center.X - s.Position.X) * sign - half.X - c.HalfSize.X;
                if (gap >= -Tolerance && gap < FPMath.Abs(d.X)) result.X = sign * (FPMath.Abs(d.X) - FPMath.Max(0, gap));
            }
            if (d.Y != 0 && FPMath.Abs(center.X - s.Position.X - d.X) < half.X + c.HalfSize.X - Tolerance)
            {
                FP sign = FPMath.Sign(d.Y), gap = (center.Y - s.Position.Y) * sign - half.Y - c.HalfSize.Y;
                if (gap >= -Tolerance && gap < FPMath.Abs(d.Y)) result.Y = sign * (FPMath.Abs(d.Y) - FPMath.Max(0, gap));
            }
            return result;
        }

        private static FPVector2 PlayerDelta(Frame f, FlipFriendsMotionConfig c, FlipFriendsMotion s, Transform2D t, FlipFriendsPlayer p, FPVector2 delta)
        {
            if (FlipFriendsRespawnSystem.IsBlocked(f, p)) return default;
            var input = f.FlipFriendsInput(p.Player);
            bool jumping = input.Jump.IsDown && !p.WasJumpHeld || p.Velocity.Y > 0 || p.IsClimbed;
            if (!jumping && Passenger(c, s, t, p)) return delta;
            return Push(c, s, t.Position + FlipFriendsInteractionSystem.Offset(p), FlipFriendsInteractionSystem.HalfSize(p), delta);
        }

        private static bool FitsSweep(Frame f, FPVector2 center, FPVector2 half, FPVector2 d, int ignored)
        {
            if (FlipFriendsMovementSystem.LimitAxis(f, center, d.X, true, half, ignored) != d.X) return false;
            center.X += d.X;
            return FlipFriendsMovementSystem.LimitAxis(f, center, d.Y, false, half, ignored) == d.Y;
        }

        private static bool CanAdvance(Frame f, FlipFriendsMotionConfig c, FlipFriendsMotion s, FPVector2 d, bool collidable)
        {
            if (!collidable || !s.Collidable || d == default) return true;
            if (!FitsSweep(f, s.Position, c.HalfSize, d, s.ContentIndex)) return false;
            var q = f.Filter<Transform2D, FlipFriendsPlayer>();
            while (q.Next(out var e, out var t, out var p))
            {
                if (p.Carrier.IsValid || p.IsFinished || p.RespawnPending) continue;
                var move = PlayerDelta(f, c, s, t, p, d);
                var center = t.Position + FlipFriendsInteractionSystem.Offset(p); var half = FlipFriendsInteractionSystem.HalfSize(p);
                if (!FitsSweep(f, center, half, move, s.ContentIndex)) return false;
                if (p.Held.IsValid)
                {
                    bool isPlayer = f.TryGet<FlipFriendsPlayer>(p.Held, out var held);
                    var h = isPlayer ? FlipFriendsInteractionSystem.HalfSize(held) : f.Get<FlipFriendsBox>(p.Held).HalfSize;
                    var at = t.Position + FlipFriendsInteractionSystem.HeldOffset + (isPlayer ? FlipFriendsInteractionSystem.Offset(held) : default);
                    if (!FitsSweep(f, at, h, move, s.ContentIndex) || Overlap(at + move, h, s.Position + d, c.HalfSize)) return false;
                }
                if (Overlap(center + move, half, s.Position + d, c.HalfSize)) return false;
                // 다른 승객도 같은 이동량을 적용할 수 있을 때만 몸체끼리 함께 전진한다.
                var others = f.Filter<Transform2D, FlipFriendsPlayer>();
                while (others.Next(out var other, out var ot, out var op))
                {
                    if (other == e || other == p.Held || op.Carrier.IsValid || op.IsFinished || op.RespawnPending) continue;
                    var om = PlayerDelta(f, c, s, ot, op, d);
                    if (Overlap(center + move, half, ot.Position + FlipFriendsInteractionSystem.Offset(op) + om, FlipFriendsInteractionSystem.HalfSize(op))) return false;
                }
                var boxes = f.Filter<Transform2D, FlipFriendsBox>();
                while (boxes.Next(out _, out var bt, out var b)) if (!b.Carrier.IsValid && !b.RespawnPending && Overlap(center + move, half, bt.Position, b.HalfSize)) return false;
            }
            // 원본 느슨한 상자에는 탑승 운반을 추가하지 않는다. 밀어 겹칠 때는 발판을 대기시킨다.
            var loose = f.Filter<Transform2D, FlipFriendsBox>();
            while (loose.Next(out _, out var bt, out var b)) if (!b.Carrier.IsValid && !b.RespawnPending &&
                (Push(c, s, bt.Position, b.HalfSize, d) != default || Overlap(bt.Position, b.HalfSize, s.Position + d, c.HalfSize))) return false;
            return true;
        }

        private static void Transport(Frame f, FlipFriendsMotionConfig c, FlipFriendsMotion s, FPVector2 d)
        {
            var q = f.Filter<Transform2D, FlipFriendsPlayer>();
            while (q.NextUnsafe(out _, out var t, out var p))
            {
                if (p->Carrier.IsValid || p->IsFinished || p->RespawnPending) continue;
                if (FlipFriendsRespawnSystem.IsBlocked(f, *p)) continue;
                var move = PlayerDelta(f, c, s, *t, *p, d);
                t->Position += move; p->PreviousPosition += move;
                if (p->Held.IsValid) f.Unsafe.GetPointer<Transform2D>(p->Held)->Position = t->Position + FlipFriendsInteractionSystem.HeldOffset;
            }
        }

        private static bool CanReset(Frame f, FlipFriendsMotionConfig c, int ignored)
        {
            if (Overlaps(f, c.Origin, c.HalfSize, ignored) || FlipFriendsTerrain.Overlaps(f.RuntimeConfig.TerrainContours, c.Origin, c.HalfSize)) return false;
            var q = f.Filter<Transform2D, FlipFriendsPlayer>();
            while (q.Next(out _, out var t, out var p)) if (!p.IsFinished && !p.RespawnPending && Overlap(c.Origin, c.HalfSize, t.Position + FlipFriendsInteractionSystem.Offset(p), FlipFriendsInteractionSystem.HalfSize(p))) return false;
            var boxes = f.Filter<Transform2D, FlipFriendsBox>();
            while (boxes.Next(out _, out var t, out var b)) if (!b.RespawnPending && Overlap(c.Origin, c.HalfSize, t.Position, b.HalfSize)) return false;
            return true;
        }

        private static void Reset(Frame f, FlipFriendsMotionConfig c, FlipFriendsMotion* s)
        {
            s->Position = c.Origin; s->Delta = default; s->ElapsedTicks = 0; s->Phase = c.DetectPlayer ? 0 : 2;
            s->ReadyFrame = f.Number + 12; s->Revision++; s->Cycle = 0; s->Angle = 0;
            s->Collidable = c.Kind == 0; s->ResetPending = false; s->HasReturned = false;
        }

        private static void Buttons(Frame f)
        {
            var configs = f.RuntimeConfig.MotionResets;
            if (configs == null) return;
            var q = f.Filter<FlipFriendsMotionReset>();
            while (q.NextUnsafe(out _, out var s))
            {
                var c = configs[s->ContentIndex]; int contacts = 0;
                var p = f.Filter<Transform2D, FlipFriendsPlayer>();
                while (p.Next(out _, out var t, out var state))
                {
                    if (state.IsFinished || state.Carrier.IsValid || FlipFriendsRespawnSystem.IsBlocked(f, state)) continue;
                    var center = t.Position + FlipFriendsInteractionSystem.Offset(state); var half = FlipFriendsInteractionSystem.HalfSize(state);
                    bool touching = c.IsTrigger ? Overlap(center, half, c.Center, c.HalfSize) :
                        FPMath.Abs(center.X - c.Center.X) < half.X + c.HalfSize.X && center.Y < c.Center.Y && FPMath.Abs(center.Y + half.Y - c.Center.Y + c.HalfSize.Y) <= FlipFriendsInteractionSystem.Skin;
                    if (touching) contacts |= 1 << (int)state.Player;
                }
                if (contacts != 0 && s->Contacts == 0)
                {
                    var motions = f.Filter<FlipFriendsMotion>();
                    while (motions.NextUnsafe(out _, out var motion)) if (motion->ContentIndex == c.Target) motion->ResetPending = true;
                }
                s->Contacts = contacts;
            }
        }

        public static bool Overlap(FPVector2 a, FPVector2 ah, FPVector2 b, FPVector2 bh) => FPMath.Abs(a.X - b.X) < ah.X + bh.X && FPMath.Abs(a.Y - b.Y) < ah.Y + bh.Y;

        public static bool Overlaps(Frame f, FPVector2 center, FPVector2 half, int ignored = -1)
        {
            var configs = f.RuntimeConfig.Motions;
            if (configs != null)
            {
                var q = f.Filter<FlipFriendsMotion>();
                while (q.Next(out _, out var s)) if (s.ContentIndex != ignored && s.Collidable && Overlap(center, half, s.Position, configs[s.ContentIndex].HalfSize)) return true;
            }
            var resets = f.RuntimeConfig.MotionResets;
            if (resets != null) foreach (var r in resets) if (!r.IsTrigger && Overlap(center, half, r.Center, r.HalfSize)) return true;
            var switches = f.RuntimeConfig.StageSwitches;
            if (switches != null) foreach (var s in switches) if (!s.IsDoor && Overlap(center, half, s.Center, s.HalfSize)) return true;
            return false;
        }

        public static FP Limit(Frame f, FPVector2 center, FPVector2 half, FP amount, bool horizontal, int ignored = -1)
        {
            if (amount == 0) return amount;
            FP sign = FPMath.Sign(amount), allowed = FPMath.Abs(amount);
            void Test(FPVector2 at, FPVector2 size)
            {
                var d = at - center;
                if (FPMath.Abs(horizontal ? d.Y : d.X) >= (horizontal ? half.Y + size.Y : half.X + size.X) - Tolerance) return;
                var gap = (horizontal ? d.X : d.Y) * sign - (horizontal ? half.X + size.X : half.Y + size.Y);
                if (gap >= -Tolerance) allowed = FPMath.Min(allowed, FPMath.Max(0, gap));
            }
            var configs = f.RuntimeConfig.Motions;
            if (configs != null)
            {
                var q = f.Filter<FlipFriendsMotion>();
                while (q.Next(out _, out var s)) if (s.ContentIndex != ignored && s.Collidable) Test(s.Position, configs[s.ContentIndex].HalfSize);
            }
            var resets = f.RuntimeConfig.MotionResets;
            if (resets != null) foreach (var r in resets) if (!r.IsTrigger) Test(r.Center, r.HalfSize);
            var switches = f.RuntimeConfig.StageSwitches;
            if (switches != null) foreach (var s in switches) if (!s.IsDoor) Test(s.Center, s.HalfSize);
            return allowed * sign;
        }

        public static FlipFriendsTrapConfig TrapAt(Frame f, FlipFriendsTrapConfig trap)
        {
            var configs = f.RuntimeConfig.Motions;
            if (configs == null) return trap;
            var q = f.Filter<FlipFriendsMotion>();
            while (q.Next(out _, out var s))
            {
                var c = configs[s.ContentIndex];
                if (c.Kind == 0 || c.Kind == 4 || c.Id != trap.Id) continue;
                FP sin = FPMath.Sin(s.Angle), cos = FPMath.Cos(s.Angle);
                var offset = trap.Center - c.Origin;
                trap.Center = s.Position + new FPVector2(offset.X * cos - offset.Y * sin, offset.X * sin + offset.Y * cos);
                var vertices = new FPVector2[trap.Vertices.Length];
                for (int i = 0; i < vertices.Length; i++)
                {
                    var v = trap.Vertices[i] - c.Origin;
                    vertices[i] = s.Position + new FPVector2(v.X * cos - v.Y * sin, v.X * sin + v.Y * cos);
                }
                trap.Vertices = vertices; trap.Origin = s.Position;
                return trap;
            }
            return trap;
        }
    }
}
