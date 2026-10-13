using System;
using System.Collections.Generic;
using UnityEngine;

// 맵 에디터 화면에 배치 가능 범위와 추락 판정 경계를 선으로 그린다.
// 추락 경계는 배치가 바뀔 때마다 움직이므로, 테스트 플레이 전에 어디서 리스폰되는지 보이게 한다.
public class MapBoundsPreview : MonoBehaviour
{
    private const int RectanglePointCount = 4;

    [SerializeField] private Material lineMaterial;
    [SerializeField] private Color placementAreaColor = new Color(1f, 1f, 1f, 0.35f);
    [SerializeField] private Color fallBoundaryColor = new Color(1f, 0.35f, 0.35f, 0.8f);
    // 카메라 세로 크기에 비례시켜 확대·축소해도 화면에서 같은 두께로 보이게 한다.
    [SerializeField] private float lineWidthPerOrthographicSize = 0.006f;
    // 배치 오브젝트(0)와 고스트(10) 위에 그려 가려지지 않게 한다.
    [SerializeField] private int sortingOrder = 20;

    private LineRenderer placementAreaLine;
    private LineRenderer fallBoundaryLine;
    private readonly Vector3[] linePoints = new Vector3[RectanglePointCount];

    private int lastObjectsSignature;
    private bool hasObjectsSignature;

    private void Awake()
    {
        if (lineMaterial == null)
        {
            Debug.LogError($"범위 표시 선의 Material이 연결되어 있지 않습니다: {name}", this);
            enabled = false;
            return;
        }

        placementAreaLine = CreateLine("PlacementArea", placementAreaColor, isLoop: true);
        fallBoundaryLine = CreateLine("FallBoundary", fallBoundaryColor, isLoop: false);
    }

    private void LateUpdate()
    {
        MapEditorPalette palette = MapEditorManager.instance?.palette;
        if (palette == null)
        {
            placementAreaLine.enabled = false;
            fallBoundaryLine.enabled = false;
            return;
        }

        UpdatePlacementArea(palette.ValidationSettings);
        UpdateFallBoundary(MapEditorManager.instance.CurrentMapData?.objects, palette);
        UpdateLineWidth();
    }

    private LineRenderer CreateLine(string lineName, Color color, bool isLoop)
    {
        GameObject lineObject = new GameObject(lineName);
        lineObject.transform.SetParent(transform, false);

        LineRenderer line = lineObject.AddComponent<LineRenderer>();
        line.sharedMaterial = lineMaterial;
        line.useWorldSpace = true;
        line.loop = isLoop;
        line.positionCount = RectanglePointCount;
        // 두께는 widthMultiplier로만 바꾸도록 곡선을 1로 고정한다.
        line.widthCurve = AnimationCurve.Constant(0f, 1f, 1f);
        line.startColor = color;
        line.endColor = color;
        line.sortingOrder = sortingOrder;
        line.numCornerVertices = 0;
        line.numCapVertices = 0;
        return line;
    }

    private void UpdatePlacementArea(MapValidationSettings settings)
    {
        Vector2 min = settings.minimumMapPosition;
        Vector2 max = settings.maximumMapPosition;
        linePoints[0] = new Vector3(min.x, min.y);
        linePoints[1] = new Vector3(max.x, min.y);
        linePoints[2] = new Vector3(max.x, max.y);
        linePoints[3] = new Vector3(min.x, max.y);
        placementAreaLine.SetPositions(linePoints);
        placementAreaLine.enabled = true;
    }

    // 이동·회전·크기 변경은 MapDataChanged를 내지 않으므로 데이터 서명을 비교해 바뀐 프레임에만 다시 계산한다.
    private void UpdateFallBoundary(List<PlacedObjectData> objects, MapEditorPalette palette)
    {
        int signature = CalculateObjectsSignature(objects);
        if (hasObjectsSignature && signature == lastObjectsSignature) return;

        lastObjectsSignature = signature;
        hasObjectsSignature = true;

        if (!FallBoundary.TryCreate(objects, palette, out FallBoundary boundary))
        {
            fallBoundaryLine.enabled = false;
            return;
        }

        // 위쪽은 판정하지 않으므로 열린 ㄷ자로 그린다.
        linePoints[0] = new Vector3(boundary.MinX, boundary.TopY);
        linePoints[1] = new Vector3(boundary.MinX, boundary.MinY);
        linePoints[2] = new Vector3(boundary.MaxX, boundary.MinY);
        linePoints[3] = new Vector3(boundary.MaxX, boundary.TopY);
        fallBoundaryLine.SetPositions(linePoints);
        fallBoundaryLine.enabled = true;
    }

    private static int CalculateObjectsSignature(List<PlacedObjectData> objects)
    {
        if (objects == null) return 0;

        HashCode hash = new HashCode();
        hash.Add(objects.Count);
        foreach (PlacedObjectData data in objects)
        {
            if (data == null) continue;

            hash.Add(data.prefabID);
            hash.Add(data.position?.ToVector3() ?? Vector3.zero);
            hash.Add(data.rotation);
            hash.Add(data.scale?.ToVector3() ?? Vector3.one);
        }

        return hash.ToHashCode();
    }

    private void UpdateLineWidth()
    {
        Camera editorCamera = Camera.main;
        if (editorCamera == null) return;

        float width = editorCamera.orthographicSize * lineWidthPerOrthographicSize;
        placementAreaLine.widthMultiplier = width;
        fallBoundaryLine.widthMultiplier = width;
    }
}
