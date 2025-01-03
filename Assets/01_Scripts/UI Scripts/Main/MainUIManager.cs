using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class MainUIManager : MonoBehaviour
{
    public static MainUIManager instance;

    public GameObject mainUI;
    public GameObject gameModeUI;
    public GameObject hostUI;

    public GameObject publicGameUI;
    public GameObject privateJoinUI;

    public GameObject settingUI;
    public GameObject keySettingUI;
    public GameObject keyboardSetting;
    public GameObject gamepadSetting;

    public int targetFrameRate = 60;

    private void Awake()
    {
        if(instance == null)
            instance = this;
    }

    void Start()
    {
        Init();
        MainUIOpen();
    }

    void Init()
    {
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlayBGM(0);
        }
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        Application.targetFrameRate = targetFrameRate;
    }

    void UIReset()
    {
        // uiList를 사용하지 않고 직접 모든 UI를 비활성화
        mainUI.SetActive(false);
        gameModeUI.SetActive(false);
        hostUI.SetActive(false);

        if (publicGameUI != null)
            publicGameUI.gameObject.SetActive(false);

        if (privateJoinUI != null)
            privateJoinUI.gameObject.SetActive(false);

        settingUI.SetActive(false);
        keyboardSetting.SetActive(false);
        gamepadSetting.SetActive(false);
        keySettingUI.SetActive(false);
    }

    public void MainUIOpen()
    {
        UIReset();
        mainUI.SetActive(true);
    }

    public void GameModeUIOpen()
    {
        UIReset();
        gameModeUI.SetActive(true);
    }

    public void HostUIOpen()
    {
        UIReset();
        hostUI.SetActive(true);
    }

    public void PublicUIOpen()
    {
        UIReset();
        publicGameUI.gameObject.SetActive(true);
    }

    public void PrivateUIOpen()
    {
        UIReset();
        privateJoinUI.gameObject.SetActive(true);
    }

    public void SettingUIOpen()
    {
        UIReset();
        settingUI.SetActive(true);
    }
    public void KeySettingUIOpen()
    {
        UIReset();
        keySettingUI.SetActive(false);
    }

    public void KeyboardSettingUIOpen()
    {
        UIReset();
        keyboardSetting.SetActive(true);
    }

    public void GamepadSettingUIOpen()
    {
        UIReset();
        gamepadSetting.SetActive(true);
    }

    public void GameQuit()
    {
        Application.Quit();
    }
}
