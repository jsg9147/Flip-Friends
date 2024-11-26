using Mirror;
using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
public class RaycastController : NetworkBehaviour
{
    public LayerMask collisionMask;

    public const float skinWidth = .015f;
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
    public RaycastOrigins raycastOrigins;

    public RaycastOrigins holdObjectRaycast;

    public virtual void Awake()
    {
        boxCollider = GetComponent<BoxCollider2D>();
    }

    public virtual void Start()
    {
        CalculateRaySpacing();
    }

    public void UpdateRaycastOrigins()
    {
        Bounds bounds = boxCollider.bounds; // 오브젝트를 감싼 콜라이더
        bounds.Expand(skinWidth * -2); // 스킨보다 약간 안쪽부터 생성

        raycastOrigins.bottomLeft = new Vector2(bounds.min.x, bounds.min.y);
        raycastOrigins.bottomRight = new Vector2(bounds.max.x, bounds.min.y);
        raycastOrigins.topLeft = new Vector2(bounds.min.x, bounds.max.y);
        raycastOrigins.topRight = new Vector2(bounds.max.x, bounds.max.y);

        holdObjectRaycast.bottomLeft = new Vector2(bounds.min.x, bounds.min.y + 1f);
        holdObjectRaycast.bottomRight = new Vector2(bounds.max.x, bounds.min.y + 1f);
        holdObjectRaycast.topLeft = new Vector2(bounds.min.x, bounds.max.y + 1f);
        holdObjectRaycast.topRight = new Vector2(bounds.max.x, bounds.max.y + 1f);
    }

    void CalculateRaySpacing()
    {
        Bounds bounds = boxCollider.bounds;
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
