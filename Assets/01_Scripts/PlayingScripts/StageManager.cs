using System.Collections;
using Mirror;
using UnityEngine;

public class StageManager : NetworkBehaviour
{
    public static StageManager instance;

    [SerializeField] private OfficialMapCatalog officialMapCatalog;
    private MapCompletionTarget loadedCompletionTarget;
    [SerializeField] private MapEditorPalette palette;
    [SerializeField] private GameObject mapDataSyncPrefab;

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

        if (slimeRoomManager.currentMapKind == LobbyMapKind.Official)
            return TryLoadPrefabStage(slimeRoomManager.currentMapId, out error);
        if (slimeRoomManager.currentMapKind != LobbyMapKind.Custom)
        {
            error = "플레이할 맵이 선택되지 않았습니다.";
            return false;
        }

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
        if (officialMapCatalog == null)
        {
            error = "OfficialMapCatalog가 StageManager에 연결되어 있지 않습니다.";
            return false;
        }
        if (!officialMapCatalog.TryGet(mapId, out OfficialMapEntry entry))
        {
            error = $"카탈로그에서 기본 맵을 찾을 수 없습니다: mapId={mapId}";
            return false;
        }

        if (!MapCompletionTarget.TryCreateOfficial(entry.MapId, entry.CompletionRevision, out loadedCompletionTarget))
        {
            error = $"공식맵 완료 대상이 올바르지 않습니다: {mapId}";
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
        SetFallBoundary(FallBoundary.TryCreate(new[] { stageObject }, out FallBoundary boundary), boundary);
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
        if (!loader.TryLoad(mapData, mapId, contentHash, out error))
            return false;

        if (!MapCompletionTarget.TryCreateCustom(mapId, contentHash, out loadedCompletionTarget))
        {
            error = "커스텀맵 완료 대상이 올바르지 않습니다.";
            return false;
        }
        SetFallBoundary(FallBoundary.TryCreate(mapData.objects, palette, out FallBoundary boundary), boundary);
        return true;
    }

    [Server]
    private void SetFallBoundary(bool isCreated, FallBoundary boundary)
    {
        if (isCreated)
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
