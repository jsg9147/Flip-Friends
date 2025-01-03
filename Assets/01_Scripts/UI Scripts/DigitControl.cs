using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class DigitControl : MonoBehaviour
{
    public Button upButton; // 숫자를 증가시키는 버튼
    public Button downButton; // 숫자를 감소시키는 버튼
    public Button boder; // 숫자를 감소시키는 버튼
    public TMP_Text digitText; // 숫자를 표시하는 텍스트

    public void SetDigit(int digit)
    {
        digitText.text = digit.ToString();
    }

    public void SelectEvent()
    {
        boder.Select();
    }
}
