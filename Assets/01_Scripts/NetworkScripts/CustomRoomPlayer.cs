using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Mirror;
using Steamworks;

public class CustomRoomPlayer : NetworkRoomPlayer
{
    public MarioLikePlayerController roomPlayer;
    public SpriteRenderer readyStatusSprite;

    [SyncVar(hook = nameof(OnNameChanged))]
    public string playerName = "No Name";

    [SyncVar]
    public bool isReady = false;

    private Button readyButton;
    private int stage;

    public override void OnStartClient()
    {
        base.OnStartClient();

        if (isLocalPlayer)
        {
            CmdPLayerObjSetActive(true);
            string playerName = SteamFriends.GetFriendPersonaName(SteamUser.GetSteamID());
            MirrorRoomManager.Instance.playerName = playerName;
            CmdSetPlayerName(playerName);

            readyButton = GameObject.Find("ReadyButton").GetComponent<Button>();
            if (readyButton != null)
            {
                readyButton.onClick.AddListener(OnReadyButtonClicked);
            }

            FindAnyObjectByType<MapSelectionManager>().roomPlayer = this;
        }
    }

    [Command]
    private void CmdPLayerObjSetActive(bool isActive)
    {
        roomPlayer.gameObject.SetActive(isActive);
        RpcPLayerObjSetActive(isActive);
    }

    [ClientRpc]
    private void RpcPLayerObjSetActive(bool isActive)
    {
        roomPlayer.gameObject.SetActive(isActive);
    }

    [Command]
    void CmdSetPlayerName(string newName)
    {
        playerName = newName; // 서버에서 플레이어 이름 설정
    }

    void OnNameChanged(string oldName, string newName)
    {
        Debug.Log($"플레이어 이름이 {oldName}에서 {newName}으로 변경되었습니다.");
        //roomPlayer.nameText.text = newName;
    }

    private void OnReadyButtonClicked()
    {
        if (isLocalPlayer)
        {
            CmdPlayerReadyChange();

            if (isLocalPlayer && readyButton != null)
            {
                readyButton.GetComponentInChildren<TMP_Text>().text = isReady ? "CANCEL" : "READY";
            }
        }
    }

    [ClientRpc]
    private void RpcReadyStateChange(bool readyToBegin)
    {
        readyStatusSprite.gameObject.SetActive(readyToBegin);

        if (!isServer)
        {
            CmdChangeReadyState(readyToBegin);
        }
    }

    [Command]
    private void CmdPlayerReadyChange()
    {
        isReady = !isReady;
        readyStatusSprite.gameObject.SetActive(isReady);
        RpcReadyStateChange(isReady);

        if (MirrorRoomManager.Instance.CheckAllPlayersReady())
        {
            FindAnyObjectByType<StageSelectUI>().stageSelectionUI.SetActive(true);
            roomPlayer.gameObject.SetActive(false);
            RpcStageSelectUIOn();
        }
    }

    [ClientRpc]
    private void RpcStageSelectUIOn()
    {
        FindAnyObjectByType<StageSelectUI>().stageSelectionUI.SetActive(true);
        roomPlayer.gameObject.SetActive(false);
    }

    [Command]
    public void CmdStageSelect(int chapter, int stage)
    {
        this.stage = stage;
        MirrorRoomManager.Instance.currentStage = stage;
        CmdChangeScene(chapter);
    }

    [Command(requiresAuthority = false)]
    public void CmdChangeScene(int chapter)
    {
        if (isServer)
        {
            chapter = chapter + 1;
            NetworkManager.singleton.ServerChangeScene($"Chapter_{chapter}");
        }
    }
}
