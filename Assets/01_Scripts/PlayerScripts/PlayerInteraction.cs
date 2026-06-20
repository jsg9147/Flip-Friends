using Mirror;
using UnityEngine;

public class PlayerInteraction : NetworkBehaviour
{
    public float throwForce = 3f;
    public BoxCollider2D catchedCollider;
    public LayerMask detectionLayer; // Ž���� ���̾�

    private BoxCollider2D boxCollider;

    private PickupObj heldObject;

    private PlayerController2D heldPlayer;  // �÷��̾ ��� ���� �� ����
    public bool IsCarriedPlayer => heldPlayer != null;

    [SerializeField] private Vector3 heldPos;

    private float throwDealy = 0.5f;
    private float currentDelay = 0f;

    public bool IsHoldingObject => heldObject != null;

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
        // ���� �ƹ��͵� ��� ���� ���� ���
        if (heldObject == null && heldPlayer == null)
        {
            // 1) �Ӹ� ���� �ٸ� ������Ʈ�� �÷��̾ �ִ��� Ȯ�� (CheckObjectAbove())
            if (!CheckObjectAbove())
            {
                // 2) ���� �÷��̾� Ž��
                PlayerController2D targetPlayer = SearchPlayer(dir);
                if (targetPlayer != null && !targetPlayer.isCarried)
                {
                    // �÷��̾ ��� ����
                    PickUpPlayer(targetPlayer);
                    return;
                }

                // 3) �÷��̾ ������ ������ �ϴ���� PickupObj Ž��
                var obj = SearchObject<PickupObj>(dir);
                if (obj != null)
                {
                    PickUpObj(obj);
                }
            }
        }
        else
        {
            // ���𰡸� ��� �ִٸ� �� ������ ó��
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
            // �� "Player" ���̾ ����Ѵٸ� ���� ����

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

        heldPlayer.SetCarriedState(true, transform);

        // 충돌 무시를 모든 클라이언트에 전파 — 서버에서만 설정하면 클라이언트에서 들려있는 플레이어와 충돌 발생
        RpcSetPlayerCollision(targetPlayer.netIdentity, true);
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
        if (heldPlayer == null) return;

        RpcSetPlayerCollision(heldPlayer.netIdentity, false);
        heldPlayer.SetCarriedState(false, null);
        heldPlayer = null;
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
        // �ڽ��� �߽� ��� (�÷��̾��� ����)
        Vector2 boxCenter = catchedCollider.transform.position;

        // �ڽ� ������ ��� �浹 ����
        Collider2D[] hits = Physics2D.OverlapBoxAll(boxCenter, catchedCollider.size, 0f, detectionLayer);

        // ����׿� �ð�ȭ (Scene â���� Ȯ�� ����)
        Debug.DrawLine(boxCenter - new Vector2(catchedCollider.size.x / 2, catchedCollider.size.y / 2),
                       boxCenter + new Vector2(catchedCollider.size.x / 2, catchedCollider.size.y / 2),
                       Color.red, 0.1f);

        return hits.Length > 0; // �ڽ� �ȿ� ��ü�� ������ true ��ȯ
    }
    private T SearchObject<T>(Vector2 dir) where T : Component
    {
        Vector2 boxSize = boxCollider.size;
        Vector2 boxCenter = (Vector2)transform.position + boxCollider.offset;
        float raySpacing = boxSize.x / 8f; // �ڽ��� ���� ũ�⸦ �������� ���� ���� ���̸� ����
        int rayCount = 10; // �� 5���� Raycast ���
        float xPos = (dir.x > 0) ? boxCollider.bounds.max.x : boxCollider.bounds.min.x;

        for (int i = 0; i < rayCount; i++)
        {
            // ���� ���� ��ġ�� ���ʿ��� ���� �������� ����
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

    [ClientRpc]
    private void RpcSetPlayerCollision(NetworkIdentity targetPlayer, bool ignore)
    {
        Collider2D heldCollider = targetPlayer.GetComponent<Collider2D>();
        if (heldCollider == null) return;
        Physics2D.IgnoreCollision(boxCollider, heldCollider, ignore);
        Physics2D.IgnoreCollision(catchedCollider, heldCollider, ignore);
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
