using UnityEngine;
using UnityEngine.UI;
using Mirror;
using TMPro;

public class StageSelectUI : NetworkBehaviour
{
    public GameObject stageSelectionUI;
    public GameObject mapSelectionUI; // 맵 선택 UI 패널
    public MapSelectionManager mapSelectionManager; // 맵 선택 관리자

    public Button[] stageBtns; // 6개의 맵 버튼

    public TMP_Text currentPlayerCountText;

    private void Start()
    {
        InitializeStageSelectBtn();
    }

    public void InitializeStageSelectBtn()
    {
        int clearChapter = PlayerPrefs.GetInt("Chapter", 0);

        for (int i = 0; i < stageBtns.Length; i++)
        {
            stageBtns[i].onClick.RemoveAllListeners();
            //stageBtns[i].interactable = clearChapter >= i;
            stageBtns[i].interactable = true;
        }
    }

    public void OnStageButtonClicked(int stageNumber)
    {
        // 방장만 맵을 선택할 수 있도록 isServer로 확인
        if (isServer)
        {
            Debug.Log($"Stage {stageNumber} 선택됨");
            OpenMapSelection(stageNumber);
        }
        else
        {
            Debug.Log("방장만 맵을 선택할 수 있습니다.");
        }
    }

    // 서버에서 맵 선택 화면 열기
    private void OpenMapSelection(int stageNumber)
    {
        mapSelectionUI.SetActive(true); // 맵 선택 UI 활성화
        mapSelectionManager.InitializeMapSelection(stageNumber); // 선택된 스테이지에 해당하는 맵 선택 초기화
        stageSelectionUI.SetActive(false); // 스테이지 선택 UI 비활성화

        RpcShowMapSelectionUI(stageNumber); // 모든 클라이언트에 전환
    }

    // 모든 클라이언트에서 맵 선택 UI 표시
    [ClientRpc]
    void RpcShowMapSelectionUI(int stageNumber)
    {
        stageSelectionUI.SetActive(false);
        mapSelectionUI.SetActive(true);
        mapSelectionManager.InitializeMapSelection(stageNumber);
    }

    public void StageSelectionUISetActive(bool isAcitve)
    {
        stageSelectionUI.SetActive(isAcitve);
        if(isServer)
            RpcCurrentPlayerCountUpdate(NetworkManager.singleton.numPlayers);
    }

    [ClientRpc]
    private void RpcCurrentPlayerCountUpdate(int count)
    {
        currentPlayerCountText.text = $"{count} / 4";
    }
}
