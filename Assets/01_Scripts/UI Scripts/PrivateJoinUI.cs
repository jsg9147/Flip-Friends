using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PrivateJoinUI : MonoBehaviour
{
    public GameObject gameModeUI;
    public TMP_InputField joinCodeInputField;
    public Button joinButton;

    void Start()
    {
        if (joinCodeInputField != null)
        {
            joinCodeInputField.onValueChanged.AddListener(delegate { JoinButtonEnable(); });
            TextUpperChange();
        }
        JoinButtonEnable(); // 초기 상태 설정
    }

    void JoinButtonEnable()
    {
        // 입력 필드의 길이가 8이고, 모든 문자가 숫자인 경우 버튼 활성화
        if (joinCodeInputField.text.Length == 8)
        {
            joinButton.interactable = true;
        }
        else
        {
            joinButton.interactable = false;
        }
    }

    void TextUpperChange()
    {
        joinCodeInputField.text = joinCodeInputField.text.ToUpper();
    }


    bool IsAllDigits(string text)
    {
        foreach (char c in text)
        {
            if (!char.IsDigit(c))
            {
                return false;
            }
        }
        return true;
    }

    public void JoinSteamLobby()
    {
        MirrorRoomManager.Instance.JoinPrivateLobby(joinCodeInputField.text);
    }

    public void ExitJoinUI()
    {
        gameModeUI.gameObject.SetActive(true);
        gameObject.SetActive(false);
    }
}
