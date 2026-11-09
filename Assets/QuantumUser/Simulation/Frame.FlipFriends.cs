namespace Quantum
{
    public unsafe partial class Frame
    {
        public int FlipFriendsCompletedFrame { get => Global->FlipFriendsCompletedFrame; set => Global->FlipFriendsCompletedFrame = value; }
        public int FlipFriendsMotionStartedFrame => Global->FlipFriendsMotionStartedFrame;
        public int FlipFriendsProjectileCount { get => Global->FlipFriendsProjectileCount; set => Global->FlipFriendsProjectileCount = value; }
        public int FlipFriendsProjectileSpawned => Global->FlipFriendsProjectileSpawned;
        public int FlipFriendsProjectileExpired => Global->FlipFriendsProjectileExpired;
        public int FlipFriendsDroppedShots { get => Global->FlipFriendsDroppedShots; set => Global->FlipFriendsDroppedShots = value; }
        public bool FlipFriendsDevicesInitialized { get => Global->FlipFriendsDevicesInitialized; set => Global->FlipFriendsDevicesInitialized = value; }
        public Input FlipFriendsInput(PlayerRef player)
        {
            var input = GetPlayerInput(player);
            return input == null ? default : *input;
        }
    }
}
