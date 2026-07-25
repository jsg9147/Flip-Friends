using Mirror;
using UnityEngine;

// 서버가 생성한 맵 JSON을 모든 클라이언트에 SyncVar로 전파
// 클라이언트가 맵 이름 표시, 로컬 저장 등에 활용
public class MapDataNetworkSync : NetworkBehaviour
{
    public static MapDataNetworkSync instance;

    // 클라이언트에서 맵 데이터 수신 시 호출 — MapReceivePrompt 등이 구독
    public event System.Action<MapData> OnMapDataReceived;

    [SyncVar(hook = nameof(OnSyncedMapJsonChanged))]
    public string syncedMapJson = string.Empty;

    private void Awake()
    {
        instance = this;
    }

    private void OnDestroy()
    {
        instance = null;
    }

    [Server]
    public void SetMapData(string json)
    {
        syncedMapJson = json;
    }

    public MapData GetMapData()
    {
        if (string.IsNullOrEmpty(syncedMapJson))
            return null;

        return MapDataRepository.FromJson(syncedMapJson);
    }

    // SyncVar hook — 클라이언트에서 값이 변경될 때 자동 호출
    private void OnSyncedMapJsonChanged(string oldJson, string newJson)
    {
        if (string.IsNullOrEmpty(newJson)) return;

        MapData mapData = MapDataRepository.FromJson(newJson);
        if (mapData != null)
            OnMapDataReceived?.Invoke(mapData);
    }
}
