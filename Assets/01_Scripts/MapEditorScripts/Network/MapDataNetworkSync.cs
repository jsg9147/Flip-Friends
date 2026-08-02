using Mirror;
using UnityEngine;

public class MapDataNetworkSync : NetworkBehaviour
{
    public static MapDataNetworkSync instance;

    // 클라이언트에서 맵 데이터 수신 시 호출 — MapReceivePrompt 등이 구독
    public event System.Action<MapData> OnMapDataReceived;

    [SyncVar(hook = nameof(OnManifestValueChanged))]
    private string mapId = string.Empty;
    [SyncVar(hook = nameof(OnManifestValueChanged))]
    private string contentHash = string.Empty;

    private void Awake()
    {
        instance = this;
    }

    private void OnDestroy()
    {
        instance = null;
    }

    [Server]
    public void SetManifest(string valueMapId, string valueContentHash)
    {
        mapId = valueMapId;
        contentHash = valueContentHash;
    }

    public MapData GetMapData()
    {
        return MapSessionCache.TryGet(mapId, contentHash, out MapData data)
            ? data
            : null;
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        PublishCachedMap();
    }

    private void OnManifestValueChanged(string oldValue, string newValue) =>
        PublishCachedMap();

    private void PublishCachedMap()
    {
        MapData mapData = GetMapData();
        if (mapData != null)
            OnMapDataReceived?.Invoke(mapData);
    }
}
