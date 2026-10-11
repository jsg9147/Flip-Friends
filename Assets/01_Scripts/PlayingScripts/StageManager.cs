using System.Collections;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

public class StageManager : NetworkBehaviour
{
    public static StageManager instance;

    [SerializeField] private BuiltInMapCatalog builtInMapCatalog;
    [SerializeField] private MapEditorPalette palette;
    [SerializeField] private GameObject mapDataSyncPrefab;
    // 스테이지 그림 범위에서 이만큼 더 벗어나야 추락으로 본다. 끝자락에서 뛰는 정상 플레이를 잘못 잡지 않게 넉넉히 둔다.
    [SerializeField] private float fallBoundaryMargin = 10f;

    // 서버에서만 값이 있다. 스테이지 그림이 하나도 없으면 null이며, 그때는 추락 판정을 하지 않는다.
    public FallBoundary FallBoundary { get; private set; }

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
        StartCoroutine(ReturnToRoomNextFrame(error));
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
        CreateFallBoundary(new[] { stageObject });
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
        if (!loader.TryLoad(mapData, mapId, contentHash, out IReadOnlyList<GameObject> spawnedObjects, out error))
            return false;

        CreateFallBoundary(spawnedObjects);
        return true;
    }

    [Server]
    private void CreateFallBoundary(IEnumerable<GameObject> stageObjects)
    {
        if (FallBoundary.TryCreate(stageObjects, fallBoundaryMargin, out FallBoundary boundary))
            FallBoundary = boundary;
        else
            Debug.LogWarning("스테이지에 그림이 없어 추락 판정 영역을 만들지 못했습니다. 추락해도 리스폰되지 않습니다.", this);
    }

    // OnStartServer는 씬 로드를 마무리하는 도중에 불리므로 같은 프레임에 씬을 바꾸지 않는다.
    private IEnumerator ReturnToRoomNextFrame(string reason)
    {
        yield return null;
        if (NetworkServer.active && NetworkManager.singleton is SlimeRoomManager roomManager)
            roomManager.ReturnRoomSceneAfterLoadFailure(reason);
    }
}
