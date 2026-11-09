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
        public FlipFriendsTrapConfig[] Traps;
        public FlipFriendsLauncherConfig[] Launchers;
        public FlipFriendsMotionConfig[] Motions;
        public FlipFriendsMotionResetConfig[] MotionResets;
        public FlipFriendsStageSwitchConfig[] StageSwitches;
    }

    [System.Serializable]
    public struct FlipFriendsMotionConfig
    {
        public string Id;
        public int Kind;
        public Photon.Deterministic.FPVector2 Origin, HalfSize;
        public Photon.Deterministic.FPVector2[] Points;
        public Photon.Deterministic.FP[] Ease;
        public Photon.Deterministic.FP Speed, RotationSpeed, RandomRange;
        public int SegmentTicks, WaitTicks, ReturnTicks, InitialTicks;
        public bool Cyclic, Repeat, DetectPlayer, Disappear, DisableOnComplete, RandomRotation;
        public string ToggleGroup;
        public int ToggleTicks;
        public bool InitialActive;
    }

    [System.Serializable]
    public struct FlipFriendsStageSwitchConfig
    {
        public string Id;
        public int Target, CooldownTicks;
        public bool IsDoor, DetectPlayers, DetectBoxes;
        public Photon.Deterministic.FPVector2 Center, HalfSize;
    }

    [System.Serializable]
    public struct FlipFriendsMotionResetConfig
    {
        public string Id;
        public int Target;
        public Photon.Deterministic.FPVector2 Center, HalfSize;
        public bool IsTrigger;
    }

    [System.Serializable]
    public struct FlipFriendsLauncherConfig
    {
        public string Id;
        public int Kind;
        public Photon.Deterministic.FPVector2 Origin, Center;
        public Photon.Deterministic.FP Radius;
        public Photon.Deterministic.FPVector2[] Vertices;
    }

    // 정적 피격 판정. 꼭짓점은 월드 좌표의 반시계 볼록 다각형이며, Direction이 0이면 기준점→플레이어 방향을 쓴다.
    [System.Serializable]
    public struct FlipFriendsTrapConfig
    {
        public string Id;
        public Photon.Deterministic.FPVector2 Origin, Direction;
        public Photon.Deterministic.FPVector2 Center;
        public Photon.Deterministic.FP Radius;
        public Photon.Deterministic.FPVector2[] Vertices;
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
