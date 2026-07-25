using Mirror;
using UnityEngine;

public class PlayerSpawnPoint : MonoBehaviour
{
    [SerializeField] private SpriteRenderer markerRenderer;

    private void Start()
    {
        if (!NetworkServer.active && !NetworkClient.active)
            return;

        if (markerRenderer == null)
        {
            Debug.LogWarning($"시작 지점 마커 Renderer가 연결되어 있지 않습니다: {name}", this);
            return;
        }

        // 플레이 위치 표시는 에디터에서만 필요하므로 네트워크 플레이 중에는 숨긴다.
        markerRenderer.enabled = false;
    }
}
