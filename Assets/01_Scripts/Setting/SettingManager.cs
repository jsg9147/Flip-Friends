using UnityEngine;
using UnityEngine.UI;

public class SettingManager : MonoBehaviour
{
    [Header("Adjusters")]
    public ResolutionAdjuster resolutionAdjuster;
    public ValueAdjuster bgmAdjuster;
    public ValueAdjuster sfxAdjuster;

    public Image playerImage;
    public ValueAdjuster redAdjuster;
    public ValueAdjuster greenAdjuster;
    public ValueAdjuster blueAdjuster;

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
}
