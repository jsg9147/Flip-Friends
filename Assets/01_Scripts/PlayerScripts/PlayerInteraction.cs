using Mirror;
using UnityEngine;

public class PlayerInteraction : NetworkBehaviour
{
    public float throwForce = 3f;
    public BoxCollider2D catchedCollider;
    public LayerMask detectionLayer; // 탐지할 레이어

    private BoxCollider2D boxCollider;

    private PickupObj heldObject;

    private PlayerController2D heldPlayer;  // 플레이어를 들고 있을 때 저장
    public bool IsCarriedPlayer => heldPlayer != null;

    [SerializeField] private Vector3 heldPos;

    private float throwDealy = 0.5f;
    private float currentDelay = 0f;

    public bool IsCarried => heldObject != null;

    private void Start()
    {
        boxCollider = GetComponent<BoxCollider2D>();
    }

    private void Update()
    {
        if (currentDelay > 0)
            currentDelay -= Time.deltaTime;

        if (isServer)
        {
            FollowToPlayer();
        }
    }

    //public void TryIntractive(Vector2 dir, bool inputDown)
    //{
    //    if (heldObject == null)
    //    {
    //        if (!CheckObjectAbove())
    //        {
    //            var obj = SearchObject<PickupObj>(dir);
    //            if (obj != null)
    //            {
    //                PickUpObj(obj);
    //            }
    //        }
    //    }
    //    else
    //    {
    //        if(currentDelay <= 0)
    //        {
    //            ThrowObject(dir, inputDown);
    //        }
    //    }
    //}

    public void TryIntractive(Vector2 dir, bool inputDown)
    {
        // 현재 아무것도 들고 있지 않은 경우
        if (heldObject == null && heldPlayer == null)
        {
            // 1) 머리 위에 다른 오브젝트나 플레이어가 있는지 확인 (CheckObjectAbove())
            if (!CheckObjectAbove())
            {
                // 2) 먼저 플레이어 탐색
                PlayerController2D targetPlayer = SearchPlayer(dir);
                if (targetPlayer != null && !targetPlayer.isCarried)
                {
                    // 플레이어를 잡는 로직
                    PickUpPlayer(targetPlayer);
                    return;
                }

                // 3) 플레이어가 없으면 기존에 하던대로 PickupObj 탐색
                var obj = SearchObject<PickupObj>(dir);
                if (obj != null)
                {
                    PickUpObj(obj);
                }
            }
        }
        else
        {
            // 무언가를 들고 있다면 → 던지기 처리
            if (currentDelay <= 0)
            {
                ThrowCarried(dir, inputDown);
            }
        }
    }

    private PlayerController2D SearchPlayer(Vector2 dir)
    {
        Vector2 boxSize = boxCollider.size;
        float raySpacing = boxSize.x / 8f;
        int rayCount = 10;
        float xPos = (dir.x > 0) ? boxCollider.bounds.max.x : boxCollider.bounds.min.x;

        for (int i = 0; i < rayCount; i++)
        {
            Vector2 rayOrigin = new Vector2(xPos, boxCollider.bounds.min.y + (i * raySpacing) - raySpacing);
            RaycastHit2D[] hits = Physics2D.RaycastAll(rayOrigin, dir, 0.2f, LayerMask.GetMask("Player"));
            // ↑ "Player" 레이어를 사용한다면 여기 지정

            foreach (RaycastHit2D hit in hits)
            {
                if (hit.collider != null && hit.collider.gameObject != gameObject)
                {
                    var pc = hit.collider.GetComponent<PlayerController2D>();
                    if (pc != null)
                    {
                        return pc;
                    }
                }
            }
        }
        return null;
    }

    private void PickUpPlayer(PlayerController2D targetPlayer)
    {
        currentDelay = throwDealy;
        heldPlayer = targetPlayer;

        // 피잡힌 쪽 PlayerController2D에 "SetCarriedState(true, transform)" 호출
        heldPlayer.SetCarriedState(true, transform);

        // 충돌 무시 처리 (서로 겹쳐도 튕기지 않도록)
        Collider2D heldCollider = targetPlayer.GetComponent<Collider2D>();
        Physics2D.IgnoreCollision(boxCollider, heldCollider, true);
        Physics2D.IgnoreCollision(catchedCollider, heldCollider, true);

        // 필요하면 잡은 쪽(본인)도 애니메이션 변경
        // ex) GetComponent<PlayerAnimationController>().PlayLiftingAnimation(true);
    }
    private void ThrowCarried(Vector2 dir, bool isPutDown)
    {
        if (heldPlayer != null)
        {
            ThrowPlayer(dir, isPutDown);
        }
        else if (heldObject != null)
        {
            ThrowObject(dir, isPutDown);
        }
    }

    private void ThrowPlayer(Vector2 dir, bool isPutDown)
    {
        // heldPlayer를 자유롭게 복구
        if (heldPlayer != null)
        {
            // 잡힌 플레이어 해제
            heldPlayer.SetCarriedState(false, null);

            // 적당한 던지는 힘을 준다
            // MovementHandler가 다시 활성화되므로, 그쪽에서 velocity 직접 세팅해줄 수도 있고,
            // 여기서 Rigidbody2D가 있다면 AddForce로 처리 가능.
            // 예: heldPlayer.GetComponent<Rigidbody2D>().AddForce(new Vector2(dir.x * throwForce, throwForce), ForceMode2D.Impulse);

            // 충돌 무시 해제
            Collider2D heldCollider = heldPlayer.GetComponent<Collider2D>();
            Physics2D.IgnoreCollision(boxCollider, heldCollider, false);
            Physics2D.IgnoreCollision(catchedCollider, heldCollider, false);

            // 본인 측 처리
            heldPlayer = null;

            // 애니메이션, 사운드 처리
            // GetComponent<PlayerAnimationController>().PlayThrowAnimation();
        }
    }


    [Command]
    private void CmdPickUpObj(PickupObj pickableObj)
    {
        if (pickableObj != null)
        {
            if (!pickableObj.GetComponent<PickupObj>().IsCarried)
            {
                PickUpObj(pickableObj);
                RpcPickUpObj(pickableObj);
            }
        }
    }
    [ClientRpc]
    private void RpcPickUpObj(PickupObj pickableObj)
    {
        if (pickableObj.GetComponent<PickupObj>() != null)
        {
            PickUpObj(pickableObj);
        }
    }

    private void PickUpObj(PickupObj pickableObj)
    {
        if (pickableObj.GetComponent<PickupObj>() != null)
        {
            currentDelay = throwDealy;
            heldObject = pickableObj;
            GetComponent<Controller2D>().SetHoldObj(pickableObj.gameObject);
            pickableObj.GetComponent<PickupObj>().SetPickupState(transform, true);
            DisableCollisionWithHeldObject(pickableObj);

            if (isServer)
                RpcVisibleBox(true);
        }
    }

    [ClientRpc]
    private void RpcVisibleBox(bool visible)
    {
        catchedCollider.enabled = visible;
        catchedCollider.GetComponent<SpriteRenderer>().enabled = visible;
    }

    [Command]
    private void CmdThrowObj(Vector2 dir, bool isPutDown)
    {
        ThrowObject(dir, isPutDown);
        RpcThrowObj(dir, isPutDown);
    }

    [ClientRpc]
    private void RpcThrowObj(Vector2 dir, bool isPutDown)
    {
        ThrowObject(dir, isPutDown);
    }

    void ThrowObject(Vector2 dir, bool isPutDown)
    {
        if (heldObject != null)
        {
            if (isServer)
            {
                PickupObj pickUp = heldObject.GetComponent<PickupObj>();
                if (pickUp != null)
                {
                    Vector3 throwDir = new(dir.x * throwForce, throwForce);
                    pickUp.RpcApplyVelocity(throwDir);
                    pickUp.StateReset();
                }
            }

            RpcVisibleBox(false);
            GetComponent<Controller2D>().HoldReset();
            EnableCollisionWithHeldObject(heldObject);
            heldObject = null;
        }
    }
    private void FollowToPlayer()
    {
        if (heldObject != null)
        {
            heldObject.transform.position = transform.position + heldPos;
            RpcheldPosUpdate(transform.position + heldPos);
        }
    }
    [ClientRpc]
    private void RpcheldPosUpdate(Vector3 pos)
    {
        if(heldObject != null)
            heldObject.transform.position = pos;
    }

    private bool CheckObjectAbove()
    {
        // 박스의 중심 계산 (플레이어의 위쪽)
        Vector2 boxCenter = catchedCollider.transform.position;

        // 박스 내부의 모든 충돌 감지
        Collider2D[] hits = Physics2D.OverlapBoxAll(boxCenter, catchedCollider.size, 0f, detectionLayer);

        // 디버그용 시각화 (Scene 창에서 확인 가능)
        Debug.DrawLine(boxCenter - new Vector2(catchedCollider.size.x / 2, catchedCollider.size.y / 2),
                       boxCenter + new Vector2(catchedCollider.size.x / 2, catchedCollider.size.y / 2),
                       Color.red, 0.1f);

        return hits.Length > 0; // 박스 안에 물체가 있으면 true 반환
    }
    private T SearchObject<T>(Vector2 dir) where T : Component
    {
        Vector2 boxSize = boxCollider.size;
        Vector2 boxCenter = (Vector2)transform.position + boxCollider.offset;
        float raySpacing = boxSize.x / 8f; // 박스의 가로 크기를 기준으로 여러 개의 레이를 생성
        int rayCount = 10; // 총 5개의 Raycast 사용
        float xPos = (dir.x > 0) ? boxCollider.bounds.max.x : boxCollider.bounds.min.x;

        for (int i = 0; i < rayCount; i++)
        {
            // 레이 시작 위치를 왼쪽에서 일정 간격으로 설정
            Vector2 rayOrigin = new Vector2(xPos, boxCollider.bounds.min.y + (i * raySpacing) - raySpacing);

            RaycastHit2D[] hits = Physics2D.RaycastAll(rayOrigin, dir, 0.2f, LayerMask.GetMask("Pickable"));
            Debug.DrawRay(rayOrigin, dir * 0.2f, Color.red, 0.1f);

            foreach (RaycastHit2D hit in hits)
            {
                if (hit.collider != null && hit.collider.gameObject != gameObject)
                {
                    T obj = hit.collider.GetComponent<T>();
                    if (obj != null)
                    {
                        return obj;
                    }
                }
            }
        }
        return null;
    }

    void DisableCollisionWithHeldObject(PickupObj objectToPickUp)
    {
        Collider2D heldCollider = objectToPickUp.GetComponent<Collider2D>();
        if (heldCollider != null && boxCollider != null)
        {
            Physics2D.IgnoreCollision(boxCollider, heldCollider, true);
            Physics2D.IgnoreCollision(catchedCollider, heldCollider, true);
        }
    }

    void EnableCollisionWithHeldObject(PickupObj objectToRelease)
    {
        Collider2D heldCollider = objectToRelease.GetComponent<Collider2D>();
        if (heldCollider != null && boxCollider != null)
        {
            Physics2D.IgnoreCollision(boxCollider, heldCollider, false);
            Physics2D.IgnoreCollision(catchedCollider, heldCollider, false);
        }
    }
}
