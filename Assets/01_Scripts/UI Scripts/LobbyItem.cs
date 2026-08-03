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

    private void Awake()
    {
        GetComponent<Button>().onClick.AddListener(JoinLobby);
    }

    public void SetLobbyInfo(SteamLobbyInfo lobbyInfo)
    {
        this.lobbyInfo = lobbyInfo;

        ownerNameText.text = lobbyInfo.LobbyName;
        currentMemberText.text = $"{lobbyInfo.CurrentMemberCount} / {lobbyInfo.MaxMembers}";
        LobbyMapMetadata map = lobbyInfo.MapMetadata;
        string state = lobbyInfo.IsInGame ? "Playing" : "Waiting";
        lobbyStateText.text = map == null
            ? $"{state} · Map info unavailable"
            : $"{state} · {GetMapKindLabel(map.MapKey.Kind)} · " +
              $"{map.DisplayName} · Min {map.MinimumPlayersToClear}";
    }

    private static string GetMapKindLabel(LobbyMapKind kind) => kind switch
    {
        LobbyMapKind.Official => "Official",
        LobbyMapKind.Custom => "Custom",
        _ => "Unknown"
    };

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
