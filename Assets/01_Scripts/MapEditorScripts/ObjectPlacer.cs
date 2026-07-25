using System.Collections.Generic;
using UnityEngine;

public class ObjectPlacer : MonoBehaviour
{
    private const float MinimumGridSize = 0.01f;

    public static ObjectPlacer instance;

    [SerializeField] private float gridSize = 1f;
    [SerializeField] private ObjectTransformEditor transformEditor;

    private readonly List<PlacedObjectView> placedObjects = new();
    private GameObject ghost;
    private string currentPrefabID;
    private float previewRotation;
    private Vector3 previewScale = Vector3.one;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Debug.LogWarning($"중복 ObjectPlacer를 제거합니다: {name}");
            Destroy(gameObject);
            return;
        }

        instance = this;
    }

    private void Start()
    {
        if (Camera.main == null)
        {
            Debug.LogError("ObjectPlacer가 사용할 MainCamera를 찾지 못했습니다.", this);
            enabled = false;
            return;
        }

        if (transformEditor == null)
            transformEditor = GetComponent<ObjectTransformEditor>();
        if (transformEditor == null)
            Debug.LogError("ObjectPlacer와 함께 사용할 ObjectTransformEditor가 없습니다.", this);

        MapEditorPalette palette = MapEditorManager.instance?.palette;
        if (palette != null && palette.entries.Count > 0)
            OnPaletteChanged(palette.entries[0].id);
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    private void Update()
    {
        UpdateGhostPosition();
    }

    public void OnPaletteChanged(string prefabID)
    {
        transformEditor?.ExitDeleteMode();
        currentPrefabID = prefabID;
        ResetPlacementTransform();
        RecreateGhost(prefabID);
    }

    public void RebuildFromMapData(MapData mapData)
    {
        ResetTransientState();
        ClearDisplayObjects();
        if (mapData?.objects == null)
        {
            Debug.LogWarning("복원할 맵 오브젝트 데이터가 없습니다.");
            return;
        }

        foreach (PlacedObjectData data in mapData.objects)
            SpawnDisplayObject(data);
    }

    public void ClearDisplayObjects()
    {
        transformEditor?.ClearSelection();
        foreach (PlacedObjectView view in placedObjects)
        {
            if (view != null)
                Destroy(view.gameObject);
        }

        placedObjects.Clear();
    }

    public void ResetTransientState()
    {
        transformEditor?.ClearSelection();
        ResetPlacementTransform();
        RecreateGhost(currentPrefabID);
    }

    public void SetGhostVisible(bool isVisible)
    {
        if (ghost != null)
            ghost.SetActive(isVisible);
    }

    public void TryPlaceCurrent()
    {
        TryPlaceCurrent(GetSnappedWorldPosition());
    }

    public void TryPlaceCurrent(Vector3 position)
    {
        if (string.IsNullOrWhiteSpace(currentPrefabID)) return;

        RemoveObjectsAt(position);
        PlacedObjectData data = new PlacedObjectData(
            currentPrefabID,
            position,
            previewRotation,
            previewScale);
        SpawnDisplayObject(data);
        MapEditorManager.instance?.AddObject(data);
    }

    private void RemoveObjectsAt(Vector3 position)
    {
        for (int i = placedObjects.Count - 1; i >= 0; i--)
        {
            PlacedObjectView view = placedObjects[i];
            if (view == null)
            {
                placedObjects.RemoveAt(i);
                continue;
            }

            Vector3 placedPosition = view.Data?.position?.ToVector3() ?? view.transform.position;
            if (!IsSameGridPosition(placedPosition, position)) continue;

            transformEditor?.ClearSelection();
            placedObjects.RemoveAt(i);
            MapEditorManager.instance?.RemoveObject(view.Data);
            Destroy(view.gameObject);
        }
    }

    private bool IsSameGridPosition(Vector3 first, Vector3 second)
    {
        float validGridSize = Mathf.Max(gridSize, MinimumGridSize);
        return Mathf.RoundToInt(first.x / validGridSize) ==
               Mathf.RoundToInt(second.x / validGridSize) &&
               Mathf.RoundToInt(first.y / validGridSize) ==
               Mathf.RoundToInt(second.y / validGridSize);
    }

    public void RotatePreview(float rotationStep)
    {
        previewRotation = Mathf.Repeat(previewRotation + rotationStep, 360f);
        ApplyPreviewTransform();
    }

    public void FlipPreview()
    {
        previewScale.x *= -1f;
        ApplyPreviewTransform();
    }

    public void Remove(PlacedObjectView view)
    {
        if (view == null || !placedObjects.Remove(view))
        {
            Debug.LogWarning("삭제할 화면 오브젝트를 배치 목록에서 찾지 못했습니다.", this);
            return;
        }

        MapEditorManager.instance?.RemoveObject(view.Data);
        Destroy(view.gameObject);
    }

    public PlacedObjectView FindTopViewAt(Vector3 worldPosition)
    {
        for (int i = placedObjects.Count - 1; i >= 0; i--)
        {
            PlacedObjectView view = placedObjects[i];
            if (view != null && view.SpriteRenderer != null &&
                view.SpriteRenderer.bounds.Contains(worldPosition))
                return view;
        }

        return null;
    }

    public Vector3 GetSnappedWorldPosition()
    {
        Vector3 worldPos = GetWorldMousePosition();
        float validGridSize = Mathf.Max(gridSize, MinimumGridSize);
        return new Vector3(
            Mathf.Round(worldPos.x / validGridSize) * validGridSize,
            Mathf.Round(worldPos.y / validGridSize) * validGridSize,
            0f
        );
    }

    public Vector3 GetWorldMousePosition()
    {
        Camera mainCamera = Camera.main;
        if (mainCamera == null)
        {
            Debug.LogError("마우스 월드 좌표 변환에 필요한 MainCamera가 없습니다.", this);
            return Vector3.zero;
        }

        Vector3 pos = mainCamera.ScreenToWorldPoint(Input.mousePosition);
        pos.z = 0f;
        return pos;
    }

    private void SpawnDisplayObject(PlacedObjectData data)
    {
        MapEditorPalette palette = MapEditorManager.instance?.palette;
        if (!CanDisplay(data, palette, out PaletteEntry entry)) return;

        Sprite thumbnail = entry.thumbnail;
        if (thumbnail == null)
        {
            Debug.LogWarning($"썸네일이 없어 오브젝트를 표시할 수 없습니다. prefabID: {data.prefabID}");
            return;
        }

        GameObject obj = CreateSpriteObject(thumbnail, data);
        PlacedObjectView view = obj.AddComponent<PlacedObjectView>();
        view.Initialize(data);
        placedObjects.Add(view);
    }

    private bool CanDisplay(
        PlacedObjectData data,
        MapEditorPalette palette,
        out PaletteEntry entry)
    {
        entry = null;
        if (data == null || palette == null)
        {
            Debug.LogWarning("오브젝트 데이터 또는 팔레트가 없어 표시할 수 없습니다.");
            return false;
        }

        return palette.TryGetEntry(data.prefabID, out entry);
    }

    private void RecreateGhost(string prefabID)
    {
        if (ghost != null) Destroy(ghost);

        MapEditorPalette palette = MapEditorManager.instance?.palette;
        if (palette == null || !palette.TryGetEntry(prefabID, out PaletteEntry entry)) return;

        Sprite thumbnail = entry.thumbnail;
        if (thumbnail == null) return;

        ghost = CreateSpriteObject(
            thumbnail,
            GetSnappedWorldPosition(),
            previewRotation,
            previewScale);
        ghost.name = "Ghost";

        SpriteRenderer sr = ghost.GetComponent<SpriteRenderer>();
        Color c = sr.color;
        c.a = 0.5f;
        sr.color = c;
        sr.sortingOrder = 10;
    }

    private GameObject CreateSpriteObject(Sprite sprite, PlacedObjectData data)
    {
        Vector3 position = data.position?.ToVector3() ?? Vector3.zero;
        Vector3 scale = data.scale?.ToVector3() ?? Vector3.one;
        return CreateSpriteObject(sprite, position, data.rotation, scale);
    }

    private GameObject CreateSpriteObject(
        Sprite sprite,
        Vector3 position,
        float rotation,
        Vector3 scale)
    {
        GameObject obj = new GameObject("PlacedObject");
        obj.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, 0f, rotation));
        obj.transform.localScale = scale;

        SpriteRenderer sr = obj.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;

        return obj;
    }

    private void UpdateGhostPosition()
    {
        if (ghost == null) return;
        ghost.transform.position = GetSnappedWorldPosition();
    }

    private void ApplyPreviewTransform()
    {
        if (ghost == null)
        {
            Debug.LogWarning("회전 또는 반전을 표시할 배치 고스트가 없습니다.", this);
            return;
        }

        ghost.transform.SetPositionAndRotation(
            ghost.transform.position,
            Quaternion.Euler(0f, 0f, previewRotation));
        ghost.transform.localScale = previewScale;
    }

    private void ResetPlacementTransform()
    {
        previewRotation = 0f;
        previewScale = Vector3.one;
    }

    private void OnValidate()
    {
        if (gridSize < MinimumGridSize)
            gridSize = 1f;
    }
}
