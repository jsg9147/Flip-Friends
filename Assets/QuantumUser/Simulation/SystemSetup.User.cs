namespace Quantum
{
    using System;
    using System.Collections.Generic;

    public static partial class DeterministicSystemSetup
    {
        static partial void AddSystemsUser(ICollection<SystemBase> systems, RuntimeConfig gameConfig, SimulationConfig simulationConfig, SystemsConfig systemsConfig)
        {
            systems.Add(new FlipFriendsRespawnSystem(false));
            systems.Add(new FlipFriendsInteractionSystem());
            systems.Add(new FlipFriendsMovementSystem());
            systems.Add(new FlipFriendsBoxSystem());
            systems.Add(new FlipFriendsCarryFollowSystem());
            systems.Add(new FlipFriendsRespawnSystem(true));
            systems.Add(new FlipFriendsDeviceSystem());
            systems.Add(new FlipFriendsHazardSystem());
            systems.Add(new FlipFriendsLauncherSystem());
            systems.Add(new FlipFriendsCompletionSystem());
        }
    }
}
