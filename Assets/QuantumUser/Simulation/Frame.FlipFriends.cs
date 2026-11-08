namespace Quantum
{
    public unsafe partial class Frame
    {
        public int FlipFriendsCompletedFrame { get => Global->FlipFriendsCompletedFrame; set => Global->FlipFriendsCompletedFrame = value; }
        public bool FlipFriendsDevicesInitialized { get => Global->FlipFriendsDevicesInitialized; set => Global->FlipFriendsDevicesInitialized = value; }
        public Input FlipFriendsInput(PlayerRef player)
        {
            var input = GetPlayerInput(player);
            return input == null ? default : *input;
        }
    }
}
