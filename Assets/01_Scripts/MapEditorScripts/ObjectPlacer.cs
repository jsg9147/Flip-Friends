using System.Collections.Generic;
using UnityEngine;

public class ObjectPlacer : MonoBehaviour
{
    public static ObjectPlacer instance;

    [SerializeField] private float gridSize = 1f;

    private readonly List<(PlacedObjectData data, GameObject obj)> placedObjects = new();
    private GameObject ghost;
    private string currentPrefabID;

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
        HandlePlaceInput();
        HandleRemoveInput();
    }

    public void OnPaletteChanged(string prefabID)
    {
        currentPrefabID = prefabID;
        RecreateGhost(prefabID);
    }

    public void RebuildFromMapData(MapData mapData)
    {
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
        foreach (var (_, obj) in placedObjects)
            Destroy(obj);

        placedObjects.Clear();
    }

    private void UpdateGhostPosition()
    {
        if (ghost == null) return;
        ghost.transform.position = GetSnappedWorldPosition();
    }

    private void HandlePlaceInput()
    {
        if (!Input.GetMouseButtonDown(0)) return;
        if (IsPointerOverUI()) return;
        if (string.IsNullOrWhiteSpace(currentPrefabID)) return;

        Vector3 pos = GetSnappedWorldPosition();
        PlacedObjectData data = new PlacedObjectData(currentPrefabID, pos, 0f, Vector3.one);
        SpawnDisplayObject(data);
        MapEditorManager.instance?.AddObject(data);
    }

    private void HandleRemoveInput()
    {
        if (!Input.GetMouseButtonDown(1)) return;

        Vector3 worldPos = GetWorldMousePosition();

        for (int i = placedObjects.Count - 1; i >= 0; i--)
        {
            var (data, obj) = placedObjects[i];
            SpriteRenderer sr = obj.GetComponent<SpriteRenderer>();
            if (sr != null && sr.bounds.Contains(worldPos))
            {
                Destroy(obj);
                MapEditorManager.instance?.RemoveObject(data);
                placedObjects.RemoveAt(i);
                return;
            }
        }
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
        placedObjects.Add((data, obj));
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

        ghost = CreateSpriteObject(thumbnail, GetSnappedWorldPosition(), 0f, Vector3.one);
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

    private Vector3 GetSnappedWorldPosition()
    {
        Vector3 worldPos = GetWorldMousePosition();
        return new Vector3(
            Mathf.Round(worldPos.x / gridSize) * gridSize,
            Mathf.Round(worldPos.y / gridSize) * gridSize,
            0f
        );
    }

    private Vector3 GetWorldMousePosition()
    {
        Vector3 pos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        pos.z = 0f;
        return pos;
    }

    private bool IsPointerOverUI()
    {
        return UnityEngine.EventSystems.EventSystem.current != null &&
               UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject();
    }
}
