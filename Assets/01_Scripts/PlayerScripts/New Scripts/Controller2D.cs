using Mirror;
using System;
using UnityEngine;

public class Controller2D : RaycastController
{
    public float maxSlopeAngle = 45f;
    public CollisionInfo collisions;
    private Vector2 playerInput;

    private GameObject heldObj;
    public bool isHold => heldObj != null;

    public NetworkIdentity underPlayer { get; private set; }

    private Vector2 movementVector;

    public override void Start()
    {
        base.Start();
        collisions.faceDir = 1;
    }

    public void Move(Vector2 moveAmount, Vector2 input, bool standingOnPlatform = false)
    {
        UpdateRaycastOrigins();
        collisions.Reset();
        collisions.moveAmountOld = moveAmount;
        playerInput = input;

        if (moveAmount.y < 0)
            DescendSlope(ref moveAmount);

        if (moveAmount.x != 0)
            collisions.faceDir = (int)Mathf.Sign(moveAmount.x);

        ProcessCollisions(ref moveAmount);

        movementVector = moveAmount;
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
        float directionX = collisions.faceDir;
        float rayLength = Mathf.Abs(moveAmount.x) + skinWidth;
        int rayCount = isHold ? horizontalRayCount * 2 : horizontalRayCount;

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
        if(isHold)
            rayOrigin = (directionY == -1) ? raycastOrigins.bottomLeft : holdObjectRaycast.topLeft;
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

            if (hit.transform != transform && hit.collider.CompareTag("Player"))
            {
                // 네트워크 객체의 NetId를 가져오기
                NetworkIdentity networkIdentity = hit.transform.GetComponent<NetworkIdentity>();

                if (networkIdentity != null)
                {
                    underPlayer = networkIdentity;
                }
            }
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

    private void AdjustHorizontalMovement(ref Vector2 moveAmount, float hitDistance, float directionX, float slopeAngle)
    {
        moveAmount.x = (hitDistance - skinWidth) * directionX;

        if (collisions.climbingSlope)
            moveAmount.y = Mathf.Tan(collisions.slopeAngle * Mathf.Deg2Rad) * Mathf.Abs(moveAmount.x);

        collisions.left = directionX == -1;
        collisions.right = directionX == 1;
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

        if (hit.collider.gameObject == heldObj)
            return false;

        return true;
    }

    public void SetHoldObj(GameObject holdObj) => heldObj = holdObj;
    public void HoldReset() => heldObj = null;

    public void UnderPlayerReset() => underPlayer = null;

    public struct CollisionInfo
    {
        public bool above, below, left, right;
        public bool climbingSlope, descendingSlope, slidingDownMaxSlope;
        public float slopeAngle, slopeAngleOld;
        public Vector2 slopeNormal, moveAmountOld;
        public int faceDir;

        public void Reset()
        {
            above = below = left = right = false;
            climbingSlope = descendingSlope = slidingDownMaxSlope = false;
            slopeNormal = Vector2.zero;
            slopeAngleOld = slopeAngle;
            slopeAngle = 0;
        }
    }
}
