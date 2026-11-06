using UnityEngine;
using System;
using System.Collections;
using System.Linq;
using Mirror;

public class GameManager : NetworkBehaviour
{
    public static GameManager Instance;

    public GameObject menuScreen;
    private bool isReturningRoom;

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
    [Server]
    public void FinishCheck()
    {
        if (!isServer || isReturningRoom) return;
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

    [Server]
    private void StageClear()
    {
        if (NetworkManager.singleton is SlimeRoomManager slimeRoomManager)
        {
            isReturningRoom = true;
            slimeRoomManager.ReturnRoomScene();
        }
        else
        {
            Debug.LogError("Cannot find slimeRoomManager.");
        }
    }

    [Command(requiresAuthority = false)]
    public void CmdChangeScene()
    {
        // 기존 외부 호출도 서버의 완료 상태를 다시 확인한다.
        FinishCheck();
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
