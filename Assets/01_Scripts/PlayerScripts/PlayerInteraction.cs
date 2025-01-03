using Mirror;
using UnityEngine;

public class PlayerInteraction : NetworkBehaviour
{
    public float throwForce = 3f;
    public BoxCollider2D catchedCollider;
    public LayerMask detectionLayer; // 탐지할 레이어

    private BoxCollider2D boxCollider;

    private PickupObj heldObject;

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

    public void TryIntractive(Vector2 dir, bool inputDown)
    {
        if (heldObject == null)
        {
            if (!CheckObjectAbove())
            {
                var obj = SearchObject<PickupObj>(dir);
                if (obj != null)
                {
                    PickUpObj(obj);
                }
            }
        }
        else
        {
            if(currentDelay <= 0)
            {
                ThrowObject(dir, inputDown);
            }
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
