using UnityEngine;
using UnityEngine.UI;

public class SettingManager : MonoBehaviour
{
    public GameObject settingWindow;
    public GameObject graphicAndSoundWindow;

    [Header("Adjusters")]
    public ResolutionAdjuster resolutionAdjuster;
    public ValueAdjuster bgmAdjuster;
    public ValueAdjuster sfxAdjuster;

    public GameObject keyRebindingWindow;
    public GameObject keyboardRebindWindow;
    public GameObject gamepadRebindWindow;

    public GameObject colorSettingWindow;

    public Image playerImage;
    public ValueAdjuster redAdjuster;
    public ValueAdjuster greenAdjuster;
    public ValueAdjuster blueAdjuster;

    private void OnEnable()
    {
        // Cancel 입력을 설정 UI 뒤로가기로 쓰기 위해 구독
        if (InputManager.instance != null)
            InputManager.instance.OnCancelEvent += CancelBtnEvent;
    }

    private void OnDisable()
    {
        if (InputManager.instance != null)
            InputManager.instance.OnCancelEvent -= CancelBtnEvent;
    }

    void Start()
    {
        // 이전 세션에서 저장한 값이 있으면 UI에 반영
        LoadSettings();
    }

    private void Update()
    {
        // 슬라이더 조작 중에도 미리보기가 바로 보이도록 매 프레임 반영
        playerImage.color = new(redAdjuster.value / 255f, greenAdjuster.value / 255f, blueAdjuster.value / 255f);
    }

    public void ApplySettings()
    {
        resolutionAdjuster.ApplyResolution();
        SaveSettings();
    }

    public void LoadSettings()
    {
        resolutionAdjuster.LoadResolution();

        bgmAdjuster.value = PlayerPrefs.GetInt("BGMVolume", bgmAdjuster.defaultValue);
        bgmAdjuster.UpdateValueText();

        sfxAdjuster.value = PlayerPrefs.GetInt("SFXVolume", sfxAdjuster.defaultValue);
        sfxAdjuster.UpdateValueText();

        redAdjuster.value = PlayerPrefs.GetInt("Red", redAdjuster.defaultValue);
        redAdjuster.UpdateValueText();

        greenAdjuster.value = PlayerPrefs.GetInt("Green", greenAdjuster.defaultValue);
        greenAdjuster.UpdateValueText();

        blueAdjuster.value = PlayerPrefs.GetInt("Blue", blueAdjuster.defaultValue);
        blueAdjuster.UpdateValueText();
    }

    public void SaveSettings()
    {
        PlayerPrefs.SetInt("BGMVolume", bgmAdjuster.value);
        PlayerPrefs.SetInt("SFXVolume", sfxAdjuster.value);

        PlayerPrefs.SetInt("ScreenRed", redAdjuster.value);
        PlayerPrefs.SetInt("ScreenGreen", greenAdjuster.value);
        PlayerPrefs.SetInt("ScreenBlue", blueAdjuster.value);

        PlayerPrefs.Save();
    }

    public void ResetSettings()
    {
        bgmAdjuster.value = bgmAdjuster.defaultValue;
        bgmAdjuster.UpdateValueText();

        sfxAdjuster.value = sfxAdjuster.defaultValue;
        sfxAdjuster.UpdateValueText();

        redAdjuster.value = redAdjuster.defaultValue;
        redAdjuster.UpdateValueText();

        greenAdjuster.value = greenAdjuster.defaultValue;
        greenAdjuster.UpdateValueText();

        blueAdjuster.value = blueAdjuster.defaultValue;
        blueAdjuster.UpdateValueText();

        // 해상도는 OS/모니터 의존성이 커서 기본값 강제 리셋을 보류
        // resolutionAdjuster.ResetToDefault();

        // UI 기본값만 바꾸면 실제 적용·저장이 안 되므로 Apply까지 호출
        ApplySettings();
    }

    public void OpenSettingWindow()
    {
        WindowReset();
        settingWindow.SetActive(true);
    }

    public void OpenGraphicsAndSoundWindow()
    {
        WindowReset();
        graphicAndSoundWindow.SetActive(true);
    }

    public void OpenKeyRebindingWindow()
    {
        WindowReset();
        keyRebindingWindow.SetActive(true);
    }

    public void OepnColorSettingWindow()
    {
        WindowReset();
        colorSettingWindow.SetActive(true);
    }

    public void OpenKeyboardRebindWindow()
    {
        WindowReset();
        keyboardRebindWindow.SetActive(true);
    }

    public void OpenGamepadRebindWindow()
    {
        WindowReset();
        gamepadRebindWindow.SetActive(true);
    }

    public void WindowReset()
    {
        // 하위 창이 겹치지 않도록 전환 전에 전부 닫음
        keyRebindingWindow.SetActive(false);
        colorSettingWindow.SetActive(false);
        keyboardRebindWindow.SetActive(false);
        gamepadRebindWindow.SetActive(false);
        settingWindow.SetActive(false);
        graphicAndSoundWindow.SetActive(false);
    }

    bool SettingActive()
    {
        bool isActive = false;
        if (keyRebindingWindow.activeSelf)
            isActive = true;
        if (colorSettingWindow.activeSelf)
            isActive = true;
        if (keyboardRebindWindow.activeSelf)
            isActive = true;
        if (gamepadRebindWindow.activeSelf)
            isActive = true;
        if (settingWindow.activeSelf)
            isActive = true;
        if (graphicAndSoundWindow.activeSelf)
            isActive = true;

        return isActive;
    }

    private void CancelBtnEvent()
    {
        // 루트 설정 창이면 메인 UI로, 하위 창이면 설정 루트로 한 단계만 되돌림
        if (settingWindow.activeSelf)
        {
            WindowReset();
            MainUIManager.instance.MainUIOpen();
        }
        else if (SettingActive())
        {
            OpenSettingWindow();
        }
    }
}
