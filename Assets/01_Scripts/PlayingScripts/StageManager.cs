using System.Collections;
using Mirror;
using UnityEngine;

public class StageManager : NetworkBehaviour
{
    public static StageManager instance;

    [SerializeField] private BuiltInMapCatalog builtInMapCatalog;
    [SerializeField] private MapEditorPalette palette;
    [SerializeField] private GameObject mapDataSyncPrefab;

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
        if (TryLoadStage(out string error))
            return;

        // 맵 없는 GamePlay 씬에 플레이어를 남겨 두지 않는다.
        Debug.LogError($"스테이지를 불러오지 못해 대기실로 돌아갑니다: {error}", this);
        StartCoroutine(ReturnToRoomNextFrame());
    }

    [Server]
    private bool TryLoadStage(out string error)
    {
        if (!(NetworkManager.singleton is SlimeRoomManager slimeRoomManager))
        {
            error = "SlimeRoomManager를 찾을 수 없습니다.";
            return false;
        }

        if (string.IsNullOrEmpty(slimeRoomManager.currentMapId))
            return TryLoadPrefabStage(slimeRoomManager.currentBuiltInMapId, out error);

        if (!slimeRoomManager.ServerMapSession.TryGet(
                slimeRoomManager.currentMapId,
                slimeRoomManager.currentMapContentHash,
                out MapData mapData))
        {
            error = "서버 세션 맵을 찾을 수 없습니다: " +
                    $"mapId={slimeRoomManager.currentMapId}, " +
                    $"contentHash={slimeRoomManager.currentMapContentHash}";
            return false;
        }

        return TryLoadCustomMap(
            mapData,
            slimeRoomManager.currentMapId,
            slimeRoomManager.currentMapContentHash,
            out error);
    }

    [Server]
    private bool TryLoadPrefabStage(string mapId, out string error)
    {
        if (builtInMapCatalog == null)
        {
            error = "BuiltInMapCatalog가 StageManager에 연결되어 있지 않습니다.";
            return false;
        }
        if (!builtInMapCatalog.TryGet(mapId, out BuiltInMapCatalog.Entry entry))
        {
            error = $"카탈로그에서 기본 맵을 찾을 수 없습니다: mapId={mapId}";
            return false;
        }

        GameObject stageObject = Instantiate(entry.StagePrefab);
        if (stageObject.GetComponent<NetworkIdentity>() == null)
        {
            Destroy(stageObject);
            error = $"스테이지 오브젝트에 NetworkIdentity 컴포넌트가 없습니다: mapId={mapId}";
            return false;
        }

        NetworkServer.Spawn(stageObject);
        error = null;
        return true;
    }

    [Server]
    private bool TryLoadCustomMap(
        MapData mapData,
        string mapId,
        string contentHash,
        out string error)
    {
        if (palette == null)
        {
            error = "MapEditorPalette가 StageManager에 연결되어 있지 않습니다.";
            return false;
        }

        CustomMapRuntimeLoader loader = new CustomMapRuntimeLoader(
            new PlacedObjectFactory(palette),
            mapDataSyncPrefab);
        return loader.TryLoad(mapData, mapId, contentHash, out error);
    }

    // OnStartServer는 씬 로드를 마무리하는 도중에 불리므로 같은 프레임에 씬을 바꾸지 않는다.
    private IEnumerator ReturnToRoomNextFrame()
    {
        yield return null;
        if (NetworkServer.active && NetworkManager.singleton is SlimeRoomManager roomManager)
            roomManager.ReturnRoomScene();
    }
}
