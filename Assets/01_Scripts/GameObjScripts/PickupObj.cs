using UnityEngine;
using Mirror;
using static Controller2D;
using UnityEngine.InputSystem.XR;

public class PickupObj : RaycastController
{
    public float maxSlopeAngle = 45f;
    public float gravity = 9.8f;
    public CollisionInfo collisions;

    [SyncVar] private bool isCarried = false;
    public bool IsCarried => isCarried;

    private bool wallSliding;

    [SerializeField] private Vector2 velocity;
    private Transform playerTransform;

    private float velocityXSmoothing;

    public float wallSlideSpeedMax = 3f;
    private float maxGravity = 12f;

    private float timeToWallUnstick;
    public override void Start()
    {
        base.Start();
        velocity = Vector2.zero;
        collisions.faceDir = 1;
    }

    private void Update()
    {
        if (isServer)
        {
            UpdateRaycastOrigins();
            collisions.Reset();
        }
    }

    [SyncVar]
    private Vector2 syncedPosition;

    private void FixedUpdate()
    {
        if (isServer)
        {
            if (!isCarried)
            {
                CalculateMovement();
                ApplyMovement();
            }

            syncedPosition = transform.position;
        }
        else
        {
            transform.position = syncedPosition;
        }
    }

    private void CalculateMovement()
    {
        float smoothTime = 1.5f;

        velocity.x = Mathf.SmoothDamp(velocity.x, 0f, ref velocityXSmoothing, smoothTime);
        velocity.y += -gravity * Time.deltaTime;

        if (velocity.y < -wallSlideSpeedMax && wallSliding)
            velocity.y = -wallSlideSpeedMax;

        if (velocity.y < -maxGravity)
            velocity.y = -maxGravity;
    }
    private void ApplyMovement()
    {
        Move(velocity * Time.deltaTime);
        if (collisions.above || collisions.below)
        {
            if (collisions.slidingDownMaxSlope)
            {
                velocity.y += collisions.slopeNormal.y * -gravity * Time.deltaTime;
            }
            else
            {
                velocity.y = 0;
            }
        }
    }

    [ClientRpc]
    public void RpcApplyVelocity(Vector2 playerVelocity)
    {
        this.velocity = playerVelocity;
    }

    public void Move(Vector2 moveAmount, bool standingOnPlatform = false)
    {
        UpdateRaycastOrigins();
        collisions.Reset();
        collisions.moveAmountOld = moveAmount;

        if (moveAmount.y < 0)
            DescendSlope(ref moveAmount);

        if (moveAmount.x != 0)
            collisions.faceDir = (int)Mathf.Sign(moveAmount.x);

        ProcessCollisions(ref moveAmount);

        transform.Translate(moveAmount);

        if (standingOnPlatform)
            collisions.below = true;
    }

    private void ProcessCollisions(ref Vector2 moveAmount)
    {
        HorizontalCollisions(ref moveAmount);
        VerticalCollisions(ref moveAmount);
    }

    private void HorizontalCollisions(ref Vector2 moveAmount)
    {
        float directionX = (Mathf.Abs(moveAmount.x) > 0f) ? Mathf.Sign(moveAmount.x) : 0f;
        float rayLength = Mathf.Abs(moveAmount.x) + skinWidth;
        int rayCount = horizontalRayCount;

        if (Mathf.Abs(moveAmount.x) < skinWidth)
            rayLength = 2 * skinWidth;

        for (int i = 0; i < rayCount; i++)
        {
            Vector2 rayOrigin = GetHorizontalRayOrigin(directionX, i);
            RaycastHit2D[] hits = Physics2D.RaycastAll(rayOrigin, Vector2.right * directionX, rayLength, collisionMask);

            Debug.DrawRay(rayOrigin, Vector2.right * directionX * rayLength, Color.red);
            ProcessHorizontalHits(hits, ref moveAmount, directionX, i);
        }
    }
    private Vector2 GetHorizontalRayOrigin(float directionX, int i)
    {
        Vector2 rayOrigin = (directionX == -1) ? raycastOrigins.bottomLeft : raycastOrigins.bottomRight;

        rayOrigin += Vector2.up * (horizontalRaySpacing * i);

        return rayOrigin;
    }
    private void ProcessHorizontalHits(RaycastHit2D[] hits, ref Vector2 moveAmount, float directionX, int rayIndex)
    {
        foreach (var hit in hits)
        {
            if (!IsValidHit(hit, moveAmount)) continue;

            float slopeAngle = Vector2.Angle(hit.normal, Vector2.up);

            if (rayIndex == 0 && slopeAngle <= maxSlopeAngle)
            {
                HandleSlopeClimbing(ref moveAmount, slopeAngle, hit.normal, hit.distance, directionX);
                break;
            }

            if (!collisions.climbingSlope || slopeAngle > maxSlopeAngle)
            {
                AdjustHorizontalMovement(ref moveAmount, hit.distance, directionX, slopeAngle);
            }
        }
    }
    private void HandleSlopeClimbing(ref Vector2 moveAmount, float slopeAngle, Vector2 slopeNormal, float hitDistance, float directionX)
    {
        if (collisions.descendingSlope)
        {
            collisions.descendingSlope = false;
            moveAmount = collisions.moveAmountOld;
        }

        float distanceToSlopeStart = hitDistance - skinWidth;
        moveAmount.x -= distanceToSlopeStart * directionX;
        ClimbSlope(ref moveAmount, slopeAngle, slopeNormal);
        moveAmount.x += distanceToSlopeStart * directionX;
    }
    private void ClimbSlope(ref Vector2 moveAmount, float slopeAngle, Vector2 slopeNormal)
    {
        float moveDistance = Mathf.Abs(moveAmount.x);
        float climbmoveAmountY = Mathf.Sin(slopeAngle * Mathf.Deg2Rad) * moveDistance;

        if (moveAmount.y <= climbmoveAmountY)
        {
            moveAmount.y = climbmoveAmountY;
            moveAmount.x = Mathf.Cos(slopeAngle * Mathf.Deg2Rad) * moveDistance * Mathf.Sign(moveAmount.x);

            collisions.below = true;
            collisions.climbingSlope = true;
            collisions.slopeAngle = slopeAngle;
            collisions.slopeNormal = slopeNormal;
        }
    }

    private void AdjustHorizontalMovement(ref Vector2 moveAmount, float hitDistance, float directionX, float slopeAngle)
    {
        moveAmount.x = (hitDistance - skinWidth) * directionX;

        if (moveAmount.x < 0.02)
            moveAmount.x = 0;

        if (Mathf.Sign(moveAmount.x) == directionX && directionX == Mathf.Sign(velocity.x) && Mathf.Abs(moveAmount.y) > 0.05f)
        {
            velocity.x = 0;
            moveAmount.x = 0f;
        }

        if (collisions.climbingSlope)
            moveAmount.y = Mathf.Tan(collisions.slopeAngle * Mathf.Deg2Rad) * Mathf.Abs(moveAmount.x);

        collisions.left = directionX == -1;
        collisions.right = directionX == 1;
    }
    private void VerticalCollisions(ref Vector2 moveAmount)
    {
        float directionY = Mathf.Sign(moveAmount.y);
        float rayLength = Mathf.Abs(moveAmount.y) + skinWidth;

        for (int i = 0; i < verticalRayCount; i++)
        {
            Vector2 rayOrigin = GetVerticalRayOrigin(directionY, moveAmount.x, i);
            RaycastHit2D[] hits = Physics2D.RaycastAll(rayOrigin, Vector2.up * directionY, rayLength, collisionMask);

            Debug.DrawRay(rayOrigin, Vector2.up * directionY * rayLength, Color.red);
            ProcessVerticalHits(hits, ref moveAmount, directionY);
        }

        if (collisions.climbingSlope)
            AdjustSlopeMovement(ref moveAmount);
    }
    private Vector2 GetVerticalRayOrigin(float directionY, float moveAmountX, int i)
    {
        Vector2 rayOrigin = (directionY == -1) ? raycastOrigins.bottomLeft : raycastOrigins.topLeft;
        rayOrigin += Vector2.right * (verticalRaySpacing * i + moveAmountX);
        return rayOrigin;
    }

    private void ProcessVerticalHits(RaycastHit2D[] hits, ref Vector2 moveAmount, float directionY)
    {
        foreach (var hit in hits)
        {
            if (!IsValidHit(hit, moveAmount)) continue;

            moveAmount.y = (hit.distance - skinWidth) * directionY;

            if (collisions.climbingSlope)
                moveAmount.x = moveAmount.y / Mathf.Tan(collisions.slopeAngle * Mathf.Deg2Rad) * Mathf.Sign(moveAmount.x);

            collisions.below = directionY == -1;
            collisions.above = directionY == 1;
        }
    }

    private void AdjustSlopeMovement(ref Vector2 moveAmount)
    {
        float directionX = Mathf.Sign(moveAmount.x);
        float rayLength = Mathf.Abs(moveAmount.x) + skinWidth;

        Vector2 rayOrigin = (directionX == -1) ? raycastOrigins.bottomLeft : raycastOrigins.bottomRight;
        rayOrigin += Vector2.up * moveAmount.y;

        RaycastHit2D[] hits = Physics2D.RaycastAll(rayOrigin, Vector2.right * directionX, rayLength, collisionMask);

        foreach (var hit in hits)
        {
            if (!IsValidHit(hit, moveAmount)) continue;

            float slopeAngle = Vector2.Angle(hit.normal, Vector2.up);

            if (slopeAngle != collisions.slopeAngle)
            {
                moveAmount.x = (hit.distance - skinWidth) * directionX;
                collisions.slopeAngle = slopeAngle;
                collisions.slopeNormal = hit.normal;
            }
        }
    }

    private void DescendSlope(ref Vector2 moveAmount)
    {
        RaycastHit2D maxSlopeHitLeft = Physics2D.Raycast(raycastOrigins.bottomLeft, Vector2.down, Mathf.Abs(moveAmount.y) + skinWidth, collisionMask);
        RaycastHit2D maxSlopeHitRight = Physics2D.Raycast(raycastOrigins.bottomRight, Vector2.down, Mathf.Abs(moveAmount.y) + skinWidth, collisionMask);

        if (maxSlopeHitLeft ^ maxSlopeHitRight)
        {
            SlideDownMaxSlope(maxSlopeHitLeft, ref moveAmount);
            SlideDownMaxSlope(maxSlopeHitRight, ref moveAmount);
        }

        if (!collisions.slidingDownMaxSlope)
        {
            float directionX = Mathf.Sign(moveAmount.x);
            Vector2 rayOrigin = (directionX == -1) ? raycastOrigins.bottomRight : raycastOrigins.bottomLeft;
            RaycastHit2D hit = Physics2D.Raycast(rayOrigin, -Vector2.up, Mathf.Infinity, collisionMask);

            if (hit)
            {
                float slopeAngle = Vector2.Angle(hit.normal, Vector2.up);
                if (slopeAngle != 0 && slopeAngle <= maxSlopeAngle)
                {
                    if (Mathf.Sign(hit.normal.x) == directionX)
                    {
                        if (hit.distance - skinWidth <= Mathf.Tan(slopeAngle * Mathf.Deg2Rad) * Mathf.Abs(moveAmount.x))
                        {
                            float moveDistance = Mathf.Abs(moveAmount.x);
                            float descendmoveAmountY = Mathf.Sin(slopeAngle * Mathf.Deg2Rad) * moveDistance;
                            moveAmount.x = Mathf.Cos(slopeAngle * Mathf.Deg2Rad) * moveDistance * Mathf.Sign(moveAmount.x);
                            moveAmount.y -= descendmoveAmountY;

                            collisions.slopeAngle = slopeAngle;
                            collisions.descendingSlope = true;
                            collisions.below = true;
                            collisions.slopeNormal = hit.normal;
                        }
                    }
                }
            }
        }
    }

    private void SlideDownMaxSlope(RaycastHit2D hit, ref Vector2 moveAmount)
    {
        if (hit)
        {
            float slopeAngle = Vector2.Angle(hit.normal, Vector2.up);
            if (slopeAngle > maxSlopeAngle)
            {
                moveAmount.x = Mathf.Sign(hit.normal.x) * (Mathf.Abs(moveAmount.y) - hit.distance) / Mathf.Tan(slopeAngle * Mathf.Deg2Rad);

                collisions.slopeAngle = slopeAngle;
                collisions.slidingDownMaxSlope = true;
                collisions.slopeNormal = hit.normal;
            }
        }
    }

    private bool IsValidHit(RaycastHit2D hit, Vector2 moveAmount)
    {
        if (hit.collider == null || hit.collider == GetComponent<Collider2D>())
            return false;

        if (hit.collider.CompareTag("Through") && Mathf.Sign(moveAmount.y) == 1)
            return false;

        return true;
    }

    public void SetPickupState(Transform player, bool isCarried)
    {
        playerTransform = player;
        this.isCarried = isCarried;
        if (isServer)
            RpcSetVisible(!isCarried);
    }

    public void StateReset()
    {
        playerTransform = null;
        isCarried = false;
        if (isServer)
            RpcSetVisible(true);
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Reset"))
        {
            if (collision.GetComponent<RespawnHandler>() != null)
            {
                RpcResetPosition(collision.GetComponent<RespawnHandler>().resetPoint.position);
            }
        }
    }

    [ClientRpc]
    private void RpcResetPosition(Vector3 resetPos)
    {
        velocity = Vector3.zero;
        transform.position = resetPos;
    }

    [ClientRpc]
    public void RpcSetVisible(bool isVisible)
    {
        GetComponent<SpriteRenderer>().enabled = isVisible;
        GetComponent<Collider2D>().enabled = isVisible;
    }
}
