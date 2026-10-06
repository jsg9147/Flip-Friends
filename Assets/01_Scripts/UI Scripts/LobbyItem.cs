using System;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class LobbyItem : MonoBehaviour
{
    private SteamLobbyInfo lobbyInfo;
    private Action<SteamLobbyInfo> joinAction;

    public TMP_Text ownerNameText;
    public TMP_Text currentMemberText;
    public TMP_Text lobbyStateText;

    private Button joinButton;

    public Button JoinButton => joinButton != null ? joinButton : joinButton = GetComponent<Button>();

    private void Awake()
    {
        JoinButton.onClick.AddListener(JoinLobby);
    }

    public void SetLobbyInfo(SteamLobbyInfo lobbyInfo)
    {
        this.lobbyInfo = lobbyInfo;

        ownerNameText.text = lobbyInfo.LobbyName;
        currentMemberText.text = $"{lobbyInfo.CurrentMemberCount} / {lobbyInfo.MaxMembers}";
        lobbyStateText.text = lobbyInfo.IsInGame ? "Playing" : "Waiting";
    }

    public void SetJoinAction(Action<SteamLobbyInfo> action)
    {
        joinAction = action;
    }

    private void JoinLobby()
    {
        if (lobbyInfo == null)
        {
            return;
        }

        if (joinAction != null)
        {
            joinAction.Invoke(lobbyInfo);
            return;
        }

        SteamRoomManager.Instance.JoinLobby(lobbyInfo.LobbyID);
    }
}
