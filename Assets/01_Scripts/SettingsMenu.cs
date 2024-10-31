using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro;

public class SettingsMenu : MonoBehaviour
{
    public TMP_Dropdown resolutionDropdown;
    public TMP_Dropdown fullscreenDropdown;
    public Slider bgmSlider;
    public Slider sfxSlider;

    private Resolution[] resolutions;

    void Start()
    {
        // 해상도 옵션 설정
        resolutions = Screen.resolutions;
        resolutionDropdown.ClearOptions();
        List<string> options = new List<string>();
        HashSet<string> uniqueResolutions = new HashSet<string>();

        int currentResolutionIndex = 0;
        for (int i = 0; i < resolutions.Length; i++)
        {
            string option = resolutions[i].width + " x " + resolutions[i].height;
            if (uniqueResolutions.Add(option)) // 중복 해상도 제거
            {
                options.Add(option);

                if (resolutions[i].width == Screen.currentResolution.width &&
                    resolutions[i].height == Screen.currentResolution.height)
                {
                    currentResolutionIndex = options.Count - 1;
                }
            }
        }

        resolutionDropdown.AddOptions(options);
        resolutionDropdown.value = currentResolutionIndex;
        resolutionDropdown.RefreshShownValue();

        // 해상도 변경 이벤트 연결
        resolutionDropdown.onValueChanged.AddListener(SetResolution);

        // 화면 모드 옵션 설정
        fullscreenDropdown.onValueChanged.AddListener(SetFullscreen);

        // BGM 및 SFX 슬라이더 초기화 및 이벤트 연결
        bgmSlider.value = PlayerPrefs.GetFloat("BGMVolume", 1.0f);
        bgmSlider.onValueChanged.AddListener(SetBGMVolume);

        sfxSlider.value = PlayerPrefs.GetFloat("SFXVolume", 1.0f);
        sfxSlider.onValueChanged.AddListener(SetSFXVolume);
    }

    public void SetResolution(int resolutionIndex)
    {
        string[] dimensions = resolutionDropdown.options[resolutionIndex].text.Split('x');
        int width = int.Parse(dimensions[0].Trim());
        int height = int.Parse(dimensions[1].Trim());
        Screen.SetResolution(width, height, Screen.fullScreenMode);
    }

    public void SetFullscreen(int mode)
    {
        switch (mode)
        {
            case 0: // 창 모드
                Screen.fullScreenMode = FullScreenMode.Windowed;
                break;
            case 1: // 전체 화면 모드
                Screen.fullScreenMode = FullScreenMode.FullScreenWindow;
                break;
            case 2: // 무제한 창 모드
                Screen.fullScreenMode = FullScreenMode.MaximizedWindow;
                break;
        }
    }

    public void SetBGMVolume(float volume)
    {
        PlayerPrefs.SetFloat("BGMVolume", volume);
        AudioListener.volume = volume; // 예시: 전체 볼륨 조정
        // 실제 게임에서는 AudioMixer 사용을 권장합니다.
    }

    public void SetSFXVolume(float volume)
    {
        PlayerPrefs.SetFloat("SFXVolume", volume);
        // SFX 볼륨의 경우 각 효과음 오브젝트에 설정된 AudioSource에서 volume 값을 조정하는 방식으로 구현 가능합니다.
    }

    public void CloseSettingUI()
    {
        gameObject.SetActive(false);
    }
}
