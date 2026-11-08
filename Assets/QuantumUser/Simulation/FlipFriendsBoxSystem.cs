namespace Quantum
{
    using Photon.Deterministic;

    public unsafe class FlipFriendsBoxSystem : SystemMainThreadFilter<FlipFriendsBoxSystem.Filter>
    {
        public struct Filter
        {
            public EntityRef Entity;
            public Transform2D* Transform;
            public FlipFriendsBox* Box;
        }

        public override void Update(Frame frame, ref Filter filter)
        {
            Simulate(frame, filter.Entity, ref *filter.Transform, ref *filter.Box);
        }

        public static void Simulate(Frame frame, EntityRef entity, ref Transform2D transform, ref FlipFriendsBox box)
        {
            if (frame.Global->FlipFriendsCompletedFrame > 0 || box.Carrier.IsValid || box.RespawnPending || box.RespawnFrame == frame.Number) return;
            box.Velocity.X = FlipFriendsMovementSystem.SmoothHorizontal(box.Velocity.X, 0, ref box.VelocityXSmoothing, FP.FromString("1.5"), frame.DeltaTime);
            box.Velocity.Y = FPMath.Max(-12, box.Velocity.Y - FP.FromString("9.8") * frame.DeltaTime);
            var delta = box.Velocity * frame.DeltaTime;
            var position = transform.Position;
            FlipFriendsMovementSystem.PrepareSlope(frame, ref position, ref delta, default, box.HalfSize, default, entity);
            var dx = FlipFriendsMovementSystem.LimitHorizontalWithMicroStep(frame, ref position, default, delta.X, box.HalfSize);
            dx = FlipFriendsInteractionSystem.LimitBoxes(frame, position, box.HalfSize, dx, true, entity);
            dx = FlipFriendsInteractionSystem.LimitPlayerBodies(frame, default, position, box.HalfSize, dx, true, isBox: true);
            position.X += dx;
            var dy = FlipFriendsMovementSystem.LimitAxis(frame, position, delta.Y, false, box.HalfSize);
            dy = FlipFriendsInteractionSystem.LimitBoxes(frame, position, box.HalfSize, dy, false, entity);
            dy = FlipFriendsInteractionSystem.LimitPlayerBodies(frame, default, position, box.HalfSize, dy, false, isBox: true);
            box.IsGrounded = delta.Y < 0 && dy != delta.Y;
            if (dy != delta.Y) box.Velocity.Y = 0;
            if (dx != delta.X) box.Velocity.X = 0;
            position.Y += dy;
            transform.Position = position;
        }
    }

    public unsafe class FlipFriendsCarryFollowSystem : SystemMainThreadFilter<FlipFriendsCarryFollowSystem.Filter>
    {
        public struct Filter
        {
            public EntityRef Entity;
            public Transform2D* Transform;
            public FlipFriendsPlayer* Player;
        }

        public override void Update(Frame frame, ref Filter filter)
        {
            if (!filter.Player->Held.IsValid) return;
            if (frame.Unsafe.TryGetPointer<Transform2D>(filter.Player->Held, out var target))
                target->Position = filter.Transform->Position + FlipFriendsInteractionSystem.HeldOffset;
            else filter.Player->Held = default;
        }
    }
}
