using System.Collections;
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
    private bool invincible;
    private bool uncontrollable;
    private float currentMoveSpeed;

    public bool isClimbed { get; private set; }
    private bool climbBlock;
    private bool jumpBlock;
    private bool isJumpHold;

    public bool isGrounded => controller.collisions.below;
    public Vector2 CurrentVelocity => velocity;

    private void Awake()
    {
        Initialize();
    }

    private void FixedUpdate()
    {
        // isOwned 플레이어는 ClientMover가 로컬에서 담당 — 서버에서도 중복 실행 방지
        if (!isServer || isOwned) return;
        Simulate(Time.fixedDeltaTime);
    }

    private void Initialize()
    {
        controller = GetComponent<Controller2D>();
        gravity = -((2 * maxJumpHeight) / Mathf.Pow(timeToJumpApex, 2)) * 0.9f;
        maxJumpVelocity = Mathf.Abs(gravity) * timeToJumpApex;
        minJumpVelocity = Mathf.Sqrt(2 * Mathf.Abs(gravity) * minJumpHeight);
        currentMoveSpeed = moveSpeed;
    }

    // 한 물리 스텝을 실행 — ClientMover와 ServerMover가 동일한 코드를 공유하기 위해 분리
    public void Simulate(float deltaTime)
    {
        CalculateMovement(deltaTime);
        PlayerMovementInteract();
        ApplyMovement(deltaTime);
    }

    // InputPayload를 받아 내부 입력 상태를 설정한 뒤 시뮬레이션
    public void ApplyInput(InputPayload input)
    {
        SetDirectionalInput(input.movement, input.run);
        JumpHold(input.jumpHeld);

        if (input.jump) OnJumpInputDown();
        if (input.jumpUp) OnJumpInputUp();
    }

    // 현재 물리 상태를 StatePayload로 반환 — 서버가 클라이언트에게 보낼 때 사용
    public StatePayload GetState(uint sequenceNumber = 0)
    {
        return new StatePayload
        {
            sequenceNumber = sequenceNumber,
            position = transform.position,
            velocity = velocity,
            isGrounded = isGrounded,
        };
    }

    // 서버 보정값으로 상태를 강제 복원 — Reconciliation 시 사용
    public void SetState(StatePayload state)
    {
        transform.position = state.position;
        velocity = state.velocity;
    }

    public void SetDirectionalInput(Vector2 input, bool run)
    {
        directionalInput = input;
        isRunPressed = run;
        currentMoveSpeed = run ? runSpeed : moveSpeed;
    }

    public void SetClimbState(bool isClimb)
    {
        if (climbBlock) return;
        isClimbed = isClimb;
    }

    public void BlockJump(float delay) => StartCoroutine(EnableJumpAfterDelay(delay));

    public void DisableClimbTemporarily(float duration)
    {
        SetClimbState(false);
        StartCoroutine(EnableClimbAfterDelay(duration));
    }

    private IEnumerator EnableClimbAfterDelay(float duration)
    {
        climbBlock = true;
        yield return new WaitForSeconds(duration);
        climbBlock = false;
    }

    private IEnumerator EnableJumpAfterDelay(float duration)
    {
        jumpBlock = true;
        yield return new WaitForSeconds(duration);
        jumpBlock = false;
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

        if (uncontrollable)
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

        GetComponent<PlayerSound>().RpcPlayJumpSound();
    }

    private void HandleRopeJump()
    {
        if (uncontrollable) return;
        velocity.y = maxJumpVelocity;
        DisableClimbTemporarily(0.3f);
    }

    private void PlayerMovementInteract()
    {
        if (isClimbed)
            controller.VerticalCollisionsDetect(Vector2.down);

        if (controller.underPlayer != null && !jumpBlock)
        {
            controller.underPlayer.GetComponent<PlayerController2D>().OnSteppedByOtherPlayer();
            DisableClimbTemporarily(0.3f);
            velocity.y = isJumpHold ? maxJumpVelocity * 1.2f : maxJumpVelocity;
            BlockJump(0.3f);
            velocity.x += (transform.position.x - controller.underPlayer.transform.position.x) * 0.5f;
            controller.UnderPlayerReset();
        }
    }

    private void BounceMovement(Vector3 targetPosition)
    {
        velocity = (transform.position - targetPosition).normalized * bounceForce;
    }

    private void SpringJump(Vector3 targetPosition)
    {
        velocity = (transform.position - targetPosition).normalized * bounceForce;
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
        if (isClimbed && !uncontrollable)
            velocity = directionalInput * moveSpeed;

        velocity += externalVelocity;
        controller.Move(velocity * deltaTime, directionalInput);

        if (controller.collisions.above || controller.collisions.below)
        {
            if (controller.collisions.slidingDownMaxSlope)
                velocity.y += controller.collisions.slopeNormal.y * -gravity * deltaTime;
            else
                velocity.y = 0;
        }
    }

    public void OnDamaged(Vector2 knockbackDirection)
    {
        if (invincible) return;
        velocity = knockbackDirection * damagedMove;
        DisableClimbTemporarily(1f);
        StartCoroutine(ActivateInvincibility());
        StartCoroutine(TemporaryUncontrollable(1f));
    }

    private IEnumerator ActivateInvincibility()
    {
        invincible = true;
        yield return new WaitForSeconds(invincibilityDuration);
        invincible = false;
    }

    private IEnumerator TemporaryUncontrollable(float duration)
    {
        uncontrollable = true;
        yield return new WaitForSeconds(duration);
        uncontrollable = false;
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (!isServer) return;

        if (collision.CompareTag("Reset"))
            RpcVelocityReset();
        else if (collision.CompareTag("Bounce"))
            BounceMovement(collision.transform.position);
        else if (collision.CompareTag("Spring"))
            SpringJump(collision.transform.position);
    }

    [ClientRpc]
    public void RpcVelocityReset()
    {
        velocity = Vector2.zero;
    }
}
