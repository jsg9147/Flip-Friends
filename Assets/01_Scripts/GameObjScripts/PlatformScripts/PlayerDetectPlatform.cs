using Mirror;
using UnityEngine;

public class PlayerDetectPlatform : PlatformController
{
    public enum Phase { WaitingForPlayer, InitialDelay, Moving, Completed }

    [Header("Platform Settings")]
    public float initialWaitTime;
    public bool disappearAfterComplete;
    public bool disableColliderWhenFalling;
    [Header("References")]
    public SpriteRenderer spriteRenderer;

    [SyncVar(hook = nameof(OnPhaseChanged))] private Phase phase;
    private bool isPlayerDetected;
    private double detectionReadyAt;
    private bool isPresentationInitialized;
    private bool initialColliderEnabled;
    private Color initialColor;
    private Animator platformAnimator;
    private const double DetectionDelay = .2;
    private const float FadeDuration = .5f;

    protected override bool AutoStart => false;
    public Phase CurrentPhase => phase;
    public bool UsesSpriteEffects => disableColliderWhenFalling || disappearAfterComplete;

    public override void OnStartServer()
    {
        base.OnStartServer();
        ResetPlatformState();
    }

    public override void Start()
    {
        base.Start();
        InitializePresentation();
        ApplyPresentation();
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        InitializePresentation();
        ApplyPresentation();
    }

    private void InitializePresentation()
    {
        if (isPresentationInitialized) return;
        if (boxCollider == null) Awake();
        initialColliderEnabled = boxCollider.enabled;
        initialColor = spriteRenderer != null ? spriteRenderer.color : Color.white;
        platformAnimator = GetComponent<Animator>();
        isPresentationInitialized = true;
        if (UsesSpriteEffects && spriteRenderer == null)
            Debug.LogError($"{transform.root.name}/{name}: 발판 연출용 SpriteRenderer가 없습니다.", this);
    }

    private void Update()
    {
        if (isServer && phase == Phase.WaitingForPlayer && NetworkTime.time >= detectionReadyAt)
            DetectPlayer();
        ApplyPresentation();
    }

    public override void FixedUpdate()
    {
        base.FixedUpdate();
        if (!isServer) return;
        if (isWaitingAtStart)
        {
            ResetPlatformState();
            return;
        }
        if (!isPlayerDetected) return;
        phase = isCompleted ? Phase.Completed : NetworkTime.time >= MotionStartedAt ? Phase.Moving : Phase.InitialDelay;
        ApplyPresentation();
    }

    [Server]
    public void ServerActivate()
    {
        if (!isServer || phase != Phase.WaitingForPlayer) return;
        isPlayerDetected = true;
        phase = Phase.InitialDelay;
        StartMotion(NetworkTime.time + Mathf.Max(0, initialWaitTime));
        ApplyPresentation();
    }

    private void DetectPlayer()
    {
        UpdateRaycastOrigins();
        for (int i = 0; i < verticalRayCount; i++)
        {
            Vector2 origin = raycastOrigins.topLeft + Vector2.right * (verticalRaySpacing * i);
            foreach (var hit in Physics2D.RaycastAll(origin, Vector2.up, skinWidth, passengerMask))
            {
                if (hit.collider == null || !hit.collider.TryGetComponent(out Controller2D player)) continue;
                if (!player.collisions.below) continue;
                if (hit.transform.position.y - hit.collider.bounds.size.y * .5f - hit.collider.offset.y <=
                    transform.position.y + boxCollider.offset.y + boxCollider.size.y * .5f) continue;
                ServerActivate();
                return;
            }
        }
    }

    private void OnPhaseChanged(Phase oldValue, Phase newValue) => ApplyPresentation();

    public override bool IsCollidableAt(double time)
    {
        InitializePresentation();
        var sample = SampleAt(time);
        return initialColliderEnabled && !(disableColliderWhenFalling && sample.isCompleted && !sample.hasReturned);
    }

    private void ApplyPresentation()
    {
        InitializePresentation();
        var sample = SampleAt(isServer ? NetworkTime.time : NetworkTime.predictedTime);
        bool hasCompleted = IsMotionActive && sample.isCompleted && !sample.hasReturned;
        boxCollider.enabled = IsCollidableAt(isServer ? NetworkTime.time : NetworkTime.predictedTime);
        if (platformAnimator != null)
            platformAnimator.SetBool("isFall", disableColliderWhenFalling && IsMotionActive && !sample.hasReturned);
        if (!UsesSpriteEffects || spriteRenderer == null) return;
        Color color = initialColor;
        if (disableColliderWhenFalling && IsMotionActive && !sample.hasReturned)
            color.a *= 1f - Mathf.Clamp01((float)((isServer ? NetworkTime.time : NetworkTime.predictedTime) - MotionStartedAt) / FadeDuration);
        if (disappearAfterComplete && hasCompleted) color.a = 0;
        spriteRenderer.color = color;
    }

    [Server]
    public void ResetPlatformState()
    {
        if (!isServer) return;
        InitializePresentation();
        ResetMotion();
        isPlayerDetected = false;
        phase = Phase.WaitingForPlayer;
        detectionReadyAt = NetworkTime.time + DetectionDelay;
        ApplyPresentation();
    }
}
