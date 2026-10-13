using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

// 맵 데이터만으로 커스텀 맵이 실제로 그려질 범위를 계산한다.
// 런타임 추락 판정과 맵 에디터 미리보기가 같은 계산을 써야 에디터에 보이는 선과 실제 리스폰 위치가 맞는다.
// 에디터는 썸네일만 그리므로 화면의 오브젝트 크기 대신 팔레트 프리팹의 렌더러 크기를 쓴다.
public static class MapRenderBounds
{
    // 프리팹 루트 기준 렌더러 범위. 범위가 없는 프리팹은 null로 캐시해 매번 다시 찾지 않는다.
    private static readonly Dictionary<GameObject, Bounds?> localBoundsCache = new();

    public static bool TryCalculate(
        IEnumerable<PlacedObjectData> objects,
        MapEditorPalette palette,
        out Bounds mapBounds)
    {
        mapBounds = default;
        if (objects == null || palette == null) return false;

        bool hasBounds = false;
        foreach (PlacedObjectData data in objects)
        {
            if (data == null) continue;

            GameObject prefab = palette.GetPrefab(data.prefabID);
            if (prefab == null || !TryGetLocalBounds(prefab, out Bounds localBounds)) continue;

            // PlacedObjectFactory와 같은 배치: 데이터 위치·Z 회전에 루트 스케일을 데이터 값으로 덮는다.
            Matrix4x4 placement = Matrix4x4.TRS(
                data.position?.ToVector3() ?? Vector3.zero,
                Quaternion.Euler(0f, 0f, data.rotation),
                data.scale?.ToVector3() ?? Vector3.one);
            Bounds worldBounds = TransformBounds(placement, localBounds);

            if (hasBounds)
            {
                mapBounds.Encapsulate(worldBounds);
            }
            else
            {
                mapBounds = worldBounds;
                hasBounds = true;
            }
        }

        return hasBounds;
    }

    private static bool TryGetLocalBounds(GameObject prefab, out Bounds localBounds)
    {
        if (!localBoundsCache.TryGetValue(prefab, out Bounds? cached))
        {
            cached = CalculateLocalBounds(prefab);
            localBoundsCache[prefab] = cached;
        }

        localBounds = cached.GetValueOrDefault();
        return cached.HasValue;
    }

    private static Bounds? CalculateLocalBounds(GameObject prefab)
    {
        Transform root = prefab.transform;
        Bounds? result = null;

        // 프리팹 에셋은 씬에 없어 activeInHierarchy를 믿을 수 없으므로 비활성까지 받아 직접 거른다.
        foreach (Renderer renderer in prefab.GetComponentsInChildren<Renderer>(true))
        {
            if (!IsActiveUnder(renderer.transform, root)) continue;
            if (!TryGetRendererLocalBounds(renderer, out Bounds rendererBounds)) continue;

            Matrix4x4 toRoot = root.worldToLocalMatrix * renderer.transform.localToWorldMatrix;
            Bounds boundsInRoot = TransformBounds(toRoot, rendererBounds);
            if (result.HasValue)
            {
                Bounds merged = result.Value;
                merged.Encapsulate(boundsInRoot);
                result = merged;
            }
            else
            {
                result = boundsInRoot;
            }
        }

        return result;
    }

    // 런타임 FallBoundary가 세는 렌더러 종류와 같아야 한다.
    private static bool TryGetRendererLocalBounds(Renderer renderer, out Bounds bounds)
    {
        bounds = default;
        if (renderer is SpriteRenderer spriteRenderer)
        {
            Sprite sprite = spriteRenderer.sprite;
            if (sprite == null) return false;

            bounds = spriteRenderer.drawMode == SpriteDrawMode.Simple
                ? sprite.bounds
                : GetResizedSpriteBounds(spriteRenderer, sprite);
            Vector3 center = bounds.center;
            if (spriteRenderer.flipX) center.x = -center.x;
            if (spriteRenderer.flipY) center.y = -center.y;
            bounds.center = center;
        }
        else if (renderer is TilemapRenderer && renderer.TryGetComponent(out Tilemap tilemap))
        {
            bounds = tilemap.localBounds;
        }
        else
        {
            return false;
        }

        return bounds.size != Vector3.zero;
    }

    // Sliced·Tiled 스프라이트는 size 크기로 피벗 기준에 맞춰 그려진다.
    private static Bounds GetResizedSpriteBounds(SpriteRenderer spriteRenderer, Sprite sprite)
    {
        Vector2 size = spriteRenderer.size;
        Vector2 pivot01 = new Vector2(
            sprite.pivot.x / sprite.rect.width,
            sprite.pivot.y / sprite.rect.height);
        Vector2 center = new Vector2((0.5f - pivot01.x) * size.x, (0.5f - pivot01.y) * size.y);
        return new Bounds(center, size);
    }

    private static bool IsActiveUnder(Transform target, Transform root)
    {
        for (Transform current = target; current != null; current = current.parent)
        {
            if (!current.gameObject.activeSelf) return false;
            if (current == root) return true;
        }

        return true;
    }

    private static Bounds TransformBounds(Matrix4x4 matrix, Bounds bounds)
    {
        Vector3 min = bounds.min;
        Vector3 max = bounds.max;
        Bounds result = new Bounds(matrix.MultiplyPoint3x4(min), Vector3.zero);
        for (int i = 1; i < 8; i++)
        {
            Vector3 corner = new Vector3(
                (i & 1) == 0 ? min.x : max.x,
                (i & 2) == 0 ? min.y : max.y,
                (i & 4) == 0 ? min.z : max.z);
            result.Encapsulate(matrix.MultiplyPoint3x4(corner));
        }

        return result;
    }
}
