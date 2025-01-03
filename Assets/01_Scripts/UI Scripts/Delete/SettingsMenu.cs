using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro;
using UnityEngine.EventSystems;

public static class SettingsData
{
    public static float GetVolume(string key, float defaultValue) => PlayerPrefs.GetFloat(key, defaultValue);
    public static void SetVolume(string key, float value) => PlayerPrefs.SetFloat(key, value);

    public static int GetInt(string key, int defaultValue) => PlayerPrefs.GetInt(key, defaultValue);
    public static void SetInt(string key, int value) => PlayerPrefs.SetInt(key, value);
}

public class SettingsMenu : MonoBehaviour
{
    public GameObject settingUI;
    [Header("UI Elements")]
    public TMP_Text resolutionText;
    public TMP_Text fullscreenText;
    public TMP_Text bgmText;
    public TMP_Text sfxText;

    [Header("Borders")]
    public GameObject resolutionBorder;
    public GameObject screenModeBorder;
    public GameObject bgmBorder;
    public GameObject sfxBorder;

    [Header("Key Settings UI")]
    public GameObject keySettingUI;

    [Header("Player Image & Color")]
    public Image playerImage;
    public TMP_Text redText;
    public TMP_Text greenText;
    public TMP_Text blueText;
    public GameObject redBorder;
    public GameObject greenBorder;
    public GameObject blueBorder;

    public ButtonNavigation settingNav;

    private Resolution[] resolutions;
    private int currentResolutionIndex;
    private int currentFullscreenIndex;

    private const float InputCooldown = 0.1f;
    private float lastInputTime;
    private bool canNavigate = true;



    private void Start()
    {
        InitializeSettings();
    }

    private void Update()
    {
        HandleNavigation();
    }

    private void FixedUpdate()
    {
        HandleColorAndVolumeInput();
    }

    private void InitializeSettings()
    {
        resolutions = FilterResolutionsTo16x9(Screen.resolutions);
        currentResolutionIndex = SettingsData.GetInt("ResolutionIndex", GetDefaultResolutionIndex());
        SetResolution(currentResolutionIndex);
        UpdateResolutionText();

        currentFullscreenIndex = SettingsData.GetInt("FullscreenMode", 1); // 기본 FullScreenWindow로 설정
        Screen.fullScreenMode = GetFullScreenMode(currentFullscreenIndex);
        UpdateFullscreenText();

        InitializeVolumeAndColor();

        keySettingUI.SetActive(false);
    }

    private int GetDefaultResolutionIndex()
    {
        Resolution currentResolution = Screen.currentResolution;

        for (int i = 0; i < resolutions.Length; i++)
        {
            if (resolutions[i].width == currentResolution.width && resolutions[i].height == currentResolution.height)
            {
                return i;
            }
        }

        return 0; // 기본 해상도 인덱스
    }

    private void SetResolution(int index)
    {
        if (index >= 0 && index < resolutions.Length)
        {
            Resolution resolution = resolutions[index];
            Screen.SetResolution(resolution.width, resolution.height, Screen.fullScreenMode);
        }
    }

    private void InitializeVolumeAndColor()
    {
        bgmText.text = Mathf.Round(PlayerPrefs.GetFloat("BGMVolume", 1.0f) * 100).ToString();
        sfxText.text = Mathf.Round(PlayerPrefs.GetFloat("SFXVolume", 1.0f) * 100).ToString();

        redText.text = Mathf.Round(PlayerPrefs.GetFloat("Red", 0.3f) * 255).ToString();
        greenText.text = Mathf.Round(PlayerPrefs.GetFloat("Green", 1.0f) * 255).ToString();
        blueText.text = Mathf.Round(PlayerPrefs.GetFloat("Blue", 1.0f) * 255).ToString();

        playerImage.color = new Color(
            PlayerPrefs.GetFloat("Red", 0.3f),
            PlayerPrefs.GetFloat("Green", 1.0f),
            PlayerPrefs.GetFloat("Blue", 1.0f)
        );
    }

    private void HandleNavigation()
    {
        if (!canNavigate || InputManager.instance.dir.x == 0 || Time.time - lastInputTime <= InputCooldown) return;

        int direction = (int)InputManager.instance.dir.x;
        GameObject selectedObject = EventSystem.current.currentSelectedGameObject;

        if (selectedObject == resolutionBorder)
        {
            ChangeResolution(direction);
        }
        else if (selectedObject == screenModeBorder)
        {
            ChangeFullscreenMode(direction);
        }

        lastInputTime = Time.time;
        canNavigate = false;
    }

    private void HandleColorAndVolumeInput()
    {
        if (InputManager.instance.dir.x == 0)
        {
            canNavigate = true;
            return;
        }

        int direction = (int)InputManager.instance.dir.x;
        GameObject selectedObject = EventSystem.current.currentSelectedGameObject;

        if (selectedObject == bgmBorder)
        {
            AdjustVolume("BGMVolume", bgmText, direction, volume =>
            {
                if (SoundManager.Instance != null)
                {
                    SoundManager.Instance.SetBGMVolume(volume);
                }
            });
        }
        else if (selectedObject == sfxBorder)
        {
            AdjustVolume("SFXVolume", sfxText, direction, volume =>
            {
                if (SoundManager.Instance != null)
                {
                    SoundManager.Instance.SetSFXVolume(volume);
                }
            });
        }
        else if (selectedObject == redBorder)
        {
            AdjustColor("Red", redText, direction, (value) => playerImage.color = new Color(value, playerImage.color.g, playerImage.color.b));
        }
        else if (selectedObject == greenBorder)
        {
            AdjustColor("Green", greenText, direction, (value) => playerImage.color = new Color(playerImage.color.r, value, playerImage.color.b));
        }
        else if (selectedObject == blueBorder)
        {
            AdjustColor("Blue", blueText, direction, (value) => playerImage.color = new Color(playerImage.color.r, playerImage.color.g, value));
        }
    }

    public void ChangeRedColor(int direction)
    {
        AdjustColor("Red", redText, direction, (value) => playerImage.color = new Color(value, playerImage.color.g, playerImage.color.b));
    }
    public void ChangeGreenColor(int direction)
    {
        AdjustColor("Green", greenText, direction, (value) => playerImage.color = new Color(playerImage.color.r, value, playerImage.color.b));
    }
    public void ChangeBlueColor(int direction)
    {
        AdjustColor("Blue", blueText, direction, (value) => playerImage.color = new Color(playerImage.color.r, playerImage.color.g, value));
    }

    public void SetBGMVolume(int direction)
    {
        AdjustVolume("BGMVolume", bgmText, direction, volume =>
        {
            if (SoundManager.Instance != null)
            {
                SoundManager.Instance.SetBGMVolume(volume);
            }
        });
    }

    public void SetSFXVolume(int direction)
    {
        AdjustVolume("SFXVolume", sfxText, direction, volume =>
        {
            if (SoundManager.Instance != null)
            {
                SoundManager.Instance.SetSFXVolume(volume);
            }
        });
    }

    private Resolution[] FilterResolutionsTo16x9(Resolution[] availableResolutions)
    {
        List<Resolution> filteredResolutions = new List<Resolution>();

        foreach (var resolution in availableResolutions)
        {
            float aspectRatio = (float)resolution.width / resolution.height;
            if (Mathf.Abs(aspectRatio - 16f / 9f) < 0.01f)
            {
                filteredResolutions.Add(resolution);
            }
        }

        return filteredResolutions.ToArray();
    }

    private void UpdateResolutionText()
    {
        resolutionText.text = $"{resolutions[currentResolutionIndex].width} x {resolutions[currentResolutionIndex].height}";
    }

    private void UpdateFullscreenText()
    {
        string fullscreenStr = currentFullscreenIndex == 1 ? "Window" : "FullScreen";

        fullscreenText.text = fullscreenStr;
    }

    public void ChangeResolution(int direction)
    {
        currentResolutionIndex = (currentResolutionIndex + direction + resolutions.Length) % resolutions.Length;
        SetResolution(currentResolutionIndex);
        UpdateResolutionText();

        SettingsData.SetInt("ResolutionIndex", currentResolutionIndex); // 변경된 해상도 저장
    }

    public void ChangeFullscreenMode(int direction)
    {
        currentFullscreenIndex = (currentFullscreenIndex + direction + 2) % 2; // 1: FullScreenWindow, 3: MaximizedWindow
        Screen.fullScreenMode = GetFullScreenMode(currentFullscreenIndex);
        UpdateFullscreenText();

        SettingsData.SetInt("FullscreenMode", currentFullscreenIndex); // 변경된 화면 모드 저장
    }

    private FullScreenMode GetFullScreenMode(int index)
    {
        return index == 0 ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
    }

    private void AdjustVolume(string key, TMP_Text text, int direction, System.Action<float> applyVolume)
    {
        float volume = Mathf.Clamp(PlayerPrefs.GetFloat(key, 1.0f) * 100 + direction, 0, 100);
        PlayerPrefs.SetFloat(key, volume / 100);
        text.text = Mathf.Round(volume).ToString();
        applyVolume?.Invoke(volume / 100);
    }

    private void AdjustColor(string key, TMP_Text text, int direction, System.Action<float> applyColor)
    {
        float colorValue = Mathf.Clamp(PlayerPrefs.GetFloat(key, 0.3f) * 255 + direction, 0, 255);
        PlayerPrefs.SetFloat(key, colorValue / 255);
        text.text = Mathf.Round(colorValue).ToString();
        applyColor?.Invoke(colorValue / 255);
    }

    public void OpenKeySettingUI()
    {
        keySettingUI.SetActive(true);
        settingNav.enabled = false;
    }

    public void CloseKeySettingUI()
    {
        keySettingUI.SetActive(false);
        settingNav.enabled = true;
    }
}
