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

    [SyncVar(hook = nameof(OnColorChange))] public Vector4 playerColor;

    private int stage;

    private PlayerController2D playerController;

    private GameObject notReadyText;

    private bool gameStart;

    public override void OnClientEnterRoom()
    {
        base.OnClientEnterRoom();
        Init();
    }

    private void OnEnable()
    {
        if (InputManager.instance != null)
        {
            InputManager.instance.OnSubmitEvent += OnReady;
        }
    }
    public override void OnDisable()
    {
        if (InputManager.instance != null)
        {
            InputManager.instance.OnSubmitEvent -= OnReady;
        }
        base.OnDisable();
    }

    public void Init()
    {
        if (isLocalPlayer)
        {
            gameStart = false;
            SteamRoomManager roomManager = NetworkManager.singleton as SteamRoomManager;
            if (roomManager != null && NetworkServer.active)
            {
                CmdSetPlayerName(roomManager.playerName);
            }

            CmdSetPlayerColor(new Vector4(PlayerPrefs.GetFloat("Red", 0.3f), PlayerPrefs.GetFloat("Green", 1.0f), PlayerPrefs.GetFloat("Blue", 1.0f), 1f));
            notReadyText = GameObject.Find("NotReadyText").GetComponent<GameObject>();

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

    [Command]
    void CmdSetPlayerColor(Vector4 color)
    {
        playerColor = color;
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

    void OnColorChange(Vector4 oldColor, Vector4 newColor)
    {
        if (playerController != null)
        {
            playerController.CmdSetPlayerColor(newColor);
        }
        else
        {
            foreach (var lobbyPlayer in FindObjectsByType<PlayerController2D>(FindObjectsSortMode.InstanceID))
            {
                if (isLocalPlayer && lobbyPlayer.isOwned)
                {
                    playerController = lobbyPlayer;
                    lobbyPlayer.CmdSetPlayerColor(newColor);
                }
            }
        }
    }


    private void OnReady()
    {
        if (gameStart)
            return;

        if (isLocalPlayer)
        {
            CmdChangeReadyState(!readyToBegin);

            if (isLocalPlayer && notReadyText != null)
            {
                notReadyText.gameObject.SetActive(!readyToBegin);
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
        gameStart = isActive;
        FindAnyObjectByType<MapSelectionManager>().MapSelectScreenSetActive(isActive);
        RpcStageSelectUIOn(isActive);
    }

    [ClientRpc]
    private void RpcStageSelectUIOn(bool isActive)
    {
        FindAnyObjectByType<MapSelectionManager>().MapSelectScreenSetActive(isActive);
        if(isOwned)
            playerController.gameObject.SetActive(!isActive);
    }

    [Command]
    public void CmdStageSelect(int stage)
    {
        this.stage = stage;

        SlimeRoomManager slimeRoomManager = (SlimeRoomManager)NetworkManager.singleton;
        if (slimeRoomManager != null)
        {
            slimeRoomManager.currentStage = stage;
        }

        CmdChangeScene();
    }

    [Command(requiresAuthority = false)]
    public void CmdChangeScene()
    {
        if (isServer)
        {
            NetworkManager.singleton.ServerChangeScene($"GamePlay");
        }
    }
}
