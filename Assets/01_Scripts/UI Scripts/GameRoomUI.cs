using UnityEngine;
using TMPro;

public class GameRoomUI : MonoBehaviour
{
    private const string lobbyKeyHideStr = "**********";

    public TMP_Text lobbyKeyText;

    private bool hideKey = true;

    private void Start()
    {
        lobbyKeyText.text = lobbyKeyHideStr;
    }

    public void ShowLobbyKey()
    {
        hideKey = !hideKey;

        if (hideKey)
        {
            lobbyKeyText.text = lobbyKeyHideStr;
        }
        else
        {
            lobbyKeyText.text = SteamRoomManager.Instance?.lobbyKeyStr;
        }
    }

    public void CopyLobbyKey()
    {
        GUIUtility.systemCopyBuffer = SteamRoomManager.Instance?.lobbyKeyStr; // 텍스트를 클립보드에 복사
    }

    public void ExitRoom()
    {
        SteamRoomManager.Instance.LeaveLobby();
    }
}
