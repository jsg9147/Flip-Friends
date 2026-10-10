using UnityEngine;
using System;
using System.Collections;
using System.Linq;
using Mirror;

public class GameManager : NetworkBehaviour
{
    public static GameManager Instance;

    public GameObject menuScreen;

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

    // 플레이어 목록을 캐시하지 않는다. 캐시하면 도중에 이탈한 플레이어가 남아 클리어가 영영 안 된다.
    public void FinishCheck()
    {
        PlayerController2D[] playerControllers = FindObjectsByType<PlayerController2D>();
        if (playerControllers.Length == 0)
            return;

        bool allPlayersFinished = playerControllers.All(player => player.isFinish);

        if (allPlayersFinished)
        {
            Debug.Log("클리어");
            StageClear();
        }
    }

    // 남은 플레이어가 모두 도착한 상태에서 마지막 미도착자가 나가면 아무도 판정을 다시 부르지 않는다.
    // 이탈한 플레이어 오브젝트는 프레임 끝에 파괴되므로 다음 프레임에 검사한다.
    [Server]
    public void ServerRecheckFinishNextFrame()
    {
        StartCoroutine(RecheckFinishNextFrame());
    }

    private IEnumerator RecheckFinishNextFrame()
    {
        yield return null;
        FinishCheck();
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
