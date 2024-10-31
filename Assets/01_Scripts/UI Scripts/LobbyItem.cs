using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class LobbyItem : MonoBehaviour
{
    private SteamLobbyInfo lobbyInfo;

    public TMP_Text ownerNameText;
    public TMP_Text currentMemberText;
    public TMP_Text lobbyStateText;

    private void Start()
    {
        GetComponent<Button>().onClick.AddListener(JoinLobby);
    }

    public void SetLobbyInfo(SteamLobbyInfo lobbyInfo)
    {
        this.lobbyInfo = lobbyInfo;

        ownerNameText.text = lobbyInfo.LobbyName;
        currentMemberText.text = $"{lobbyInfo.CurrentMemberCount} / {lobbyInfo.MaxMembers}";
        lobbyStateText.text = lobbyInfo.IsInGame ? "Playing" : "Waiting";
    }

    private void JoinLobby()
    {
        if (lobbyInfo != null)
            MirrorRoomManager.Instance.JoinLobby(lobbyInfo.LobbyID);
    }
}
