using Mirror;
using System;
using Unity.VisualScripting;
using UnityEngine;

public class Controller2D : RaycastController
{
    public float maxSlopeAngle = 45f;
    public float coyoteTimeDuration = 0.2f; // �ڿ��� Ÿ�� ���� �ð�
    private float coyoteTimeCounter = 0f;
    [NonSerialized] public CollisionInfo collisions;
    public Collider2D objCollider;

    private GameObject heldObj;
    public bool isHold => heldObj != null;

    public Conveyor onConveyor;
    public MovingPlatform onMovingPlatform { get; private set; }

    public NetworkIdentity underPlayer { get; private set; }
    private Vector2 movementVector;

    private const int MaxPenetrationPasses = 3;

    public float CoyoteTime
    {
        get => coyoteTimeCounter;
        set => coyoteTimeCounter = value;
    }

    public override void Start()
    {
        base.Start();
        collisions.faceDir = 1;
    }

    // 재시뮬레이션처럼 한 프레임에 Move를 여러 번 부르면 boxCollider.bounds가 이전 위치를 가리켜 레이가 벽을 뚫는다.
    protected override Bounds GetColliderBounds() => ComputeBoundsFromTransform();

    // 다른 플레이어·한 방향 발판·들고 있는 물체와 겹치는 건 정상이므로 밀어내지 않는다.
    protected override bool IsBlocker(Collider2D hit)
    {
        if (!base.IsBlocker(hit) || hit.CompareTag("Player")) return false;
        if (objCollider != null && hit.gameObject == objCollider.gameObject) return false;
        return heldObj == null || hit.gameObject != heldObj;
    }

    // 레이는 콜라이더 밖에서 출발해야 벽을 본다. Ground는 Outline 콜라이더라 안에서 쏜 레이는 아무것도 맞히지 않으므로,
    // 순간이동·보정·내려놓기로 벽에 박힌 상태면 이동 전에 가장 가까운 면으로 밀어낸다.
    public void ResolvePenetration()
    {
        for (int pass = 0; pass < MaxPenetrationPasses; pass++)
        {
            Collider2D blocker = FindBlockingCollider(transform.position, overlapTolerance);
            if (blocker == null) return;

            // 자신의 물리 형상은 마지막 물리 스텝 위치에 있으므로 거리 계산 전에 맞춘다. 겹쳤을 때만 부르므로 평소 비용은 없다.
            Physics2D.SyncTransforms();
            ColliderDistance2D distance = boxCollider.Distance(blocker);
            // 바닥에 선 상태도 접촉 여유 반경 때문에 -skinWidth 정도로 나온다. 이런 얕은 겹침은 레이가 처리한다.
            // 여기서 밀면 매 틱 들썩여 접지 판정이 깜빡이고 점프 입력이 씹힌다.
            if (!distance.isValid || distance.distance > -overlapTolerance) return;

            transform.position += (Vector3)(distance.pointB - distance.pointA);
        }
    }

    // 보정으로 위치를 되돌린 뒤, 다음 틱이 쓰는 발밑 정보(컨베이어·움직이는 발판)를 새 위치에서 다시 구한다.
    public void RestoreContacts(bool isGrounded, int faceDir)
    {
        collisions.Reset();
        collisions.faceDir = faceDir == 0 ? 1 : faceDir;
        UpdateRaycastOrigins();
        Vector2 probe = new Vector2(0f, -skinWidth);
        VerticalCollisions(ref probe);
        collisions.below = isGrounded;
        underPlayer = null;
    }

    public void Move(Vector2 moveAmount, bool standingOnPlatform)
    {
        Move(moveAmount, Vector2.zero, standingOnPlatform);
    }

    public void Move(Vector2 moveAmount, Vector2 input, bool standingOnPlatform = false)
    {
        ResolvePenetration();
        UpdateRaycastOrigins();
        collisions.Reset();
        collisions.moveAmountOld = moveAmount;

        if (moveAmount.y < 0)
            DescendSlope(ref moveAmount);

        if (moveAmount.x != 0)
            collisions.faceDir = (int)Mathf.Sign(moveAmount.x);

        if(!standingOnPlatform)
            ProcessCollisions(ref moveAmount);

        movementVector = moveAmount;
        transform.Translate(moveAmount);

        if (standingOnPlatform)
            collisions.below = true;

        // �ڿ��� Ÿ�� ī���� ������Ʈ
        if (collisions.below)
        {
            coyoteTimeCounter = coyoteTimeDuration;
        }
        else
        {
            coyoteTimeCounter -= Time.deltaTime;
        }
    }
    public bool CanJump()
    {
        // �ڿ��� Ÿ�� ���� �ִ��� Ȯ��
        return collisions.below || coyoteTimeCounter > 0f;
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
            ProcessHorizontalHits(hits, ref moveAmount, ref rayLength, directionX, i);
        }
    }

    private Vector2 GetHorizontalRayOrigin(float directionX, int i)
    {
        Vector2 rayOrigin = (directionX == -1) ? raycastOrigins.bottomLeft : raycastOrigins.bottomRight;

        rayOrigin += Vector2.up * (horizontalRaySpacing * i);

        return rayOrigin;
    }

    // 레이 길이를 가장 가까운 충돌 거리로 줄여 간다. 줄이지 않으면 더 먼 충돌이 이동량을 덮어써 앞의 벽을 통과한다.
    private void ProcessHorizontalHits(RaycastHit2D[] hits, ref Vector2 moveAmount, ref float rayLength, float directionX, int rayIndex)
    {
        foreach (var hit in hits)
        {
            if (!IsValidHit(hit, moveAmount)) continue;
            if (hit.distance > rayLength) break;

            float slopeAngle = Vector2.Angle(hit.normal, Vector2.up);

            if (rayIndex == 0 && slopeAngle <= maxSlopeAngle)
            {
                HandleSlopeClimbing(ref moveAmount, slopeAngle, hit.normal, hit.distance, directionX);
                break;
            }

            if (!collisions.climbingSlope || slopeAngle > maxSlopeAngle)
            {
                AdjustHorizontalMovement(ref moveAmount, hit.distance, directionX, slopeAngle);
                rayLength = hit.distance;
            }
        }
    }

    private void VerticalCollisions(ref Vector2 moveAmount)
    {
        float directionY = Mathf.Sign(moveAmount.y);
        float rayLength = Mathf.Abs(moveAmount.y) + skinWidth;

        onConveyor = null;
        onMovingPlatform = null;

        for (int i = 0; i < verticalRayCount; i++)
        {
            Vector2 rayOrigin = GetVerticalRayOrigin(directionY, moveAmount.x, i);
            RaycastHit2D[] hits = Physics2D.RaycastAll(rayOrigin, Vector2.up * directionY, rayLength, collisionMask);

            Debug.DrawRay(rayOrigin, Vector2.up * directionY * rayLength, Color.red);
            ProcessVerticalHits(hits, ref moveAmount, ref rayLength, directionY);
        }

        if (collisions.climbingSlope)
            AdjustSlopeMovement(ref moveAmount);
    }

    public void VerticalCollisionsDetect(Vector2 dir)
    {
        float directionY = Mathf.Sign(dir.y);
        float rayLength = skinWidth + 0.1f;

        for (int i = 0; i < verticalRayCount; i++)
        {
            Vector2 rayOrigin = GetVerticalRayOrigin(directionY, movementVector.x, i);
            RaycastHit2D[] hits = Physics2D.RaycastAll(rayOrigin, Vector2.up * directionY, rayLength, collisionMask);

            Debug.DrawRay(rayOrigin, Vector2.up * directionY * rayLength, Color.red);
            ProcessVerticalHitsPlayer(hits, dir, directionY);
        }
    }

    private Vector2 GetVerticalRayOrigin(float directionY, float moveAmountX, int i)
    {
        Vector2 rayOrigin = (directionY == -1) ? raycastOrigins.bottomLeft : raycastOrigins.topLeft;
        if(isHold)
            rayOrigin = (directionY == -1) ? raycastOrigins.bottomLeft : holdObjectRaycast.topLeft;
        rayOrigin += Vector2.right * (verticalRaySpacing * i + moveAmountX);
        return rayOrigin;
    }

    private void ProcessVerticalHits(RaycastHit2D[] hits, ref Vector2 moveAmount, ref float rayLength, float directionY)
    {
        foreach (var hit in hits)
        {
            if (!IsValidHit(hit, moveAmount, true)) continue;
            if (hit.distance > rayLength) break;

            moveAmount.y = (hit.distance - skinWidth) * directionY;
            rayLength = hit.distance;

            if (collisions.climbingSlope)
                moveAmount.x = moveAmount.y / Mathf.Tan(collisions.slopeAngle * Mathf.Deg2Rad) * Mathf.Sign(moveAmount.x);

            collisions.below = directionY == -1;
            collisions.above = directionY == 1;

            SearchConveyor(hit);
            SearchMovingPlatform(hit, directionY);

            if (hit.transform != transform && hit.collider.CompareTag("Player") && hit.transform.position.y + (boxCollider.size.y * 0.5f)  < transform.position.y)
            {
                // ��Ʈ��ũ ��ü�� NetId�� ��������
                NetworkIdentity networkIdentity = hit.transform.GetComponent<NetworkIdentity>();

                if (networkIdentity != null)
                {
                    underPlayer = networkIdentity;
                }
            }
        }
    }

    private void SearchConveyor(RaycastHit2D hit)
    {
        Conveyor conveyor = hit.transform.GetComponent<Conveyor>();
        onConveyor = conveyor;
    }

    private void SearchMovingPlatform(RaycastHit2D hit, float directionY)
    {
        if (directionY != -1) return;
        MovingPlatform platform = hit.transform.GetComponent<MovingPlatform>();
        if (platform != null)
            onMovingPlatform = platform;
    }

    private void ProcessVerticalHitsPlayer(RaycastHit2D[] hits, Vector2 dir, float directionY)
    {
        foreach (var hit in hits)
        {
            if (!IsValidHit(hit, dir)) continue;

            if (hit.transform != transform && hit.collider.CompareTag("Player") && hit.transform.position.y + (boxCollider.size.y * 0.5f) < transform.position.y)
            {
                // ��Ʈ��ũ ��ü�� NetId�� ��������
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
    private bool IsValidHit(RaycastHit2D hit, Vector2 moveAmount, bool isVertical = false)
    {
        if (hit.collider == null || hit.collider == GetComponent<Collider2D>())
            return false;

        //if (hit.collider.CompareTag("Through") && Mathf.Sign(moveAmount.y) == 1)
        if (!isVertical && hit.collider.CompareTag("Through"))
            return false;

        if (isVertical && hit.collider.CompareTag("Through") && Mathf.Sign(moveAmount.y) == 1)
            return false;

        if (hit.collider.gameObject == objCollider.gameObject)
            return false;

        // 머리 위에 든 플레이어는 자식으로 붙어 같이 움직이므로 위쪽 레이가 막히면 안 된다.
        if (heldObj != null && hit.collider.gameObject == heldObj)
            return false;

        return true;
    }

    public void SetHoldObj(GameObject holdObj) => heldObj = holdObj;
    public void HoldReset() => heldObj = null;

    public void UnderPlayerReset() => underPlayer = null;

    public struct CollisionInfo
    {
        public bool above, below, left, right;
        public bool climbingSlope, descendingSlope, slidingDownMaxSlope, isSlide;
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
