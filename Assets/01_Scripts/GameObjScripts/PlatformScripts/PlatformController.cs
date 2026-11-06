using System;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

[DefaultExecutionOrder(-20)]
public class PlatformController : RaycastController
{
    public Color previewColor = Color.red;
    [Header("Platform Settings")]
    public LayerMask passengerMask;
    public Vector3[] localWaypoints;
    public float speed;
    public bool cyclic;
    public bool isRepeat;
    public float originalPositionDelay;
    public float waitTime;
    [Range(0, 2)] public float easeAmount;
    [SerializeField] private bool isPassengerTransportEnabled = true;

    [SyncVar] private Vector3 initialPosition;
    [SyncVar] private double motionStartedAt;
    [SyncVar] private bool isMotionActive;
    [SyncVar] private uint motionRevision;
    private bool isInitialized;

    protected bool isWaitingAtStart;
    protected bool isCompleted;
    protected virtual bool AutoStart => true;
    public uint MotionRevision => motionRevision;
    public bool IsMotionActive => isMotionActive;
    public Vector3 InitialPosition => initialPosition;
    public double MotionStartedAt => motionStartedAt;
    public bool TransportsPassengers => isPassengerTransportEnabled;
    public virtual bool IsCollidableAt(double time) => boxCollider != null && boxCollider.enabled;

    private static readonly List<PlatformController> platforms = new List<PlatformController>();
    protected override Bounds GetColliderBounds() => ComputeBoundsFromTransform();

    public override void OnStartServer()
    {
        base.OnStartServer();
        InitializePosition();
        isMotionActive = AutoStart;
        motionStartedAt = NetworkTime.time;
    }

    public override void Start()
    {
        base.Start();
        InitializePosition();
        if (!platforms.Contains(this)) platforms.Add(this);
    }

    private void InitializePosition()
    {
        if (isInitialized) return;
        if (!isClient || isServer) initialPosition = transform.position;
        isInitialized = true;
        if (localWaypoints == null || localWaypoints.Length < 2)
            Debug.LogError($"{transform.root.name}/{name}: 이동 경유점이 2개 미만입니다.", this);
    }

    protected virtual void OnDisable() => platforms.Remove(this);
    protected virtual void OnEnable()
    {
        if (isInitialized && !platforms.Contains(this)) platforms.Add(this);
    }

    [Server]
    protected void StartMotion(double startTime)
    {
        InitializePosition();
        motionStartedAt = startTime;
        isMotionActive = true;
        isCompleted = false;
        isWaitingAtStart = false;
    }

    [Server]
    protected void ResetMotion()
    {
        InitializePosition();
        isMotionActive = false;
        motionStartedAt = NetworkTime.time;
        motionRevision++;
        isCompleted = false;
        isWaitingAtStart = false;
        transform.position = initialPosition;
        UpdateRaycastOrigins();
    }

    public PlatformMotion.Sample SampleAt(double time)
    {
        if (!isMotionActive) return new PlatformMotion.Sample { offset = Vector3.zero };
        return PlatformMotion.Evaluate(localWaypoints, speed, waitTime, cyclic, isRepeat, originalPositionDelay, time - motionStartedAt, easeAmount);
    }

    public Vector3 PositionAt(double time) => initialPosition + SampleAt(time).offset;

    public Vector2 Displacement(double fromTime, double toTime, uint revision)
    {
        if (!isMotionActive || revision != motionRevision || toTime < fromTime) return Vector2.zero;
        var from = SampleAt(fromTime);
        var to = SampleAt(toTime);
        // 자동 복귀나 비왕복 경로의 시작점 전환은 승객을 순간이동시키지 않는다.
        if (from.hasReturned || to.hasReturned || from.cycle != to.cycle) return Vector2.zero;
        return to.offset - from.offset;
    }

    public virtual void FixedUpdate()
    {
        if (!isInitialized) return;
        double now = isServer ? NetworkTime.time : NetworkTime.predictedTime;
        transform.position = PositionAt(now);
        var sample = SampleAt(now);
        isCompleted = sample.isCompleted;
        isWaitingAtStart = sample.hasReturned;
    }

    // 외부 입력별 시점에 발판 판정을 맞추고 같은 시뮬레이션 안에서 반드시 복구한다.
    public static SimulationScope AtSimulationTime(double time) => SimulationScope.Rent(time);

    public sealed class SimulationScope : IDisposable
    {
        private static readonly Stack<SimulationScope> pool = new Stack<SimulationScope>();
        private readonly List<Transform> targets = new List<Transform>();
        private readonly List<Vector3> positions = new List<Vector3>();
        private readonly List<Collider2D> colliders = new List<Collider2D>();
        private readonly List<bool> colliderStates = new List<bool>();

        private SimulationScope() { }

        internal static SimulationScope Rent(double time)
        {
            var scope = pool.Count > 0 ? pool.Pop() : new SimulationScope();
            scope.Apply(time);
            return scope;
        }

        private void Apply(double time)
        {
            foreach (var platform in platforms)
            {
                if (platform == null || !platform.isActiveAndEnabled) continue;
                targets.Add(platform.transform);
                positions.Add(platform.transform.position);
                platform.transform.position = platform.PositionAt(time);
                if (platform.boxCollider != null)
                {
                    colliders.Add(platform.boxCollider);
                    colliderStates.Add(platform.boxCollider.enabled);
                    platform.boxCollider.enabled = platform.IsCollidableAt(time);
                }
            }
            if (targets.Count > 0) Physics2D.SyncTransforms();
        }

        public void Dispose()
        {
            for (int i = 0; i < targets.Count; i++)
                if (targets[i] != null) targets[i].position = positions[i];
            for (int i = 0; i < colliders.Count; i++)
                if (colliders[i] != null) colliders[i].enabled = colliderStates[i];
            if (targets.Count > 0) Physics2D.SyncTransforms();
            targets.Clear();
            positions.Clear();
            colliders.Clear();
            colliderStates.Clear();
            pool.Push(this);
        }
    }

    private void OnDrawGizmos()
    {
        if (localWaypoints == null) return;
        Gizmos.color = previewColor;
        foreach (var waypoint in localWaypoints)
        {
            Vector3 point = (Application.isPlaying && isInitialized ? initialPosition : transform.position) + waypoint;
            Gizmos.DrawLine(point - Vector3.up * .3f, point + Vector3.up * .3f);
            Gizmos.DrawLine(point - Vector3.left * .3f, point + Vector3.left * .3f);
        }
    }
}
