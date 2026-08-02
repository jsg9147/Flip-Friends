using System;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MapEditorManager : MonoBehaviour
{
    private const string MainSceneName = "Main";

    public static MapEditorManager instance;

    public MapEditorPalette palette;

    public MapData CurrentMapData { get; private set; }
    public string SelectedPrefabID { get; private set; }
    public event Action MapDataChanged;

    private string currentFileName;

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

    public bool NewMap(string mapName)
    {
        if (!TrySetMapName(mapName, out string normalizedName)) return false;

        ObjectPlacer.instance?.ResetTransientState();
        CurrentMapData = new MapData(normalizedName, GetAuthorName());
        CurrentMapData.mapId = MapDataRepository.CreateUniqueMapId();
        currentFileName = null;
        MapDataChanged?.Invoke();
        return true;
    }

    public bool SetMapName(string mapName)
    {
        if (!TrySetMapName(mapName, out string normalizedName)) return false;
        if (CurrentMapData == null)
        {
            Debug.LogWarning("이름을 변경할 현재 맵 데이터가 없습니다.", this);
            return false;
        }

        CurrentMapData.mapName = normalizedName;
        return true;
    }

    public void SelectPrefab(string prefabID)
    {
        SelectedPrefabID = prefabID;
    }

    public void AddObject(PlacedObjectData data)
    {
        if (CurrentMapData?.objects == null || data == null) return;

        CurrentMapData.objects.Add(data);
        MapDataChanged?.Invoke();
    }

    public void RemoveObject(PlacedObjectData data)
    {
        if (CurrentMapData?.objects?.Remove(data) != true) return;

        MapDataChanged?.Invoke();
    }

    public bool SaveMap(
        bool replaceExistingFile,
        out MapSaveResult result)
    {
        if (CurrentMapData == null)
        {
            Debug.LogWarning("저장할 현재 맵 데이터가 없습니다.", this);
            result = MapSaveResult.Failure(
                MapSaveFailureKind.MissingData,
                "저장할 현재 맵 데이터가 없습니다.");
            return false;
        }

        bool saved = currentFileName == null
            ? MapDataRepository.TryCreate(
                CurrentMapData,
                replaceExistingFile,
                out result)
            : MapDataRepository.TryUpdate(
                currentFileName,
                CurrentMapData,
                out result);
        if (!saved) return false;

        currentFileName = CurrentMapData.mapName;
        Debug.Log($"맵 저장 완료: {CurrentMapData.mapName}");
        return true;
    }

    public MapValidationReport ValidateCurrentMap()
    {
        var validator = new MapDataValidator(palette);
        return validator.Validate(CurrentMapData);
    }

    public bool CanStartTestPlay(out MapValidationReport report)
    {
        report = ValidateCurrentMap();
        return report.CanStartPlay;
    }

    public void LoadMap(string mapName)
    {
        MapData loaded = MapDataRepository.Load(mapName);
        if (loaded == null) return;

        ObjectPlacer.instance?.ResetTransientState();
        CurrentMapData = loaded;
        currentFileName = mapName;
        ObjectPlacer.instance?.RebuildFromMapData(loaded);
        MapDataChanged?.Invoke();
    }

    public void ReturnToMain()
    {
        ObjectPlacer.instance?.ResetTransientState();
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

    private bool TrySetMapName(string mapName, out string normalizedName)
    {
        if (MapDataRepository.TryNormalizeMapName(
                mapName,
                out normalizedName,
                out string error))
            return true;

        Debug.LogWarning($"맵 이름을 사용할 수 없습니다: {error}", this);
        return false;
    }
}
