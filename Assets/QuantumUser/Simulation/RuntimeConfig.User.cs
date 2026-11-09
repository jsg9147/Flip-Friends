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
        public FlipFriendsConveyorConfig[] Conveyors;
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
        // Target이 -1이면 벨트만 있는 그룹이며, 그 그룹 첫 벨트(Conveyors 색인)가 수동 반전 홀짝을 저장한다.
        public int ConveyorAnchor;
        public bool IsDoor, DetectPlayers, DetectBoxes;
        public Photon.Deterministic.FPVector2 Center, HalfSize;
    }

    // 벨트 판정은 Motion 색인의 고체 발판(고정 벨트는 속도0 발판)을 그대로 쓴다.
    // Group이 비면 고정 방향, TerrainAnchor>=0이면 그 전환 지형의 적용 상태, 아니면 BeltAnchor 벨트의 그룹 상태를 따른다.
    [System.Serializable]
    public struct FlipFriendsConveyorConfig
    {
        public string Id, Group;
        public int Motion, TerrainAnchor, BeltAnchor, GroupTicks;
        public bool InitialClockwise;
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
