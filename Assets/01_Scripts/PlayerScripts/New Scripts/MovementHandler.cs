using System.Collections;
using UnityEngine;
using Mirror;

[RequireComponent(typeof(Controller2D))]
public class MovementHandler : NetworkBehaviour
{
    [Header("Movement Settings")]
    public float maxJumpHeight;
    public float minJumpHeight;
    public float runJumpHeight;
    public float timeToJumpApex;
    public float moveSpeed;
    public float runSpeed;

    [Header("Wall Interaction")]
    public Vector2 wallJumpClimb;
    public Vector2 wallJumpOff;
    public Vector2 wallLeap;
    public float wallSlideSpeedMax = 3f;
    public float wallStickTime = 0.25f;
    public float wallSlideTime;

    [Header("Damage")]
    public Vector2 damagedMove;
    public float invincibilityDuration = 1f;

    private float gravity;
    private float maxGravity = 15f;
    private float maxJumpVelocity;
    private float minJumpVelocity;
    private float velocityXSmoothing;
    private float timeToWallUnstick;

    private Vector2 velocity;
    private Vector2 directionalInput;
    private bool isRunPressed;
    private bool wallSliding;
    private int wallDirX;

    private Controller2D controller;
    private bool invincible;
    private bool uncontrollable;
    private float currentMoveSpeed;

    private bool isClimbed;
    public bool isGrounded => controller.collisions.below;
    public Vector2 CurrentVelocity => velocity;

    private void Start()
    {
        Initialize();
    }

    private void FixedUpdate()
    {
        if (!isOwned) return;

        CalculateMovement();

        PlayerMovementInteract();
        ApplyMovement();
    }

    private void Initialize()
    {
        controller = GetComponent<Controller2D>();
        gravity = -(2 * maxJumpHeight) / Mathf.Pow(timeToJumpApex, 2);
        maxJumpVelocity = Mathf.Abs(gravity) * timeToJumpApex;
        minJumpVelocity = Mathf.Sqrt(2 * Mathf.Abs(gravity) * minJumpHeight);
        currentMoveSpeed = moveSpeed;
    }

    public void SetDirectionalInput(Vector2 input, bool isRunPressed)
    {
        directionalInput = input;
        this.isRunPressed = isRunPressed;
        currentMoveSpeed = isRunPressed ? runSpeed : moveSpeed;
    }
    public void SetClimbState(bool isClimb) => this.isClimbed = isClimb;

    public void OnJumpInputDown()
    {
        if (wallSliding)
        {
            HandleWallJump();
        }
        else if(isClimbed)
        {
            HandleRopeJump();
        }
        else if (controller.collisions.below)
        {
            HandleGroundJump();
        }
    }

    public void OnJumpInputUp()
    {
        if (velocity.y > minJumpVelocity)
            velocity.y = minJumpVelocity;
    }

    private void CalculateMovement()
    {
        if (wallSliding)
            HandleWallSliding();

        float targetVelocityX = directionalInput.x * currentMoveSpeed;
        float smoothTime = controller.collisions.below ? 0.4f : 0.8f;
        if (Mathf.Sign(directionalInput.x) != Mathf.Sign(velocity.x))
        {
            smoothTime = smoothTime * 0.8f;
        }

        if (uncontrollable)
        {
            smoothTime = 1f;
        }

        velocity.x = Mathf.SmoothDamp(velocity.x, targetVelocityX, ref velocityXSmoothing, smoothTime);
        velocity.y += gravity * Time.deltaTime;

        if (velocity.y < -wallSlideSpeedMax && wallSliding)
            velocity.y = -wallSlideSpeedMax;

        if (velocity.y < -maxGravity)
            velocity.y = -maxGravity;
    }

    private void HandleWallSliding()
    {
        wallDirX = controller.collisions.left ? -1 : 1;
        if ((controller.collisions.left || controller.collisions.right) && !controller.collisions.below && velocity.y < 0)
        {
            wallSliding = true;
            timeToWallUnstick = wallStickTime;

            if (directionalInput.x != wallDirX && directionalInput.x != 0)
                timeToWallUnstick -= Time.deltaTime;
        }
        else
        {
            wallSliding = false;
        }
    }

    private void HandleWallJump()
    {
        if (wallDirX == directionalInput.x)
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
    }

    private void HandleRopeJump()
    {
        velocity.y = maxJumpVelocity;
        isClimbed = false;
    }

    private void PlayerMovementInteract()
    {
        if (controller.underPlayer != null)
        {
            CmdPlayerInteractEffect(controller.underPlayer.netId);
            //점프 구현
            velocity.y = maxJumpVelocity;
            controller.UnderPlayerReset();
        }
    }

    [Command]

    private void CmdPlayerInteractEffect(uint targetNetId)
    {
        NetworkIdentity targetIdentity = NetworkServer.spawned[targetNetId];
        if (targetIdentity != null)
        {
            // 해당 객체의 함수를 호출
            targetIdentity.GetComponent<PlayerController2D>().RpcTargetFunction();
        }
    }

    private void ApplyMovement()
    {
        if (isClimbed)
        {
            velocity = directionalInput * moveSpeed;
        }

        controller.Move(velocity * Time.deltaTime, directionalInput);

        if (controller.collisions.above || controller.collisions.below)
        {
            if (controller.collisions.slidingDownMaxSlope)
            {
                velocity.y += controller.collisions.slopeNormal.y * -gravity * Time.deltaTime;
            }
            else
            {
                velocity.y = 0;
            }
        }
    }


    public void OnDamaged(Vector2 knockbackDirection)
    {
        if (invincible) return;

        SetClimbState(false);
        velocity = knockbackDirection * damagedMove;
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
}
