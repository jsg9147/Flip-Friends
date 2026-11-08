namespace Quantum
{
    public unsafe class FlipFriendsCompletionSystem : SystemMainThread
    {
        public override void Update(Frame frame)
        {
            if (!frame.RuntimeConfig.EnableGoals || frame.Global->FlipFriendsCompletedFrame != 0) return;
            if (frame.RuntimeConfig.Devices != null && frame.RuntimeConfig.Devices.Length > 0)
            {
                for (int index = 0; index < frame.PlayerCount; index++)
                {
                    var query = frame.Filter<Transform2D, FlipFriendsPlayer>();
                    while (query.NextUnsafe(out var entity, out var transform, out var state))
                    {
                        if ((int)state->Player != index) continue;
                        if (FlipFriendsMovementSystem.TryFinish(frame, ref *transform, ref *state, frame.FlipFriendsInput(state->Player)) &&
                            state->Held.IsValid && !FlipFriendsInteractionSystem.Release(frame, entity, true))
                        { state->IsFinished = false; state->FinishedFrame = 0; }
                    }
                }
            }
            var players = frame.Filter<FlipFriendsPlayer>();
            int count = 0;
            while (players.Next(out var entity, out var player))
            {
                if (!player.IsFinished) return;
                count++;
            }
            if (count == frame.PlayerCount) frame.Global->FlipFriendsCompletedFrame = frame.Number;
        }
    }
}
