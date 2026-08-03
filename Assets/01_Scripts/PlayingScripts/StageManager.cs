using Mirror;
using UnityEngine;

public class StageManager : NetworkBehaviour
{
    public static StageManager instance;

    [SerializeField] private OfficialMapCatalog officialMapCatalog;
    public MapEditorPalette palette;
    public GameObject mapDataSyncPrefab;
    private MapCompletionTarget loadedCompletionTarget;

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

        switch (slimeRoomManager.currentMapKind)
        {
            case LobbyMapKind.Official:
                LoadOfficialMap(slimeRoomManager.currentMapId);
                return;
            case LobbyMapKind.Custom:
                LoadCustomMap(slimeRoomManager);
                return;
            case LobbyMapKind.None:
                Debug.LogError("플레이할 맵이 선택되지 않았습니다.");
                return;
            default:
                Debug.LogError($"알 수 없는 맵 종류입니다: {slimeRoomManager.currentMapKind}");
                return;
        }
    }

    [Server]
    private void LoadOfficialMap(string officialMapId)
    {
        if (officialMapCatalog == null)
        {
            Debug.LogError("OfficialMapCatalog가 StageManager에 연결되지 않았습니다.");
            return;
        }
        if (!OfficialMapId.TryNormalize(officialMapId, out string normalizedMapId) ||
            normalizedMapId != officialMapId)
        {
            Debug.LogError($"공식 MapId 형식이 올바르지 않습니다: {officialMapId}");
            return;
        }
        if (!officialMapCatalog.TryGet(officialMapId, out OfficialMapEntry entry))
        {
            Debug.LogError($"공식맵을 카탈로그에서 찾을 수 없습니다: {officialMapId}");
            return;
        }
        if (entry.StagePrefab == null)
        {
            Debug.LogError($"공식 스테이지 프리팹이 연결되지 않았습니다: {officialMapId}");
            return;
        }
        if (entry.StagePrefab.GetComponent<NetworkIdentity>() == null)
        {
            Debug.LogError($"공식 스테이지 프리팹에 NetworkIdentity가 없습니다: {officialMapId}");
            return;
        }

        GameObject stageObject = Instantiate(entry.StagePrefab);
        NetworkServer.Spawn(stageObject);
        if (!MapCompletionTarget.TryCreateOfficial(
                entry.MapId, entry.CompletionRevision, out loadedCompletionTarget))
        {
            Debug.LogError($"공식맵 완료 대상을 만들 수 없습니다: {entry.MapId}");
        }
    }

    [Server]
    private void LoadCustomMap(SlimeRoomManager roomManager)
    {
        if (!CustomMapId.TryNormalize(roomManager.currentMapId, out string normalizedMapId) ||
            normalizedMapId != roomManager.currentMapId ||
            string.IsNullOrEmpty(roomManager.currentMapContentHash))
        {
            Debug.LogError("커스텀 MapId 또는 ContentHash가 올바르지 않습니다.");
            return;
        }
        if (!roomManager.ServerMapSession.TryGet(
                roomManager.currentMapId,
                roomManager.currentMapContentHash,
                out MapData mapData))
        {
            Debug.LogError($"커스텀 세션 스냅샷이 일치하지 않습니다: mapId={roomManager.currentMapId}, contentHash={roomManager.currentMapContentHash}");
            return;
        }

        if (LoadFromMapData(mapData) &&
            !MapCompletionTarget.TryCreateCustom(
                roomManager.currentMapId,
                roomManager.currentMapContentHash,
                out loadedCompletionTarget))
        {
            Debug.LogError($"커스텀맵 완료 대상을 만들 수 없습니다: {roomManager.currentMapId}");
        }
    }

    [Server]
    private bool LoadFromMapData(MapData mapData)
    {
        if (mapData == null)
        {
            Debug.LogError("MapData가 null입니다. 맵을 불러올 수 없습니다.");
            return false;
        }

        string json = MapDataRepository.ToJson(mapData);
        if (string.IsNullOrEmpty(json))
        {
            Debug.LogError("커스텀 맵 데이터를 동기화용 JSON으로 변환할 수 없습니다.");
            return false;
        }

        SlimeRoomManager roomManager = (SlimeRoomManager)NetworkManager.singleton;
        if (!SpawnMapDataSync(
                roomManager.currentMapId,
                roomManager.currentMapContentHash))
            return false;

        foreach (PlacedObjectData objData in mapData.objects)
        {
            if (!SpawnPlacedObject(objData))
                return false;
        }

        return true;
    }

    [Server]
    private bool SpawnMapDataSync(string mapId, string contentHash)
    {
        if (mapDataSyncPrefab == null)
        {
            Debug.LogError("MapDataNetworkSync 프리팹이 StageManager에 연결되어 있지 않습니다.");
            return false;
        }

        GameObject syncObj = Instantiate(mapDataSyncPrefab);
        if (syncObj.GetComponent<NetworkIdentity>() == null)
        {
            Debug.LogError("MapDataNetworkSync 프리팹에 NetworkIdentity가 없습니다.", syncObj);
            Destroy(syncObj);
            return false;
        }

        MapDataNetworkSync mapDataSync = syncObj.GetComponent<MapDataNetworkSync>();
        if (mapDataSync == null)
        {
            Debug.LogError("MapDataNetworkSync 컴포넌트가 프리팹에 없습니다.", syncObj);
            Destroy(syncObj);
            return false;
        }

        mapDataSync.SetManifest(mapId, contentHash);
        NetworkServer.Spawn(syncObj);
        return true;
    }

    [Server]
    private bool SpawnPlacedObject(PlacedObjectData objData)
    {
        if (palette == null)
        {
            Debug.LogError("MapEditorPalette가 StageManager에 연결되어 있지 않습니다.");
            return false;
        }

        GameObject prefab = palette.GetPrefab(objData.prefabID);
        if (prefab == null)
        {
            Debug.LogError($"팔레트 프리팹을 찾을 수 없습니다: {objData.prefabID}");
            return false;
        }

        Vector3 position = objData.position.ToVector3();
        Quaternion rotation = Quaternion.Euler(0f, 0f, objData.rotation);

        GameObject obj = Instantiate(prefab, position, rotation);
        obj.transform.localScale = objData.scale.ToVector3();

        NetworkIdentity identity = obj.GetComponent<NetworkIdentity>();
        if (identity == null)
        {
            Debug.LogError($"팔레트 프리팹에 NetworkIdentity가 없습니다: {objData.prefabID}", obj);
            Destroy(obj);
            return false;
        }

        NetworkServer.Spawn(obj);
        return true;
    }

    [Server]
    public bool TryGetLoadedCompletionTarget(out MapCompletionTarget target)
    {
        target = default;
        if (!loadedCompletionTarget.IsValid ||
            NetworkManager.singleton is not SlimeRoomManager roomManager ||
            roomManager.GameplayCompletionSession == null ||
            roomManager.GameplayCompletionSession.MapKey != loadedCompletionTarget.MapKey ||
            roomManager.currentMapKind != loadedCompletionTarget.MapKey.Kind ||
            roomManager.currentMapId != loadedCompletionTarget.MapKey.MapId)
            return false;

        if (loadedCompletionTarget.MapKey.Kind == LobbyMapKind.Official)
        {
            if (officialMapCatalog == null ||
                !officialMapCatalog.TryGet(
                    loadedCompletionTarget.MapKey.MapId, out OfficialMapEntry entry) ||
                !MapCompletionTarget.TryCreateOfficial(
                    entry.MapId, entry.CompletionRevision, out target))
                return false;

            return target.Equals(loadedCompletionTarget);
        }

        if (roomManager.currentMapContentHash != loadedCompletionTarget.Revision ||
            roomManager.GameplayCompletionSession.CustomContentHash !=
                loadedCompletionTarget.Revision ||
            !roomManager.ServerMapSession.TryGet(
                loadedCompletionTarget.MapKey.MapId,
                loadedCompletionTarget.Revision,
                out _))
            return false;

        target = loadedCompletionTarget;
        return true;
    }
}
