using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MapSelectionManager : NetworkBehaviour
{
    public TextMeshProUGUI stageTitleText; // 선택된 스테이지 제목
    public Button[] mapButtons; // 6개의 맵 버튼

    public CustomRoomPlayer roomPlayer;
    private int currentChapter;

    // 맵 선택 초기화
    public void InitializeMapSelection(int chapter)
    {
        currentChapter = chapter;
        stageTitleText.text = $"Stage {chapter}"; // 스테이지 제목 설정

        for (int i = 0; i < mapButtons.Length; i++)
        {
            int stageNum = i + 1;
            mapButtons[i].GetComponentInChildren<TextMeshProUGUI>().text = $"{chapter}-{stageNum}";
            mapButtons[i].onClick.RemoveAllListeners();
            mapButtons[i].onClick.AddListener(() => LoadStageScene(chapter, stageNum));
        }
    }

    // 스테이지 씬 로드
    private void LoadStageScene(int chapter, int stage)
    {
        Debug.Log($"챕터 '{chapter}' 스테이지{stage} 로드 중...");
        roomPlayer.CmdStageSelect(chapter, stage);
    }

    // 씬 전환을 트리거하는 서버 명령

    // 뒤로 가기 버튼
    public void BackToStageSelection()
    {
        gameObject.SetActive(false); // 맵 선택 UI 비활성화
        FindAnyObjectByType<StageSelectUI>().gameObject.SetActive(true); // 스테이지 선택 UI 활성화
    }
    
}
