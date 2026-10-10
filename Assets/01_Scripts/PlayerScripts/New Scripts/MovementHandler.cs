using UnityEngine;
using Mirror;

[RequireComponent(typeof(Controller2D))]
public class MovementHandler : NetworkBehaviour
{
    [Header("이동 설정")]
    public float maxJumpHeight;
    public float minJumpHeight;
    public float runJumpHeight;
    public float timeToJumpApex;
    public float moveSpeed;
    public float runSpeed;
    public float conveyorSpeed;
    public float conveyorAccelerationSpeed;

    [Header("벽 상호작용")]
    public Vector2 wallJumpClimb;
    public Vector2 wallJumpOff;
    public Vector2 wallLeap;
    public float wallSlideSpeedMax = 3f;
    public float wallStickTime = 0.25f;
    public float wallSlideTime;
    public float bounceForce = 30f;
    public float springJumpForce = 50f;

    [Header("피격")]
    public Vector2 damagedMove;
    public float invincibilityDuration = 1f;

    private const float RopeJumpClimbBlockDuration = 0.3f;
    private const float StompClimbBlockDuration = 0.3f;
    private const float StompJumpBlockDuration = 0.3f;
    private const float StompHoldJumpMultiplier = 1.2f;
    private const float StompHorizontalPush = 0.5f;
    private const float DamagedClimbBlockDuration = 1f;
    private const float DamagedUncontrollableDuration = 1f;

    private float gravity;
    private float maxGravity = 12f;
    private float maxJumpVelocity;
    private float minJumpVelocity;
    private float velocityXSmoothing;

    private Vector2 velocity;
    private Vector2 externalVelocity;
    private Vector2 directionalInput;
    private bool isRunPressed;
    private bool wallSliding;
    private int wallDirX;

    private Controller2D controller;
    private ServerMover serverMover;
    private float currentMoveSpeed;

    // 코루틴 대신 Simulate 안에서 줄어드는 타이머다. 재시뮬레이션 때 StatePayload로 되돌릴 수 있어야 서버와 결과가 같다.
    private float jumpBlockTimer;
    private float climbBlockTimer;
    private float invincibleTimer;
    private float uncontrollableTimer;

    // 로프 트리거 겹침 수. 서버와 소유 클라이언트가 각자 물리로 세므로 등반 진입을 예측할 수 있다.
    private int ropeContactCount;

    public bool isClimbed { get; private set; }
    private bool isJumpHold;

    private bool isJumpBlocked => jumpBlockTimer > 0f;
    private bool isClimbBlocked => climbBlockTimer > 0f;
    private bool isInvincible => invincibleTimer > 0f;
    private bool isUncontrollable => uncontrollableTimer > 0f;

    public bool isGrounded => controller.collisions.below;
    public Vector2 CurrentVelocity => velocity;
    public float Gravity => gravity;
    public float MaxFallSpeed => maxGravity;

    // ServerMover가 Simulate를 직접 호출하는 경우 true — FixedUpdate 중복 실행 방지용
    [System.NonSerialized] public bool managedExternally = false;

    // ClientMover가 재시뮬레이션(Reconcile) 중일 때 true — 사운드 등 부작용 억제
    [System.NonSerialized] public bool isReconciling = false;

    private void Awake()
    {
        Initialize();
    }

    private void FixedUpdate()
    {
        // ClientMover(isOwned)와 ServerMover(managedExternally)가 각각 Simulate를 호출
        if (!isServer || isOwned || managedExternally) return;
        Simulate(Time.fixedDeltaTime);
    }

    private void Initialize()
    {
        controller = GetComponent<Controller2D>();
        serverMover = GetComponent<ServerMover>();
        gravity = -((2 * maxJumpHeight) / Mathf.Pow(timeToJumpApex, 2)) * 0.9f;
        maxJumpVelocity = Mathf.Abs(gravity) * timeToJumpApex;
        minJumpVelocity = Mathf.Sqrt(2 * Mathf.Abs(gravity) * minJumpHeight);
        currentMoveSpeed = moveSpeed;
    }

    // 한 물리 스텝을 실행 — ClientMover와 ServerMover가 동일한 코드를 공유하기 위해 분리
    public void Simulate(float deltaTime)
    {
        TickTimers(deltaTime);
        UpdateClimbState();
        CalculateMovement(deltaTime);
        PlayerMovementInteract();
        ApplyMovement(deltaTime);
    }

    private void TickTimers(float deltaTime)
    {
        jumpBlockTimer = Mathf.Max(0f, jumpBlockTimer - deltaTime);
        climbBlockTimer = Mathf.Max(0f, climbBlockTimer - deltaTime);
        invincibleTimer = Mathf.Max(0f, invincibleTimer - deltaTime);
        uncontrollableTimer = Mathf.Max(0f, uncontrollableTimer - deltaTime);
    }

    private void UpdateClimbState()
    {
        if (ropeContactCount <= 0)
        {
            isClimbed = false;
            return;
        }

        // 물건이나 플레이어를 든 채로는 로프를 탈 수 없다.
        if (!isClimbed && directionalInput.y != 0 && !isClimbBlocked && !controller.isHold)
            isClimbed = true;
    }

    // InputPayload를 받아 내부 입력 상태를 설정한 뒤 시뮬레이션
    public void ApplyInput(InputPayload input)
    {
        SetDirectionalInput(input.movement, input.run);
        JumpHold(input.jumpHeld);

        if (input.jump) OnJumpInputDown();
        if (input.jumpUp) OnJumpInputUp();
    }

    public StatePayload GetState(uint sequenceNumber = 0)
    {
        return new StatePayload
        {
            sequenceNumber = sequenceNumber,
            position = transform.position,
            velocity = velocity,
            externalVelocity = externalVelocity,
            velocityXSmoothing = velocityXSmoothing,
            isGrounded = isGrounded,
            isClimbed = isClimbed,
            faceDir = (sbyte)controller.collisions.faceDir,
            coyoteTime = controller.CoyoteTime,
            jumpBlockTime = jumpBlockTimer,
            climbBlockTime = climbBlockTimer,
            invincibleTime = invincibleTimer,
            uncontrollableTime = uncontrollableTimer,
        };
    }

    // 서버 보정값으로 상태를 강제 복원 — Reconciliation 시 사용
    public void SetState(StatePayload state)
    {
        transform.position = state.position;
        velocity = state.velocity;
        externalVelocity = state.externalVelocity;
        velocityXSmoothing = state.velocityXSmoothing;
        isClimbed = state.isClimbed;
        jumpBlockTimer = state.jumpBlockTime;
        climbBlockTimer = state.climbBlockTime;
        invincibleTimer = state.invincibleTime;
        uncontrollableTimer = state.uncontrollableTime;
        controller.CoyoteTime = state.coyoteTime;
        controller.RestoreContacts(state.isGrounded, state.faceDir);
    }

    // 던지기·순간이동처럼 서버가 움직임을 새로 정할 때 쓴다. 조작 불가 시간 동안은 입력이 궤적을 꺾지 못한다.
    public void Launch(Vector2 launchVelocity, float uncontrollableDuration)
    {
        velocity = launchVelocity;
        externalVelocity = Vector2.zero;
        velocityXSmoothing = 0f;
        isClimbed = false;
        uncontrollableTimer = Mathf.Max(uncontrollableTimer, uncontrollableDuration);
    }

    public void SetDirectionalInput(Vector2 input, bool run)
    {
        directionalInput = input;
        isRunPressed = run;
        currentMoveSpeed = run ? runSpeed : moveSpeed;
    }

    public void BlockJump(float duration) => jumpBlockTimer = Mathf.Max(jumpBlockTimer, duration);

    public void DisableClimbTemporarily(float duration)
    {
        isClimbed = false;
        climbBlockTimer = Mathf.Max(climbBlockTimer, duration);
    }

    public void JumpHold(bool hold)
    {
        isJumpHold = hold;
    }

    public void OnJumpInputDown()
    {
        if (wallSliding)
            HandleWallJump();
        else if (isClimbed)
            HandleRopeJump();
        else if (controller.collisions.below)
            HandleGroundJump();
    }

    public void OnJumpInputUp()
    {
        // 짧게 눌렀을 때 최소 높이로 컷 — 가변 점프 높이 구현
        if (velocity.y > minJumpVelocity)
            velocity.y = minJumpVelocity;
    }

    private void CalculateMovement(float deltaTime)
    {
        float targetVelocityX = directionalInput.x * currentMoveSpeed;
        float smoothTime = 0.4f;

        if (Mathf.Sign(directionalInput.x) != Mathf.Sign(velocity.x))
            smoothTime *= 0.7f;

        if (isUncontrollable)
            smoothTime = 1f;

        // deltaTime 명시 전달 — 서버/클라이언트가 동일한 결과를 내도록 보장
        velocity.x = Mathf.SmoothDamp(velocity.x, targetVelocityX, ref velocityXSmoothing, smoothTime, Mathf.Infinity, deltaTime);
        velocity.y += gravity * deltaTime;

        if (controller.onConveyor != null)
            ConveyorAcceleration(controller.onConveyor, deltaTime);
        else
        {
            float decel = controller.collisions.below ? deltaTime * 5f : deltaTime;
            externalVelocity.x = Mathf.Lerp(externalVelocity.x, 0, decel);
        }

        if (velocity.y < -wallSlideSpeedMax && wallSliding)
            velocity.y = -wallSlideSpeedMax;

        if (velocity.y < -maxGravity)
            velocity.y = -maxGravity;
    }

    private void HandleWallJump()
    {
        if (wallDirX == (int)directionalInput.x)
        {
            velocity.x = -wallDirX * wallJumpClimb.x;
            velocity.y = wallJumpClimb.y;
        }
        else if (directionalInput.x == 0)
        {
            velocity.x = -wallDirX * wallJumpOff.x;
            velocity.y = wallJumpOff.y;
        }
        else
        {
            velocity.x = -wallDirX * wallLeap.x;
            velocity.y = wallLeap.y;
        }
        wallSliding = false;
    }

    private void HandleGroundJump()
    {
        if (!controller.CanJump()) return;

        if (controller.collisions.slidingDownMaxSlope)
        {
            if (directionalInput.x != -Mathf.Sign(controller.collisions.slopeNormal.x))
            {
                velocity.y = maxJumpVelocity * controller.collisions.slopeNormal.y;
                velocity.x = maxJumpVelocity * controller.collisions.slopeNormal.x;
            }
        }
        else
        {
            velocity.y = maxJumpVelocity;

            if (isRunPressed)
            {
                float speedRatio = Mathf.Clamp01((Mathf.Abs(velocity.x) - moveSpeed) / (runSpeed - moveSpeed));
                velocity.y += runJumpHeight * speedRatio;
            }
        }

        // 재시뮬레이션 중에는 사운드 재생 생략 — 보정 때마다 이중 재생 방지
        if (!isReconciling)
            GetComponent<PlayerSound>().PlayJumpSound();
    }

    private void HandleRopeJump()
    {
        if (isUncontrollable) return;
        velocity.y = maxJumpVelocity;
        DisableClimbTemporarily(RopeJumpClimbBlockDuration);
    }

    private void PlayerMovementInteract()
    {
        if (isClimbed)
            controller.VerticalCollisionsDetect(Vector2.down);

        NetworkIdentity steppedPlayer = controller.underPlayer;
        controller.UnderPlayerReset();
        if (steppedPlayer == null) return;

        if (isJumpBlocked) return;

        // 내 튕김은 소유 클라이언트도 바로 예측해 착지 후 RTT만큼 멈췄다 튀는 일이 없게 한다.
        // 최종 판정은 서버다. 상대를 과거 위치로 봐서 판정이 갈리면 서버 보정이 덮어쓰므로 결과는 모든 화면에서 같다.
        DisableClimbTemporarily(StompClimbBlockDuration);
        velocity.y = isJumpHold ? maxJumpVelocity * StompHoldJumpMultiplier : maxJumpVelocity;
        BlockJump(StompJumpBlockDuration);
        velocity.x += (transform.position.x - steppedPlayer.transform.position.x) * StompHorizontalPush;

        // 밟힌 쪽 연출·점프 차단은 서버만 한다. 예측이나 재시뮬레이션에서 부르면 상대 상태를 클라이언트가 바꾸게 된다.
        if (!isServer || isReconciling) return;
        steppedPlayer.GetComponent<PlayerController2D>().OnSteppedByOtherPlayer();
        serverMover.SendStateNow();
    }

    private void ConveyorAcceleration(Conveyor conveyor, float deltaTime)
    {
        if (conveyor == null) return;

        if (conveyor.isClockwise)
            externalVelocity.x -= conveyorSpeed * deltaTime * conveyorAccelerationSpeed;
        else
            externalVelocity.x += conveyorSpeed * deltaTime * conveyorAccelerationSpeed;

        externalVelocity.x = Mathf.Clamp(externalVelocity.x, -conveyorSpeed, conveyorSpeed);
    }

    private void ApplyMovement(float deltaTime)
    {
        if (isClimbed && !isUncontrollable)
            velocity = directionalInput * moveSpeed;

        velocity += externalVelocity;

        Vector2 platformDelta = controller.onMovingPlatform != null
            ? controller.onMovingPlatform.PlatformDelta
            : Vector2.zero;

        controller.Move(velocity * deltaTime + platformDelta, directionalInput);

        if (controller.collisions.above || controller.collisions.below)
        {
            if (controller.collisions.slidingDownMaxSlope)
                velocity.y += controller.collisions.slopeNormal.y * -gravity * deltaTime;
            else
                velocity.y = 0;
        }
    }

    // 무적 시간이라 피해를 받지 않았으면 false — 트랩에 닿아 있는 동안 매 틱 피격 연출이 반복되지 않게 한다
    public bool OnDamaged(Vector2 knockbackDirection)
    {
        if (isInvincible) return false;
        velocity = knockbackDirection * damagedMove;
        DisableClimbTemporarily(DamagedClimbBlockDuration);
        invincibleTimer = invincibilityDuration;
        uncontrollableTimer = Mathf.Max(uncontrollableTimer, DamagedUncontrollableDuration);
        return true;
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Rope"))
        {
            ropeContactCount++;
            return;
        }

        // 스프링·바운스는 위치만으로 정해지므로 서버와 소유 클라이언트가 같이 적용한다. 서버만 적용하면 보정이 올 때까지 화면이 늦는다.
        if (!isServer && !isOwned) return;

        if (collision.CompareTag("Bounce"))
            velocity = (transform.position - collision.transform.position).normalized * bounceForce;
        else if (collision.CompareTag("Spring"))
            velocity = (transform.position - collision.transform.position).normalized * springJumpForce;
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        if (collision.CompareTag("Rope"))
            ropeContactCount = Mathf.Max(0, ropeContactCount - 1);
    }
}
