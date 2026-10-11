using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

// 스테이지 밖으로 떨어지거나 날아간 대상을 가려내는 영역.
// 위쪽은 중력으로 다시 내려오므로 검사하지 않는다. 그래서 위로 높이 던져져도 리스폰되지 않는다.
public sealed class FallBoundary
{
    private readonly float minX;
    private readonly float maxX;
    private readonly float minY;

    public FallBoundary(Bounds stageBounds, float margin)
    {
        minX = stageBounds.min.x - margin;
        maxX = stageBounds.max.x + margin;
        minY = stageBounds.min.y - margin;
    }

    public bool IsOutside(Vector2 position) =>
        position.y < minY || position.x < minX || position.x > maxX;

    // 콜라이더는 Auto Sync Transforms가 꺼져 있어 생성 직후 스케일이 반영되지 않고, 타일맵 콜라이더는 생성이 늦을 수 있다.
    // 생성 즉시 정확한 렌더러 범위로 스테이지 크기를 잰다.
    public static bool TryCreate(IEnumerable<GameObject> stageObjects, float margin, out FallBoundary boundary)
    {
        boundary = null;
        bool hasBounds = false;
        Bounds stageBounds = default;

        foreach (GameObject stageObject in stageObjects)
        {
            if (stageObject == null) continue;

            foreach (Renderer renderer in stageObject.GetComponentsInChildren<Renderer>())
            {
                if (!(renderer is SpriteRenderer) && !(renderer is TilemapRenderer)) continue;

                // 빈 타일맵은 원점에 크기 0인 범위를 내므로 넣으면 스테이지가 원점까지 늘어난다.
                Bounds rendererBounds = renderer.bounds;
                if (rendererBounds.size == Vector3.zero) continue;

                if (hasBounds)
                {
                    stageBounds.Encapsulate(rendererBounds);
                }
                else
                {
                    stageBounds = rendererBounds;
                    hasBounds = true;
                }
            }
        }

        if (!hasBounds) return false;

        boundary = new FallBoundary(stageBounds, margin);
        return true;
    }
}
