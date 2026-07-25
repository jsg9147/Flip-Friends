using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MapSelectionManager : NetworkBehaviour
{
    public GameObject mapSelectScreen;
    public StageSelectBtnEvent stageSelectBtnEvent;

    public Button[] mapButtons; // �� ��ư

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
        if (!isServer)
            return;

        roomPlayer.CmdStageSelect(stage);
    }

    // 방장이 커스텀 맵을 선택했을 때 호출 — mapJson은 MapDataRepository.ToJson() 결과
    public void CustomMapLoad(string mapJson)
    {
        if (!isServer)
            return;

        roomPlayer.CmdCustomMapSelect(mapJson);
    }

    private void AddButtonEvent()
    {
        for (int i = 0; i < mapButtons.Length; i++)
        {
            int index = i; // ���ο� ���� ������ i ���� ����
            mapButtons[i].onClick.AddListener(() => StageLoad(index));
        }
    }
}
