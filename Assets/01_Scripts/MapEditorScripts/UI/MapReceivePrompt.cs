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
        MapData cachedMap = MapDataNetworkSync.instance.GetMapData();
        if (cachedMap != null)
            ShowPrompt(cachedMap);
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

        if (MapDataRepository.TryLoadById(
                pendingMapData.mapId,
                out string existingFileName,
                out MapData _,
                out string _,
                out MapRepositoryFailure lookupFailure))
        {
            promptText.text = $"같은 MapId의 맵이 이미 저장되어 있습니다: {existingFileName}";
            Debug.LogWarning($"수신 맵 저장을 건너뛰었습니다. 같은 MapId가 이미 있습니다: {pendingMapData.mapId}");
            return;
        }

        if (lookupFailure.Kind == MapRepositoryFailureKind.DuplicateMapId)
        {
            promptText.text = "같은 MapId의 로컬 파일이 여러 개라 저장할 수 없습니다.";
            Debug.LogWarning(lookupFailure.Message);
            return;
        }

        if (!MapDataRepository.TryExists(pendingMapData.mapName, out bool nameExists))
            return;
        if (nameExists)
        {
            promptText.text = "같은 이름의 다른 맵이 있어 자동으로 덮어쓸 수 없습니다.";
            Debug.LogWarning($"수신 맵 이름 충돌로 저장을 중단했습니다: {pendingMapData.mapName}");
            return;
        }

        if (!MapDataRepository.TryImport(
                pendingMapData,
                out MapSaveResult saveResult))
        {
            promptText.text = saveResult.Message;
            return;
        }

        promptPanel.SetActive(false);
        pendingMapData = null;
    }

    public void OnCancelSave()
    {
        promptPanel.SetActive(false);
        pendingMapData = null;
    }
}
