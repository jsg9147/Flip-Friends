using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;

public class ValueAdjuster : MonoBehaviour
{
    [Header("Settings")]
    public string key = "DefaultKey"; // PlayerPrefs에 사용할 키
    public int defaultValue = 50; // 기본값
    public int minValue = 0; // 최소값
    public int maxValue = 100; // 최대값

    [Header("UI Elements")]
    public TMP_Text valueText; // 수치 표시 Text
    public GameObject targetUI; // 조정 후 돌아갈 버튼

    public int value;

    void Awake()
    {
        // PlayerPrefs에서 저장된 값을 불러옴. 없으면 기본값 사용.
        value = PlayerPrefs.GetInt(key, defaultValue);
        UpdateValueText();
    }

    void Update()
    {
        if (EventSystem.current.currentSelectedGameObject == targetUI)
        {
            HandleAdjustmentInput();
        }
    }

    private void HandleAdjustmentInput()
    {
        if (InputManager.instance.dir.x > 0)
        {
            ChangeValue(1); // 오른쪽 입력으로 값 증가
        }
        else if (InputManager.instance.dir.x < 0)
        {
            ChangeValue(-1); // 왼쪽 입력으로 값 감소
        }
    }

    private void ChangeValue(int delta)
    {
        value = Mathf.Clamp(value + delta, minValue, maxValue);
        UpdateValueText();
        SaveValue();
    }

    public void UpdateValueText()
    {
        valueText.text = value.ToString();
    }

    private void SaveValue()
    {
        PlayerPrefs.SetInt(key, value);
        PlayerPrefs.Save();
    }
}
