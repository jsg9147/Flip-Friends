using Mirror;
using UnityEngine;

public class PlayerMovement : NetworkBehaviour
{
    public float moveSpeed = 4f;
    public float runMultiplier = 1.5f;
    public float accelerationRate = 10.0f;

    private Rigidbody2D rb;
    private bool isGround = false;
    private bool isRunning = false;

    // Jump
    public float jumpForce = 10f;
    public float jumpHoldTime = 0.3f;
    private float jumpTime = 0f;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    public void HandleMovement(Vector2 inputDirection, bool isRun)
    {
        float moveInput = inputDirection.x;
        isRunning = isRun;

        float currentSpeed = isRunning ? moveSpeed * runMultiplier : moveSpeed;
        float maxSpeed = currentSpeed;

        if (Mathf.Abs(moveInput) > 0.01f)
        {
            float targetSpeed = moveInput * currentSpeed;
            float speedDifference = targetSpeed - rb.linearVelocityX;
            float appliedForce = Mathf.Clamp(speedDifference * accelerationRate, -maxSpeed, maxSpeed) * 5f;

            if (!isGround && Mathf.Sign(rb.linearVelocityX) != Mathf.Sign(appliedForce))
            {
                appliedForce *= 0.8f;
            }

            rb.linearVelocity = new Vector2(rb.linearVelocityX + appliedForce * Time.deltaTime, rb.linearVelocityY);
        }
        Vector2 clampedVelocity = rb.linearVelocity;
        clampedVelocity.x = Mathf.Clamp(clampedVelocity.x, -maxSpeed, maxSpeed);
        rb.linearVelocity = clampedVelocity;

    }

    public void HandleJump()
    {
        if (isGround)
        {
            // 점프 초기화 및 힘 계산
            float runJumpMultiplier = Mathf.Max((Mathf.Abs(rb.linearVelocity.x) / moveSpeed) * 0.8f, 1f);
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce * runJumpMultiplier);
            jumpTime = 0f; // 점프 시간 초기화
        }
    }

    public void HandleRopeJump(Vector2 inputDirection)
    {
        // 점프 초기화 및 힘 계산
        rb.linearVelocity = new Vector2(moveSpeed * inputDirection.x, jumpForce);
        jumpTime = 0f; // 점프 시간 초기화
    }

    public void HoldJump()
    {
        if (!isGround)
        {
            if (jumpTime < jumpHoldTime)
            {
                // 자연스럽게 감속하는 상승 구현
                float jumpStrength = Mathf.SmoothStep(0.1f, 0.02f, jumpTime / jumpHoldTime); // 시작할 때 강하고 점차 약해짐
                rb.linearVelocity += (Vector2.up * jumpStrength);
                jumpTime += Time.deltaTime;
            }
            else
            {
                jumpTime = jumpHoldTime;
            }
        }
    }

    public void SetJumpingState(bool isGround) => this.isGround = isGround;
}
