using UnityEngine;
using UnityEngine.SceneManagement;

public class MapEditorManager : MonoBehaviour
{
    private const string MainSceneName = "Main";

    public static MapEditorManager instance;

    public MapEditorPalette palette;

    public MapData CurrentMapData { get; private set; }
    public string SelectedPrefabID { get; private set; }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Debug.LogWarning($"중복 MapEditorManager를 제거합니다: {name}");
            Destroy(gameObject);
            return;
        }

        instance = this;
        NewMap("새 맵");
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    public void NewMap(string mapName)
    {
        CurrentMapData = new MapData(mapName, GetAuthorName());
    }

    public void SetMapName(string mapName)
    {
        if (CurrentMapData != null)
            CurrentMapData.mapName = mapName;
    }

    public void SelectPrefab(string prefabID)
    {
        SelectedPrefabID = prefabID;
    }

    public void AddObject(PlacedObjectData data)
    {
        CurrentMapData?.objects.Add(data);
    }

    public void RemoveObject(PlacedObjectData data)
    {
        CurrentMapData?.objects.Remove(data);
    }

    public void SaveMap()
    {
        if (CurrentMapData == null || string.IsNullOrEmpty(CurrentMapData.mapName))
        {
            Debug.LogWarning("맵 이름이 없습니다. 저장할 수 없습니다.");
            return;
        }
        MapDataRepository.Save(CurrentMapData);
        Debug.Log($"맵 저장 완료: {CurrentMapData.mapName}");
    }

    public void LoadMap(string mapName)
    {
        MapData loaded = MapDataRepository.Load(mapName);
        if (loaded == null) return;

        CurrentMapData = loaded;
        ObjectPlacer.instance?.RebuildFromMapData(loaded);
    }

    public void ReturnToMain()
    {
        if (!Application.CanStreamedLevelBeLoaded(MainSceneName))
        {
            Debug.LogError($"메인 씬을 불러올 수 없습니다. Build Settings에서 '{MainSceneName}' 씬이 활성화되어 있는지 확인하세요.", this);
            return;
        }

        SceneManager.LoadScene(MainSceneName);
    }

    private string GetAuthorName()
    {
        // 제작자 정보를 유지하려면 Main 씬에서 생존한 Steam 이름을 우선 사용해야 한다.
        if (SteamRoomManager.Instance != null)
            return SteamRoomManager.Instance.playerName;
        return "Unknown";
    }
}
