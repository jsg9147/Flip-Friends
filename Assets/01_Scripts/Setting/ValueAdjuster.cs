using System;
using UnityEngine;
using TMPro;

public class ValueAdjuster : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private string key = "DefaultKey"; // PlayerPrefs에 사용할 키
    [SerializeField] private int defaultValue = 50;
    [SerializeField] private int minValue = 0;
    [SerializeField] private int maxValue = 100;

    [Header("UI Elements")]
    [SerializeField] private TMP_Text valueText; // 수치 표시 Text
    [SerializeField] private GameObject targetUI; // 조정 후 돌아갈 버튼

    [Header("Hold Repeat")]
    [SerializeField] private float repeatDelay = 0.4f; // 누른 뒤 반복을 시작하기까지 기다리는 시간(초)
    [SerializeField] private float repeatInterval = 0.1f; // 반복 중 한 칸씩 바꾸는 간격(초)

    private int currentValue;

    // 범위가 넓어 한 칸씩만 바꾸면 끝까지 가기 번거로우므로 누르고 있으면 반복한다
    private HorizontalStepInput stepInput;

    // GetFloat이 int로 저장된 키에서 기본값을 돌려주는 것을 이용해 이전 저장 형식을 알아내는 표식
    private const float LegacyFormatMark = -1f;

    // 값을 실제로 쓰는 쪽이 구독한다. 저장은 이 클래스가, 적용은 구독자가 맡아 책임을 나눈다
    public event Action<int> ValueChanged;

    public int Value => currentValue;

    // 저장과 노출은 0~1로 정규화한다. 값을 읽는 SoundManager·PlayerSound·PlayerController2D가 모두 GetFloat을 쓰기 때문이다
    public float NormalizedValue => maxValue <= 0 ? 0f : (float)currentValue / maxValue;

    void Awake()
    {
        stepInput = HorizontalStepInput.Repeating(repeatDelay, repeatInterval);
        LoadValue();
    }

    void Update()
    {
        int direction = stepInput.Read(targetUI);

        if (direction != 0)
        {
            ChangeValue(direction);
        }
    }

    private void ChangeValue(int delta)
    {
        SetValue(currentValue + delta);
    }

    // 마우스 클릭도 키보드·게임패드 입력과 같은 증감 경로를 쓰도록 공개
    public void Increase()
    {
        ChangeValue(1);
    }

    public void Decrease()
    {
        ChangeValue(-1);
    }

    private void UpdateValueText()
    {
        if (valueText == null)
        {
            return;
        }

        valueText.text = currentValue.ToString();
    }

    private void SetValue(int newValue)
    {
        int clampedValue = Mathf.Clamp(newValue, minValue, maxValue);

        if (clampedValue == currentValue)
        {
            return;
        }

        currentValue = clampedValue;
        UpdateValueText();
        SaveValue();
        ValueChanged?.Invoke(currentValue);
    }

    private void LoadValue()
    {
        float normalizedValue = NormalizeDefault();
        bool needsRewrite = true;

        if (PlayerPrefs.HasKey(key))
        {
            // 정규화 값은 0~1이라 음수가 나올 수 없다. 음수면 키가 float이 아니라는 뜻이다
            float savedValue = PlayerPrefs.GetFloat(key, LegacyFormatMark);

            if (savedValue >= 0f)
            {
                normalizedValue = savedValue;
                needsRewrite = false;
            }
            else if (maxValue > 0)
            {
                // 이전 빌드는 같은 키에 0~maxValue 정수를 저장했다. 그 값을 버리지 않고 옮긴다
                normalizedValue = (float)PlayerPrefs.GetInt(key, defaultValue) / maxValue;
            }
        }

        currentValue = Mathf.Clamp(Mathf.RoundToInt(normalizedValue * maxValue), minValue, maxValue);
        UpdateValueText();

        // 설정 UI는 화면을 처음 열 때 깨어난다. 그때 구독자가 저장된 값을 따라올 수 있게 알린다
        ValueChanged?.Invoke(currentValue);

        // 저장된 값이 없거나 이전 정수 형식이면 지금 형식으로 다시 적어 둔다
        if (needsRewrite)
        {
            SaveValue();
        }
    }

    private float NormalizeDefault()
    {
        return maxValue <= 0 ? 0f : (float)defaultValue / maxValue;
    }

    private void SaveValue()
    {
        PlayerPrefs.SetFloat(key, NormalizedValue);
        PlayerPrefs.Save();
    }
}
