using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 스테이지를 불러오지 못해 GameRoom으로 돌아왔을 때 실패 사유를 보여 준다.
// 맵 선택 화면은 이때 닫혀 있으므로 별도 패널로 띄운다.
public class StageLoadFailureNotice : MonoBehaviour
{
    private const float DefaultDisplaySeconds = 8f;

    [SerializeField] private GameObject panel;
    [SerializeField] private TMP_Text messageText;
    [SerializeField] private Button closeButton;
    [SerializeField, Min(1f)] private float displaySeconds = DefaultDisplaySeconds;

    private float hideAt;

    private void Awake()
    {
        if (closeButton != null)
            closeButton.onClick.AddListener(Hide);
        Hide();
    }

    // Submit·Cancel은 GameRoom에서 준비·방 나가기에 쓰이므로 닫기는 버튼과 시간 경과로만 한다.
    private void Update()
    {
        if (panel != null && panel.activeSelf && Time.unscaledTime >= hideAt)
            Hide();
    }

    public void Show(string reason)
    {
        if (panel == null || messageText == null)
        {
            Debug.LogWarning($"로드 실패 알림 UI가 연결되지 않아 로그로만 남깁니다: {reason}", this);
            return;
        }

        messageText.text = $"맵을 불러오지 못해 대기실로 돌아왔습니다.\n{reason}";
        panel.SetActive(true);
        hideAt = Time.unscaledTime + displaySeconds;
    }

    private void Hide()
    {
        if (panel != null)
            panel.SetActive(false);
    }
}
