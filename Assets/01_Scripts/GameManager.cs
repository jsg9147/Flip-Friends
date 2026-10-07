using UnityEngine;
using System;
using System.Linq;
using Mirror;

public class GameManager : NetworkBehaviour
{
    public static GameManager Instance;

    public GameObject menuScreen;

    private PlayerController2D[] playerControllers;

    public void Awake()
    {
        Instance = this;
    }

    private void OnEnable()
    {
        if(InputManager.instance != null)
        {
            InputManager.instance.OnMenuEvent += SetMenu;
        }
    }

    private void OnDisable()
    {
        if (InputManager.instance != null)
        {
            InputManager.instance.OnMenuEvent -= SetMenu;
        }
    }

    public void SetPlayerController(PlayerController2D[] playerControllers)
    {
        this.playerControllers = playerControllers;
    }

    public void FinishCheck()
    {
        if (playerControllers == null || playerControllers.Length == 0)
        {
            this.playerControllers = FindObjectsByType<PlayerController2D>();
        }

        bool allPlayersFinished = playerControllers.All(player => player.isFinish);

        if (allPlayersFinished)
        {
            Debug.Log("클리어");
            StageClear();
        }
    }

    private void StageClear()
    {
        SlimeRoomManager slimeRoomManager = (SlimeRoomManager)NetworkManager.singleton;

        if(slimeRoomManager != null)
        {
            CmdChangeScene();
        }
        else
        {
            Debug.LogError("Cannot find slimeRoomManager.");
        }
    }

    [Command(requiresAuthority = false)]
    public void CmdChangeScene()
    {
        if (isServer)
        {
            SlimeRoomManager slimeRoomManager = (SlimeRoomManager)NetworkManager.singleton;
            if(slimeRoomManager != null)
                slimeRoomManager.ReturnRoomScene();
        }
    }

    public void ExitGame()
    {
        if (NetworkManager.singleton is SlimeRoomManager { IsTestPlaying: true } roomManager)
        {
            roomManager.EndTestPlay();
            return;
        }

        if (SteamRoomManager.Instance != null)
        {
            SteamRoomManager.Instance.LeaveLobby();
        }
        NetworkManager.singleton.StopClient();
    }

    private void SetMenu()
    {
        menuScreen.SetActive(!menuScreen.activeSelf);
    }
}
