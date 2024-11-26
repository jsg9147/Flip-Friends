using UnityEngine;
using System;
using System.Linq;
using Mirror;

public class GameManager : NetworkBehaviour
{
    public static GameManager Instance;

    [SerializeField] private int maxStageNum = 5;
    [SerializeField] private int maxChapterNum = 5;

    private PlayerController2D[] playerControllers;

    public void Awake()
    {
        Instance = this;
    }

    public void SetPlayerController(PlayerController2D[] playerControllers)
    {
        this.playerControllers = playerControllers;
    }

    public void FinishCheck()
    {
        if (playerControllers == null || playerControllers.Length == 0)
        {
            this.playerControllers = FindObjectsByType<PlayerController2D>(FindObjectsSortMode.InstanceID); ;
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
            if (slimeRoomManager.currentStage < maxStageNum)
            {
                slimeRoomManager.currentStage++;
                CmdChangeScene(slimeRoomManager.currentChapter);
            }
            else
            {
                if (slimeRoomManager.currentChapter < maxChapterNum)
                {
                    slimeRoomManager.currentChapter++;
                    slimeRoomManager.currentStage = 0;

                    CmdChangeScene(slimeRoomManager.currentChapter);
                }
                else
                {
                    print("최조 엔딩 구현");
                }
            }
        }
        else
        {
            Debug.LogError("Cannot find slimeRoomManager.");
        }
    }

    [Command(requiresAuthority = false)]
    public void CmdChangeScene(int chapter)
    {
        if (isServer)
        {
            SlimeRoomManager slimeRoomManager = (SlimeRoomManager)NetworkManager.singleton;
            if(slimeRoomManager != null)
                slimeRoomManager.ReturnRoomScene();
        }
    }
}
