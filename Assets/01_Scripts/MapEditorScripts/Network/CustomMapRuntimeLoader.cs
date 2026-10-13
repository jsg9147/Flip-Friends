using System.Collections.Generic;
using Mirror;
using UnityEngine;

// 서버 세션의 커스텀 맵 하나를 GamePlay 씬에 생성한다.
// 어떤 맵을 고를지는 StageManager가 정하고, 이 클래스는 생성 순서와 실패 시 되돌리기만 맡는다.
public sealed class CustomMapRuntimeLoader
{
    private readonly PlacedObjectFactory objectFactory;
    private readonly GameObject mapDataSyncPrefab;

    public CustomMapRuntimeLoader(PlacedObjectFactory objectFactory, GameObject mapDataSyncPrefab)
    {
        this.objectFactory = objectFactory;
        this.mapDataSyncPrefab = mapDataSyncPrefab;
    }

    // 일부만 생성된 맵은 플레이할 수 없으므로, 하나라도 실패하면 이미 생성한 것을 모두 지운다.
    public bool TryLoad(MapData mapData, string mapId, string contentHash, out string error)
    {
        if (mapData == null)
        {
            error = "MapData가 null입니다.";
            return false;
        }

        // 씬의 시작 위치는 기본 스테이지용이다. 남겨 두면 RoundRobin이 커스텀 맵에 배치한
        // 시작 지점보다 먼저 골라 플레이어가 맵과 무관한 곳에서 생성된다.
        Transform[] sceneStartPositions = NetworkManager.startPositions.ToArray();
        foreach (Transform startPosition in sceneStartPositions)
            NetworkManager.UnRegisterStartPosition(startPosition);

        List<GameObject> spawnedObjects = new List<GameObject>();

        if (!TrySpawnMapDataSync(mapId, contentHash, out GameObject syncObj, out error))
        {
            Rollback(spawnedObjects, sceneStartPositions);
            return false;
        }
        spawnedObjects.Add(syncObj);

        foreach (PlacedObjectData objData in mapData.objects)
        {
            GameObject obj = objectFactory.Spawn(objData);
            if (obj == null)
            {
                error = $"배치 오브젝트를 생성하지 못했습니다: prefabID={objData.prefabID}";
                Rollback(spawnedObjects, sceneStartPositions);
                return false;
            }
            spawnedObjects.Add(obj);
        }

        error = null;
        return true;
    }

    private static void Rollback(List<GameObject> spawnedObjects, Transform[] sceneStartPositions)
    {
        foreach (GameObject obj in spawnedObjects)
        {
            if (obj != null)
                NetworkServer.Destroy(obj);
        }

        foreach (Transform startPosition in sceneStartPositions)
        {
            if (startPosition != null)
                NetworkManager.RegisterStartPosition(startPosition);
        }
    }

    private bool TrySpawnMapDataSync(
        string mapId,
        string contentHash,
        out GameObject syncObj,
        out string error)
    {
        syncObj = null;
        if (mapDataSyncPrefab == null)
        {
            error = "MapDataNetworkSync 프리팹이 StageManager에 연결되어 있지 않습니다.";
            return false;
        }

        GameObject instance = Object.Instantiate(mapDataSyncPrefab);
        MapDataNetworkSync mapDataSync = instance.GetComponent<MapDataNetworkSync>();
        if (instance.GetComponent<NetworkIdentity>() == null || mapDataSync == null)
        {
            Object.Destroy(instance);
            error = "MapDataNetworkSync 프리팹에 NetworkIdentity 또는 MapDataNetworkSync 컴포넌트가 없습니다.";
            return false;
        }

        mapDataSync.SetManifest(mapId, contentHash);
        NetworkServer.Spawn(instance);
        syncObj = instance;
        error = null;
        return true;
    }
}
