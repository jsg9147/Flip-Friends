using UnityEngine;

public class GameModeUI : MonoBehaviour
{
    public GameObject mainUI;
    public GameObject publicLobbyUI;
    public GameObject privateLobbyUI;

    public void PublicLobbyUIOpen()
    {
        gameObject.SetActive(false);
        publicLobbyUI.SetActive(true);
    }
    public void PrivateLobbyUIOpen()
    {
        gameObject.SetActive(false);
        privateLobbyUI.SetActive(true);
    }

    public void ExitButton()
    {
        gameObject.SetActive(false);
        mainUI.SetActive(true);
    }

    public void CreateRoom()
    {
        MirrorRoomManager.Instance.HostLobby();
    }
}

