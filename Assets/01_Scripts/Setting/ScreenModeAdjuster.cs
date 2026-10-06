using System;
using UnityEngine;
using TMPro;

public class ScreenModeAdjuster : MonoBehaviour
{
    [SerializeField] private TMP_Text screenModeText;
    [SerializeField] private GameObject targetUI; // 이 버튼이 선택돼 있을 때만 좌우 입력을 받는다

    private static readonly FullScreenMode[] SupportedModes =
    {
        FullScreenMode.FullScreenWindow,
        FullScreenMode.Windowed
    };

    private const string ScreenModeKey = "ScreenMode";

    private int currentIndex;

    // 값이 둘뿐이라 누르고 있는 동안 반복하면 깜빡이다가 임의의 값에서 멈춘다
    private readonly HorizontalStepInput stepInput = HorizontalStepInput.PressOnly();

    public FullScreenMode SelectedMode => SupportedModes[currentIndex];

    void Awake()
    {
        LoadScreenMode();
        UpdateScreenModeText();
    }

    void Update()
    {
        int direction = stepInput.Read(targetUI);

        if (direction != 0)
        {
            ChangeScreenMode(direction);
        }
    }

    // 마우스 클릭도 키보드·게임패드 입력과 같은 전환 경로를 쓰도록 공개
    public void NextScreenMode()
    {
        ChangeScreenMode(1);
    }

    public void PreviousScreenMode()
    {
        ChangeScreenMode(-1);
    }

    // 표시만 바꾼다. 실제 적용은 해상도와 함께 설정 화면을 벗어날 때 SettingManager.ApplySettings가 맡는다
    private void ChangeScreenMode(int direction)
    {
        currentIndex = (currentIndex + direction + SupportedModes.Length) % SupportedModes.Length;
        UpdateScreenModeText();
    }

    private void UpdateScreenModeText()
    {
        if (screenModeText == null)
        {
            return;
        }

        screenModeText.text = SelectedMode == FullScreenMode.Windowed ? "Windowed" : "Fullscreen";
    }

    private void LoadScreenMode()
    {
        // 저장값이 없으면 지금 창의 모드를 따른다. Player Settings 기본값이나 실행 인자로 정해진 모드를 덮어쓰지 않기 위함이다
        FullScreenMode mode = Screen.fullScreenMode;

        if (PlayerPrefs.HasKey(ScreenModeKey))
        {
            string savedMode = PlayerPrefs.GetString(ScreenModeKey);

            if (!Enum.TryParse(savedMode, out mode))
            {
                Debug.LogWarning($"저장된 화면 모드 '{savedMode}'를 읽을 수 없어 현재 화면 모드를 씁니다.", this);
                mode = Screen.fullScreenMode;
            }
        }

        currentIndex = IndexOf(mode);
    }

    // 설정 화면을 드나들 때마다 호출되므로 저장값과 같으면 디스크에 쓰지 않는다
    public void SaveScreenMode()
    {
        string selectedMode = SelectedMode.ToString();

        if (PlayerPrefs.GetString(ScreenModeKey) == selectedMode)
        {
            return;
        }

        PlayerPrefs.SetString(ScreenModeKey, selectedMode);
        PlayerPrefs.Save();
    }

    // 목록에 없는 모드(ExclusiveFullScreen, MaximizedWindow)는 가장 가까운 쪽으로 묶는다
    private static int IndexOf(FullScreenMode mode)
    {
        FullScreenMode supportedMode = mode == FullScreenMode.ExclusiveFullScreen ? FullScreenMode.FullScreenWindow
            : mode == FullScreenMode.MaximizedWindow ? FullScreenMode.Windowed
            : mode;

        int index = Array.IndexOf(SupportedModes, supportedMode);
        return index >= 0 ? index : 0;
    }
}
