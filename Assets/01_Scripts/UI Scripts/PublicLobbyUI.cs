using UnityEngine;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Linq;

public class PublicLobbyUI : MonoBehaviour
{
    public List<GameObject> inputCodeBtns;

    public LobbyItem lobbyItemPrefab;
    public Transform lobbyItemContent;

    public GameObject gameModeUI;

    private List<LobbyItem> lobbyItems = new();

    private async void OnEnable()
    {
        if (SteamManager.Initialized)
            await LobbyListUpdate();

        if (InputManager.instance != null)
            InputManager.instance.OnCancelEvent += CancelBtnEvent;
    }

    private void OnDisable()
    {
        LobbyListReset();
        if (InputManager.instance != null)
            InputManager.instance.OnCancelEvent -= CancelBtnEvent;
    }

    private void Update()
    {
        if (InputManager.instance.dir.x != 0)
        {
            if (inputCodeBtns.Contains(EventSystem.current.currentSelectedGameObject))
            {

            }
        }
    }

    public async Task LobbyListUpdate()
    {
        LobbyListReset();

        List<SteamLobbyInfo> lobbyInfoList = await SteamRoomManager.Instance.GetLobbyListAsync();

        foreach (SteamLobbyInfo info in lobbyInfoList)
        {
            LobbyItem item = Instantiate(lobbyItemPrefab, lobbyItemContent);
            item.SetLobbyInfo(info);

            lobbyItems.Add(item);
        }
    }

    void LobbyListReset()
    {
        foreach (Transform child in lobbyItemContent)
        {
            Destroy(child.gameObject);
        }

        lobbyItems.Clear();
    }

    public async void OnLobbyListUpdateButtonClicked()
    {
        if (SteamManager.Initialized)
            await LobbyListUpdate();
    }

    private void CancelBtnEvent()
    {
        MainUIManager.instance.GameModeUIOpen();
    }
}
