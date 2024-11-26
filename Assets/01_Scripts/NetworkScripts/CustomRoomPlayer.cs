using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using Mirror;
using Steamworks;
using System.Collections;
using Unity.Collections.LowLevel.Unsafe;

public class CustomRoomPlayer : NetworkRoomPlayer
{
    [SyncVar(hook = nameof(OnNameChanged))]
    public string playerName = "No Name";

    private Button readyButton;
    private int stage;

    private PlayerController2D playerController;

    public override void OnClientEnterRoom()
    {
        base.OnClientEnterRoom();
        Init();
    }

    public void Init()
    {
        if (isLocalPlayer)
        {
            SteamRoomManager roomManager = NetworkManager.singleton as SteamRoomManager;
            if (roomManager != null)
            {
                CmdSetPlayerName(roomManager.playerName);
            }

            readyButton = GameObject.Find("ReadyButton").GetComponent<Button>();
            if (readyButton != null)
            {
                readyButton.onClick.AddListener(OnReadyButtonClicked);
            }

            MapSelectionManager mapSelectionManager = FindAnyObjectByType<MapSelectionManager>();
            if(mapSelectionManager != null)
                mapSelectionManager.roomPlayer = this;

            foreach (var lobbyPlayer in FindObjectsByType<PlayerController2D>(FindObjectsSortMode.InstanceID))
            {
                if (lobbyPlayer.isOwned)
                {
                    playerController = lobbyPlayer;
                }
            }
        }
    }

    [Command]
    void CmdSetPlayerName(string newName)
    {
        playerName = newName; // 서버에서 플레이어 이름 설정
    }

    void OnNameChanged(string oldName, string newName)
    {
        if (playerController != null)
        {
            playerController.CmdSetPlayerName(newName);
        }
        else
        {
            foreach (var lobbyPlayer in FindObjectsByType<PlayerController2D>(FindObjectsSortMode.InstanceID))
            {
                if (isLocalPlayer && lobbyPlayer.isOwned)
                {
                    playerController = lobbyPlayer;
                    lobbyPlayer.CmdSetPlayerName(playerName);
                }
            }
        }
    }


    private void OnReadyButtonClicked()
    {
        if (isLocalPlayer)
        {
            CmdChangeReadyState(!readyToBegin);

            if (isLocalPlayer && readyButton != null)
            {
                readyButton.GetComponentInChildren<TMP_Text>().text = readyToBegin ? "READY" : "CANCEL";
            }
        }
    }

    public override void ReadyStateChanged(bool oldReadyState, bool newReadyState)
    {
        base.ReadyStateChanged(oldReadyState, newReadyState);
        ReadySpriteChanged(newReadyState);
    }

    void ReadySpriteChanged(bool isReady)
    {
        if (playerController != null && isLocalPlayer)
        {
            playerController.CmdSetPlayerReady(isReady);
        }
    }

    public void StageSelectionUISetAcitve(bool isActive)
    {
        FindAnyObjectByType<StageSelectUI>().StageSelectionUISetActive(isActive);
        RpcStageSelectUIOn(isActive);
    }

    [ClientRpc]
    private void RpcStageSelectUIOn(bool isActive)
    {
        FindAnyObjectByType<StageSelectUI>().StageSelectionUISetActive(isActive);
    }

    [Command]
    public void CmdStageSelect(int chapter, int stage)
    {
        this.stage = stage;

        SlimeRoomManager slimeRoomManager = (SlimeRoomManager)NetworkManager.singleton;
        if (slimeRoomManager != null)
        {
            slimeRoomManager.currentChapter = chapter;
            slimeRoomManager.currentStage = stage;
        }

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
