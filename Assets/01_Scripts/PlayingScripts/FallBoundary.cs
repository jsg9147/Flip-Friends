using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

// 스테이지 밖으로 떨어지거나 날아간 대상을 가려내는 영역.
// 위쪽은 중력으로 다시 내려오므로 검사하지 않는다. 그래서 위로 높이 던져져도 리스폰되지 않는다.
public sealed class FallBoundary
{
    // 스테이지 그림 범위에서 이만큼 더 벗어나야 추락으로 본다. 끝자락에서 뛰는 정상 플레이를 잘못 잡지 않게 넉넉히 둔다.
    // 맵 에디터 미리보기도 이 값을 쓰므로 Inspector 값으로 두지 않는다.
    public const float Margin = 10f;

    public float MinX { get; }
    public float MaxX { get; }
    public float MinY { get; }
    // 검사하지 않는 위쪽 경계. 미리보기에서 옆 경계선을 어디까지 그릴지 정할 때만 쓴다.
    public float TopY { get; }

    public FallBoundary(Bounds stageBounds)
    {
        MinX = stageBounds.min.x - Margin;
        MaxX = stageBounds.max.x + Margin;
        MinY = stageBounds.min.y - Margin;
        TopY = stageBounds.max.y + Margin;
    }

    public bool IsOutside(Vector2 position) =>
        position.y < MinY || position.x < MinX || position.x > MaxX;

    // 기본 스테이지용. 콜라이더는 Auto Sync Transforms가 꺼져 있어 생성 직후 값이 늦고, 타일맵 콜라이더는 생성이 늦을 수 있다.
    // 생성 즉시 정확한 렌더러 범위로 스테이지 크기를 잰다.
    public static bool TryCreate(IEnumerable<GameObject> stageObjects, out FallBoundary boundary)
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

        boundary = new FallBoundary(stageBounds);
        return true;
    }

    // 커스텀 맵용. 맵 에디터 미리보기와 같은 계산을 써서 에디터에 보인 선에서 리스폰되게 한다.
    public static bool TryCreate(IEnumerable<PlacedObjectData> objects, MapEditorPalette palette, out FallBoundary boundary)
    {
        boundary = null;
        if (!MapRenderBounds.TryCalculate(objects, palette, out Bounds mapBounds)) return false;

        boundary = new FallBoundary(mapBounds);
        return true;
    }
}
