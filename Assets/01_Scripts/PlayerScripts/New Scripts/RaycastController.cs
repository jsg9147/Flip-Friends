using Mirror;
using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
public class RaycastController : NetworkBehaviour
{
    public bool offsetApply;
    public LayerMask collisionMask;

    public const float skinWidth = .02f;
    const float dstBetweenRays = .15f;

    [HideInInspector]
    public int horizontalRayCount;// 수평 이동시 사용되는 광선 개수 (세로로 몇개 레이저 사용할지)
    [HideInInspector]
    public int verticalRayCount; // 수직 이동시 사용되는 광선 개수 (가로로 몇개 레이저 사용할지)

    [HideInInspector]
    public float horizontalRaySpacing;
    [HideInInspector]
    public float verticalRaySpacing;

    [HideInInspector]
    public BoxCollider2D boxCollider;
    [System.NonSerialized] public RaycastOrigins raycastOrigins;

    [System.NonSerialized] public RaycastOrigins holdObjectRaycast;

    Vector2 offset = Vector2.zero;

    private readonly Collider2D[] blockerBuffer = new Collider2D[8];

    public virtual void Awake()
    {
        boxCollider = GetComponent<BoxCollider2D>();
        if(offsetApply)
            offset = boxCollider.offset;
    }

    public virtual void Start()
    {
        CalculateRaySpacing();
    }
    public void UpdateRaycastOrigins()
    {
        Bounds bounds = GetColliderBounds(); // 오브젝트를 감싼 콜라이더
        bounds.Expand(skinWidth * -2); // 스킨보다 약간 안쪽부터 생성

        // offset 값을 bounds에 반영
        raycastOrigins.bottomLeft = new Vector2(bounds.min.x + offset.x, bounds.min.y + offset.y);
        raycastOrigins.bottomRight = new Vector2(bounds.max.x + offset.x, bounds.min.y + offset.y);
        raycastOrigins.topLeft = new Vector2(bounds.min.x + offset.x, bounds.max.y + offset.y);
        raycastOrigins.topRight = new Vector2(bounds.max.x + offset.x, bounds.max.y + offset.y);

        holdObjectRaycast.bottomLeft = new Vector2(bounds.min.x + offset.x, bounds.min.y + 1f + offset.y);
        holdObjectRaycast.bottomRight = new Vector2(bounds.max.x + offset.x, bounds.min.y + 1f + offset.y);
        holdObjectRaycast.topLeft = new Vector2(bounds.min.x + offset.x, bounds.max.y + 1f + offset.y);
        holdObjectRaycast.topRight = new Vector2(bounds.max.x + offset.x, bounds.max.y + 1f + offset.y);
    }


    protected virtual Bounds GetColliderBounds() => boxCollider.bounds;

    // Auto Sync Transforms가 꺼져 있어 boxCollider.bounds는 마지막 물리 스텝 위치에 머물고, 콜라이더가 꺼져 있으면 비어 있다.
    // 한 프레임에 여러 번 움직이거나 숨겨 둔 오브젝트를 내려놓을 때는 transform 기준으로 계산한다. 회전은 고려하지 않는다.
    protected Bounds ComputeBoundsFromTransform()
    {
        Vector3 scale = transform.lossyScale;
        Vector2 size = new Vector2(boxCollider.size.x * Mathf.Abs(scale.x), boxCollider.size.y * Mathf.Abs(scale.y));
        return new Bounds(transform.TransformPoint(boxCollider.offset), size);
    }

    // position에 이 오브젝트를 두면 겹치는 콜라이더. inset만큼 안쪽은 접촉으로 보고 무시한다.
    public Collider2D FindBlockingCollider(Vector2 position, float inset)
    {
        Bounds bounds = ComputeBoundsFromTransform();
        Vector2 center = (Vector2)bounds.center + (position - (Vector2)transform.position);
        Vector2 size = (Vector2)bounds.size - Vector2.one * (inset * 2);

        var filter = new ContactFilter2D { useTriggers = false };
        filter.SetLayerMask(collisionMask);
        int count = Physics2D.OverlapBox(center, size, 0f, filter, blockerBuffer);

        for (int i = 0; i < count; i++)
        {
            if (IsBlocker(blockerBuffer[i]))
                return blockerBuffer[i];
        }
        return null;
    }

    // 2D 물리 형상은 접촉 여유 반경이 있어, 바닥에 딱 붙어 서 있기만 해도 skinWidth 안쪽 검사에 겹침으로 잡힌다.
    // 레이는 skinWidth 안쪽에서 출발하므로 그보다 깊이 박힌 경우만 겹침으로 본다.
    public const float overlapTolerance = skinWidth * 2;

    public bool CanOccupy(Vector2 position) => FindBlockingCollider(position, overlapTolerance) == null;

    public Bounds CurrentBounds => ComputeBoundsFromTransform();

    protected virtual bool IsBlocker(Collider2D hit) => hit != boxCollider && !hit.CompareTag("Through");

    void CalculateRaySpacing()
    {
        Bounds bounds = GetColliderBounds();
        bounds.Expand(skinWidth * -2);

        float boundsWidth = bounds.size.x;
        float boundsHeight = bounds.size.y;

        horizontalRayCount = Mathf.RoundToInt(boundsHeight / dstBetweenRays);
        verticalRayCount = Mathf.RoundToInt(boundsWidth / dstBetweenRays);

        horizontalRaySpacing = bounds.size.y / (horizontalRayCount - 1);
        verticalRaySpacing = bounds.size.x / (verticalRayCount - 1);
    }

    public struct RaycastOrigins
    {
        public Vector2 topLeft, topRight;
        public Vector2 bottomLeft, bottomRight;
    }
}
