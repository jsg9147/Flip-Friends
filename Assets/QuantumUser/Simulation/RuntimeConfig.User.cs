namespace Quantum
{
    public partial class RuntimeConfig
    {
        public string CustomMapAgreement;
        public bool EnableRespawn;
        public Photon.Deterministic.FP FallMinX;
        public Photon.Deterministic.FP FallMaxX;
        public Photon.Deterministic.FP FallMinY;
        public Photon.Deterministic.FPVector2[] CustomMapSpawns;
        public Photon.Deterministic.FPVector2[] CustomMapRopeCenters;
        public Photon.Deterministic.FPVector2[] CustomMapRopeSizes;
        public bool EnableGoals;
        public Photon.Deterministic.FPVector2[] GoalCenters;
        public Photon.Deterministic.FPVector2[] GoalSizes;
        public Photon.Deterministic.FPVector2[] BoxCenters;
        public Photon.Deterministic.FPVector2[] BoxSizes;
        public FlipFriendsDeviceConfig[] Devices;
        public FlipFriendsTerrainContour[] TerrainContours;
    }

    [System.Serializable]
    public struct FlipFriendsDeviceConfig
    {
        public string Id;
        public int Kind;
        public int CheckpointId;
        public Photon.Deterministic.FPVector2 Center, Size, Destination;
        public Photon.Deterministic.FPVector2[] ContactCenters, ContactSizes;
        public bool OnlyBoxReset;
        public bool DetectPlayers, DetectBoxes;
        public int TargetBoxIndex;
        public int CooldownTicks;
    }
}
