using Mirror;
using UnityEngine;

// 들기·던지기·내려놓기는 모두 서버가 결정한다. 위치와 속도를 서버가 정해 모든 화면이 같은 결과를 보게 한다.
public class PlayerInteraction : NetworkBehaviour
{
    public float throwForce = 3f;
    public BoxCollider2D catchedCollider;
    public LayerMask detectionLayer;

    [SerializeField] private Vector3 heldPos;

    [Header("플레이어 운반")]
    [SerializeField] private Vector2 playerThrowVelocity = new Vector2(7f, 12f);
    [SerializeField] private float thrownUncontrollableDuration = 0.4f;
    [Tooltip("들린 플레이어가 이 시간(초)이 지나야 조작 입력으로 빠져나올 수 있다")]
    [SerializeField] private float carryLockDuration = 1.5f;

    private const float SearchRayLength = 0.2f;
    private const int SearchRayCount = 10;
    private const float SearchRaySpacingRatio = 1f / 8f;

    private BoxCollider2D boxCollider;
    private Controller2D controller;
    private PlayerController2D playerController;

    private PickupObj heldObject;
    private PlayerController2D heldPlayer;
    private double carryStartTime;

    // 들자마자 같은 입력으로 던지는 것을 막는 최소 간격. 들기 입력은 누른 순간 한 번만 오므로 짧아도 된다.
    [Tooltip("들고 난 뒤 이 시간(초) 동안은 던지기·내려놓기 입력을 무시한다")]
    [SerializeField] private float throwDealy = 0.15f;
    private float currentDelay = 0f;

    public bool IsHoldingObject => heldObject != null;
    public bool IsHoldingAnything => heldObject != null || heldPlayer != null;
    public Vector3 HeldOffset => heldPos;

    private void Awake()
    {
        boxCollider = GetComponent<BoxCollider2D>();
        controller = GetComponent<Controller2D>();
        playerController = GetComponent<PlayerController2D>();
    }

    private void Update()
    {
        if (currentDelay > 0)
            currentDelay -= Time.deltaTime;

        if (isServer)
            FollowToPlayer();
    }

    [Server]
    public void TryIntractive(Vector2 dir, bool isPutDown)
    {
        if (playerController.isCarried) return;

        if (!IsHoldingAnything)
        {
            if (CheckObjectAbove()) return;

            PlayerController2D targetPlayer = SearchPlayer(dir);
            if (targetPlayer != null && targetPlayer.CanBeCarried)
            {
                PickUpPlayer(targetPlayer);
                return;
            }

            var obj = SearchObject<PickupObj>(dir);
            if (obj != null)
                PickUpObj(obj);
        }
        else if (currentDelay <= 0)
        {
            Release(dir, isPutDown);
        }
    }

    // 피격·리셋·도착처럼 손을 놓아야 하는 상황. 들고 있던 것은 앞에 내려놓는다.
    [Server]
    public void DropAll()
    {
        Release(playerController.FacingDirection, true);
    }

    // 들린 플레이어가 고정 시간이 지난 뒤 조작하면 운반자가 보는 방향으로 강제로 던져진다.
    [Server]
    public bool TryEscape(PlayerController2D carried)
    {
        if (heldPlayer != carried) return false;
        if (NetworkTime.time - carryStartTime < carryLockDuration) return false;

        ReleaseHeldPlayer(playerController.FacingDirection, false);
        return true;
    }

    // 들려 있던 플레이어가 나가면 손만 비운다. 내려놓을 대상이 이미 없다.
    [Server]
    public void ForgetHeldPlayer(PlayerController2D carried)
    {
        if (heldPlayer != carried) return;
        heldPlayer = null;
        SetHoldTarget(null);
    }

    private void Release(Vector2 dir, bool isPutDown)
    {
        if (heldPlayer != null)
            ReleaseHeldPlayer(dir, isPutDown);
        else if (heldObject != null)
            ReleaseHeldObject(dir, isPutDown);
    }

    private void PickUpPlayer(PlayerController2D targetPlayer)
    {
        currentDelay = throwDealy;
        heldPlayer = targetPlayer;
        carryStartTime = NetworkTime.time;

        SetHoldTarget(targetPlayer.gameObject);
        targetPlayer.BeginCarried(this);
        RpcSetPlayerCollision(targetPlayer.netIdentity, true);
    }

    private void ReleaseHeldPlayer(Vector2 dir, bool isPutDown)
    {
        PlayerController2D target = heldPlayer;
        heldPlayer = null;
        SetHoldTarget(null);
        RpcSetPlayerCollision(target.netIdentity, false);

        RaycastController targetBody = target.GetComponent<Controller2D>();
        Vector2 carriedPosition = transform.position + heldPos;
        Vector2 position;
        Vector2 velocity;

        if (isPutDown)
        {
            position = FindPutDownPosition(targetBody, dir.x, carriedPosition);
            velocity = Vector2.zero;
        }
        else
        {
            // 머리 위가 막혀 있으면(낮은 천장) 운반자 자리에서 던진다. 플레이어끼리는 겹쳐도 밀어내지 않는다.
            position = targetBody.CanOccupy(carriedPosition) ? carriedPosition : (Vector2)transform.position;
            velocity = new Vector2(Mathf.Sign(dir.x) * playerThrowVelocity.x, playerThrowVelocity.y);
        }

        target.EndCarried(position, velocity, isPutDown ? 0f : thrownUncontrollableDuration);
    }

    private void PickUpObj(PickupObj pickableObj)
    {
        currentDelay = throwDealy;
        heldObject = pickableObj;
        SetHoldTarget(pickableObj.gameObject);
        pickableObj.SetPickupState(transform, true);
        SetCollisionWithHeldObject(pickableObj, true);
        RpcVisibleBox(true);
    }

    private void ReleaseHeldObject(Vector2 dir, bool isPutDown)
    {
        PickupObj box = heldObject;
        heldObject = null;

        Vector2 carriedPosition = transform.position + heldPos;
        Vector2 position;
        Vector2 velocity;

        if (isPutDown)
        {
            position = FindPutDownPosition(box, dir.x, carriedPosition);
            velocity = Vector2.zero;
        }
        else
        {
            position = box.CanOccupy(carriedPosition) ? carriedPosition : FindPutDownPosition(box, dir.x, carriedPosition);
            velocity = new Vector2(Mathf.Sign(dir.x) * throwForce, throwForce);
        }

        box.Release(position, velocity);
        RpcVisibleBox(false);
        SetHoldTarget(null);
        SetCollisionWithHeldObject(box, false);
    }

    // 앞쪽 바닥 높이에 내려놓는다. 벽에 막히면 머리 위, 그것도 막히면 운반자 자리를 쓴다.
    private Vector2 FindPutDownPosition(RaycastController target, float dirX, Vector2 carriedPosition)
    {
        Bounds carrierBounds = controller.CurrentBounds;
        Bounds targetBounds = target.CurrentBounds;
        Vector2 pivotOffset = (Vector2)target.transform.position - (Vector2)targetBounds.center;
        float gap = RaycastController.skinWidth;

        Vector2 frontCenter = new Vector2(
            carrierBounds.center.x + Mathf.Sign(dirX) * (carrierBounds.extents.x + targetBounds.extents.x + gap),
            carrierBounds.min.y + targetBounds.extents.y + gap);
        Vector2 front = frontCenter + pivotOffset;

        if (target.CanOccupy(front)) return front;
        if (target.CanOccupy(carriedPosition)) return carriedPosition;
        return transform.position;
    }

    private void SetHoldTarget(GameObject target)
    {
        ApplyHoldTarget(target);
        RpcSetHoldTarget(target);
    }

    // 운반자 클라이언트의 예측도 머리 위 물체를 알아야 낮은 천장에서 서버와 같은 곳에 멈춘다.
    [ClientRpc]
    private void RpcSetHoldTarget(GameObject target)
    {
        if (isServer) return;
        ApplyHoldTarget(target);
    }

    private void ApplyHoldTarget(GameObject target)
    {
        if (target != null)
            controller.SetHoldObj(target);
        else
            controller.HoldReset();
    }

    [ClientRpc]
    private void RpcVisibleBox(bool visible)
    {
        catchedCollider.enabled = visible;
        catchedCollider.GetComponent<SpriteRenderer>().enabled = visible;
    }

    // 상자는 숨겨진 채 서버에서만 따라다닌다. 머리 위 상자 모양은 catchedCollider가 보여 준다.
    private void FollowToPlayer()
    {
        if (heldObject != null)
            heldObject.transform.position = transform.position + heldPos;
    }

    public override void OnStopServer()
    {
        // 운반자가 나가거나 파괴되면 들고 있던 것을 내려놓는다. 안 그러면 자식으로 붙은 플레이어가 같이 사라진다.
        if (NetworkServer.active && IsHoldingAnything)
            DropAll();
        base.OnStopServer();
    }

    public override void OnStopClient()
    {
        // 운반자 파괴 메시지가 서버의 해제 RPC보다 먼저 도착하므로, 자식으로 붙은 플레이어를 먼저 떼어 같이 파괴되지 않게 한다.
        foreach (PlayerController2D carried in GetComponentsInChildren<PlayerController2D>(true))
        {
            if (carried != playerController)
                carried.DetachFromCarrierLocally();
        }
        base.OnStopClient();
    }

    private PlayerController2D SearchPlayer(Vector2 dir)
    {
        foreach (RaycastHit2D hit in CastSearchRays(dir, LayerMask.GetMask("Player")))
        {
            if (hit.collider != null && hit.collider.gameObject != gameObject)
            {
                var pc = hit.collider.GetComponent<PlayerController2D>();
                if (pc != null)
                    return pc;
            }
        }
        return null;
    }

    private T SearchObject<T>(Vector2 dir) where T : Component
    {
        foreach (RaycastHit2D hit in CastSearchRays(dir, LayerMask.GetMask("Pickable")))
        {
            if (hit.collider != null && hit.collider.gameObject != gameObject)
            {
                T obj = hit.collider.GetComponent<T>();
                if (obj != null)
                    return obj;
            }
        }
        return null;
    }

    private System.Collections.Generic.IEnumerable<RaycastHit2D> CastSearchRays(Vector2 dir, int layerMask)
    {
        Bounds bounds = controller.CurrentBounds;
        float raySpacing = boxCollider.size.x * SearchRaySpacingRatio;
        float xPos = (dir.x > 0) ? bounds.max.x : bounds.min.x;

        for (int i = 0; i < SearchRayCount; i++)
        {
            Vector2 rayOrigin = new Vector2(xPos, bounds.min.y + (i * raySpacing) - raySpacing);
            Debug.DrawRay(rayOrigin, dir * SearchRayLength, Color.red, 0.1f);

            foreach (RaycastHit2D hit in Physics2D.RaycastAll(rayOrigin, dir, SearchRayLength, layerMask))
                yield return hit;
        }
    }

    private bool CheckObjectAbove()
    {
        // 머리 위 상자 자리에 다른 물체가 있으면 들 수 없다.
        Vector2 boxCenter = catchedCollider.transform.position;
        Collider2D[] hits = Physics2D.OverlapBoxAll(boxCenter, catchedCollider.size, 0f, detectionLayer);
        return hits.Length > 0;
    }

    [ClientRpc]
    private void RpcSetPlayerCollision(NetworkIdentity targetPlayer, bool ignore)
    {
        if (targetPlayer == null) return;
        Collider2D heldCollider = targetPlayer.GetComponent<Collider2D>();
        if (heldCollider == null) return;
        Physics2D.IgnoreCollision(boxCollider, heldCollider, ignore);
        Physics2D.IgnoreCollision(catchedCollider, heldCollider, ignore);
    }

    private void SetCollisionWithHeldObject(PickupObj obj, bool ignore)
    {
        Collider2D heldCollider = obj.GetComponent<Collider2D>();
        if (heldCollider != null && boxCollider != null)
        {
            Physics2D.IgnoreCollision(boxCollider, heldCollider, ignore);
            Physics2D.IgnoreCollision(catchedCollider, heldCollider, ignore);
        }
    }
}
