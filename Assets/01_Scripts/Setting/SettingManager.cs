using UnityEngine;
using UnityEngine.UI;

public class SettingManager : MonoBehaviour
{
    public GameObject settingWindow;
    public GameObject graphicAndSoundWindow;

    [Header("Adjusters")]
    public ResolutionAdjuster resolutionAdjuster; // 해상도 조정

    public ValueAdjuster bgmAdjuster; // BGM 볼륨 조정
    public ValueAdjuster sfxAdjuster; // SFX 볼륨 조정

    public GameObject keyRebindingWindow;
    public GameObject keyboardRebindWindow;
    public GameObject gamepadRebindWindow;

    public GameObject colorSettingWindow;

    public Image playerImage;
    public ValueAdjuster redAdjuster; // 화면 빨간색 조정
    public ValueAdjuster greenAdjuster; // 화면 초록색 조정
    public ValueAdjuster blueAdjuster; // 화면 파란색 조정

    private void OnEnable()
    {
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
        // 모든 설정 불러오기
        LoadSettings();
    }

    private void Update()
    {
        playerImage.color = new(redAdjuster.value / 255f, greenAdjuster.value / 255f, blueAdjuster.value / 255f);
    }

    public void ApplySettings()
    {
        // 각 설정을 적용
        resolutionAdjuster.ApplyResolution();

        // 모든 설정 저장
        SaveSettings();
    }

    public void LoadSettings()
    {
        // 각 설정 불러오기
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
        // 각 설정 저장
        PlayerPrefs.SetInt("BGMVolume", bgmAdjuster.value);
        PlayerPrefs.SetInt("SFXVolume", sfxAdjuster.value);

        PlayerPrefs.SetInt("ScreenRed", redAdjuster.value);
        PlayerPrefs.SetInt("ScreenGreen", greenAdjuster.value);
        PlayerPrefs.SetInt("ScreenBlue", blueAdjuster.value);

        PlayerPrefs.Save();
    }

    public void ResetSettings()
    {
        // 기본값으로 설정
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

        //resolutionAdjuster.ResetToDefault(); // 해상도 기본값으로 설정

        // 변경사항 적용
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
        if(keyRebindingWindow.activeSelf)
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
        if(settingWindow.activeSelf)
        {
            WindowReset();
            MainUIManager.instance.MainUIOpen();
        }        
        else if(SettingActive())
        {
            OpenSettingWindow();
        }
    }
}
