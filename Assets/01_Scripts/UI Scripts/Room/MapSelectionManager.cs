using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MapSelectionManager : NetworkBehaviour
{
    public GameObject mapSelectScreen;
    public StageSelectBtnEvent stageSelectBtnEvent;

    public Button[] mapButtons; // 맵 버튼

    public CustomRoomPlayer roomPlayer;
    private int currentChapter;

    private void Start()
    {
        AddButtonEvent();
    }

    public void MapSelectScreenSetActive(bool isActive)
    {
        mapSelectScreen.SetActive(isActive);
        stageSelectBtnEvent.ButtonInit();
    }

    public void StageLoad(int stage)
    {
        if (isServer)
        {
            Debug.Log($"스테이지{stage} 로드 중...");
            roomPlayer.CmdStageSelect(stage);
        }
        else
        {
            Debug.Log("방장만 맵을 선택할 수 있습니다.");
        }
    }

    private void AddButtonEvent()
    {
        for (int i = 0; i < mapButtons.Length; i++)
        {
            int index = i; // 새로운 로컬 변수로 i 값을 저장
            mapButtons[i].onClick.AddListener(() => StageLoad(index));
        }
    }
}
