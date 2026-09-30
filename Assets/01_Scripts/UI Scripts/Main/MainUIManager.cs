using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class MainUIManager : MonoBehaviour
{
    private const string MainMenuScreenId = "main-menu";
    private const string ModeSelectScreenId = "mode-select";
    private const string HostRoomScreenId = "host-room";
    private const string PublicLobbyScreenId = "public-lobby";
    private const string PrivateJoinScreenId = "private-join";
    public bool mouseLock;

    public static MainUIManager instance;

    public GameObject mainUI;
    public GameObject gameModeUI;
    public GameObject hostUI;

    public GameObject publicGameUI;
    public GameObject privateJoinUI;

    public int targetFrameRate = 60;

    private ScreenNavigator screenNavigator;

    private void Awake()
    {
        if(instance == null)
            instance = this;

        screenNavigator = GetComponent<ScreenNavigator>();
    }

    void Start()
    {
        Init();
    }

    void Init()
    {
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlayBGM(0);
        }
        if (mouseLock)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        Application.targetFrameRate = targetFrameRate;
    }

    // ScreenNavigator가 없을 때만 쓰는 legacy 대체 경로다. 목록을 따로 두지 않고 개별 UI를 직접 끈다.
    void UIReset()
    {
        mainUI.SetActive(false);
        gameModeUI.SetActive(false);
        hostUI.SetActive(false);

        if (publicGameUI != null)
            publicGameUI.gameObject.SetActive(false);

        if (privateJoinUI != null)
            privateJoinUI.gameObject.SetActive(false);
    }

    public void MainUIOpen()
    {
        if (TryOpenScreen(MainMenuScreenId, false))
        {
            return;
        }

        UIReset();
        mainUI.SetActive(true);
    }

    public void GameModeUIOpen()
    {
        if (TryOpenScreen(ModeSelectScreenId))
        {
            return;
        }

        UIReset();
        gameModeUI.SetActive(true);
    }

    public void HostUIOpen()
    {
        if (TryOpenScreen(HostRoomScreenId))
        {
            return;
        }

        UIReset();
        hostUI.SetActive(true);
    }

    public void PublicUIOpen()
    {
        if (TryOpenScreen(PublicLobbyScreenId))
        {
            return;
        }

        UIReset();
        publicGameUI.gameObject.SetActive(true);
    }

    public void PrivateUIOpen()
    {
        if (TryOpenScreen(PrivateJoinScreenId))
        {
            return;
        }

        UIReset();
        privateJoinUI.gameObject.SetActive(true);
    }

    private bool TryOpenScreen(string screenId, bool rememberCurrent = true)
    {
        if (screenNavigator == null)
        {
            screenNavigator = GetComponent<ScreenNavigator>();
        }

        if (screenNavigator == null)
        {
            return false;
        }

        screenNavigator.Open(screenId, rememberCurrent);
        return true;
    }

    public void GameQuit()
    {
        Application.Quit();
    }
}
