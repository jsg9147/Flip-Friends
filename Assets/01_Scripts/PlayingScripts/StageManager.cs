using Mirror;
using System.Collections.Generic;
using UnityEngine;

public class StageManager : NetworkBehaviour
{
    public static StageManager instance;

    public List<GameObject> stageMapPrefabs;
    public MapEditorPalette palette;
    public GameObject mapDataSyncPrefab;

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
    }

    private void OnDestroy()
    {
        instance = null;
    }

    private void Start()
    {
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlayBGM(2);
        }
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        StageLoad();
    }

    [Server]
    public void StageLoad()
    {
        SlimeRoomManager slimeRoomManager = (SlimeRoomManager)NetworkManager.singleton;
        if (slimeRoomManager == null)
            return;

        if (!string.IsNullOrEmpty(slimeRoomManager.currentMapData))
        {
            MapData mapData = MapDataRepository.FromJson(slimeRoomManager.currentMapData);
            LoadFromMapData(mapData);
        }
        else
        {
            LoadPrefabStage(slimeRoomManager.currentStage);
        }
    }

    [Server]
    private void LoadPrefabStage(int stage)
    {
        GameObject stageObject = Instantiate(stageMapPrefabs[stage]);

        NetworkIdentity stageIdentity = stageObject.GetComponent<NetworkIdentity>();
        if (stageIdentity != null)
        {
            NetworkServer.Spawn(stageObject);
        }
        else
        {
            Debug.LogError("스테이지 오브젝트에 NetworkIdentity 컴포넌트가 없습니다.");
        }
    }

    [Server]
    private void LoadFromMapData(MapData mapData)
    {
        if (mapData == null)
        {
            Debug.LogError("MapData가 null입니다. 맵을 불러올 수 없습니다.");
            return;
        }

        SpawnMapDataSync(MapDataRepository.ToJson(mapData));

        foreach (PlacedObjectData objData in mapData.objects)
        {
            SpawnPlacedObject(objData);
        }
    }

    [Server]
    private void SpawnMapDataSync(string json)
    {
        if (mapDataSyncPrefab == null)
        {
            Debug.LogError("MapDataNetworkSync 프리팹이 StageManager에 연결되어 있지 않습니다.");
            return;
        }

        GameObject syncObj = Instantiate(mapDataSyncPrefab);
        if (syncObj.GetComponent<NetworkIdentity>() == null)
        {
            Debug.LogError("MapDataNetworkSync 프리팹에 NetworkIdentity가 없습니다.", syncObj);
            Destroy(syncObj);
            return;
        }

        NetworkServer.Spawn(syncObj);
        MapDataNetworkSync mapDataSync = syncObj.GetComponent<MapDataNetworkSync>();
        if (mapDataSync == null)
        {
            Debug.LogError("MapDataNetworkSync 컴포넌트가 프리팹에 없습니다.", syncObj);
            NetworkServer.Destroy(syncObj);
            return;
        }

        mapDataSync.SetMapData(json);
    }

    [Server]
    private void SpawnPlacedObject(PlacedObjectData objData)
    {
        if (palette == null)
        {
            Debug.LogError("MapEditorPalette가 StageManager에 연결되어 있지 않습니다.");
            return;
        }

        GameObject prefab = palette.GetPrefab(objData.prefabID);
        if (prefab == null)
            return;

        Vector3 position = objData.position.ToVector3();
        Quaternion rotation = Quaternion.Euler(0f, 0f, objData.rotation);

        GameObject obj = Instantiate(prefab, position, rotation);
        obj.transform.localScale = objData.scale.ToVector3();

        NetworkIdentity identity = obj.GetComponent<NetworkIdentity>();
        if (identity == null)
        {
            Debug.LogError($"팔레트 프리팹에 NetworkIdentity가 없습니다: {objData.prefabID}", obj);
            Destroy(obj);
            return;
        }

        NetworkServer.Spawn(obj);
    }
}
