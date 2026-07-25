using System.Collections;
using Mirror;
using TMPro;
using UnityEngine;

// GamePlay 씬에서 클라이언트(비방장)에게 수신된 커스텀 맵 로컬 저장 여부를 묻는 프롬프트
public class MapReceivePrompt : MonoBehaviour
{
    [SerializeField] private GameObject promptPanel;
    [SerializeField] private TMP_Text promptText;

    private MapData pendingMapData;

    private void Start()
    {
        promptPanel.SetActive(false);
        StartCoroutine(SubscribeWhenReady());
    }

    private void OnDestroy()
    {
        if (MapDataNetworkSync.instance != null)
            MapDataNetworkSync.instance.OnMapDataReceived -= ShowPrompt;
    }

    // MapDataNetworkSync는 StageManager가 Spawn한 이후에 instance가 설정되므로 대기
    private IEnumerator SubscribeWhenReady()
    {
        while (MapDataNetworkSync.instance == null)
            yield return null;

        // 방장(서버)은 자신이 만든 맵이므로 저장 프롬프트 불필요
        if (NetworkServer.active)
            yield break;

        MapDataNetworkSync.instance.OnMapDataReceived += ShowPrompt;
    }

    private void ShowPrompt(MapData mapData)
    {
        pendingMapData = mapData;
        promptText.text = $"\"{mapData.mapName}\" 맵을 로컬에 저장하시겠습니까?";
        promptPanel.SetActive(true);
    }

    public void OnConfirmSave()
    {
        if (pendingMapData == null) return;

        if (!MapDataRepository.Save(pendingMapData)) return;

        promptPanel.SetActive(false);
        pendingMapData = null;
    }

    public void OnCancelSave()
    {
        promptPanel.SetActive(false);
        pendingMapData = null;
    }
}
