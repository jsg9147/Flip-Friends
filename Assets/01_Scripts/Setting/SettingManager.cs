using UnityEngine;
using UnityEngine.UI;

// 설정 값의 저장은 각 Adjuster가 직접 맡는다. 이 클래스는 저장된 값을 실제 게임에 적용하는 책임만 갖는다
public class SettingManager : MonoBehaviour
{
    [Header("Adjusters")]
    [SerializeField] private ResolutionAdjuster resolutionAdjuster;
    [SerializeField] private ScreenModeAdjuster screenModeAdjuster;
    [SerializeField] private ValueAdjuster bgmAdjuster;
    [SerializeField] private ValueAdjuster sfxAdjuster;

    [Header("Player Color")]
    [SerializeField] private Image playerImage;
    [SerializeField] private ValueAdjuster redAdjuster;
    [SerializeField] private ValueAdjuster greenAdjuster;
    [SerializeField] private ValueAdjuster blueAdjuster;

    // 설정 창의 Adjuster는 화면을 처음 열 때 깨어난다. 그래서 Start에서 미리 적용하지 않고,
    // Adjuster가 값을 불러오며 보내는 ValueChanged만으로 적용한다
    private void OnEnable()
    {
        Subscribe(bgmAdjuster, HandleBgmChanged);
        Subscribe(sfxAdjuster, HandleSfxChanged);
        Subscribe(redAdjuster, HandleColorChanged);
        Subscribe(greenAdjuster, HandleColorChanged);
        Subscribe(blueAdjuster, HandleColorChanged);
    }

    private void OnDisable()
    {
        Unsubscribe(bgmAdjuster, HandleBgmChanged);
        Unsubscribe(sfxAdjuster, HandleSfxChanged);
        Unsubscribe(redAdjuster, HandleColorChanged);
        Unsubscribe(greenAdjuster, HandleColorChanged);
        Unsubscribe(blueAdjuster, HandleColorChanged);
    }

    // 해상도와 화면 모드는 ±를 누를 때마다 바꾸면 조작 중 창이 계속 흔들려서, 설정 화면을 벗어날 때 한 번만 확정한다
    public void ApplySettings()
    {
        if (resolutionAdjuster == null)
        {
            Debug.LogWarning("ResolutionAdjuster가 연결되지 않아 해상도를 적용하지 못했습니다.", this);
            return;
        }

        if (screenModeAdjuster == null)
        {
            Debug.LogWarning("ScreenModeAdjuster가 연결되지 않아 현재 화면 모드를 유지합니다.", this);
            resolutionAdjuster.ApplyResolution(Screen.fullScreenMode);
            return;
        }

        resolutionAdjuster.ApplyResolution(screenModeAdjuster.SelectedMode);
        screenModeAdjuster.SaveScreenMode();
    }

    private void HandleBgmChanged(int value)
    {
        ApplyBgmVolume();
    }

    private void HandleSfxChanged(int value)
    {
        ApplySfxVolume();
    }

    private void HandleColorChanged(int value)
    {
        ApplyPlayerColor();
    }

    private void ApplyBgmVolume()
    {
        if (bgmAdjuster == null || SoundManager.Instance == null)
        {
            return;
        }

        SoundManager.Instance.ApplyBGMVolume(bgmAdjuster.NormalizedValue);
    }

    private void ApplySfxVolume()
    {
        if (sfxAdjuster == null || SoundManager.Instance == null)
        {
            return;
        }

        SoundManager.Instance.ApplySFXVolume(sfxAdjuster.NormalizedValue);
    }

    private void ApplyPlayerColor()
    {
        if (playerImage == null || redAdjuster == null || greenAdjuster == null || blueAdjuster == null)
        {
            return;
        }

        playerImage.color = new Color(redAdjuster.NormalizedValue, greenAdjuster.NormalizedValue, blueAdjuster.NormalizedValue);
    }

    private static void Subscribe(ValueAdjuster adjuster, System.Action<int> handler)
    {
        if (adjuster == null)
        {
            return;
        }

        adjuster.ValueChanged += handler;
    }

    private static void Unsubscribe(ValueAdjuster adjuster, System.Action<int> handler)
    {
        if (adjuster == null)
        {
            return;
        }

        adjuster.ValueChanged -= handler;
    }
}
