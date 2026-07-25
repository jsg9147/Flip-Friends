using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class DigitControl : MonoBehaviour
{
    public Button upButton;
    public Button downButton;
    public Button boder;
    public TMP_Text digitText;

    public int CurrentDigit { get; private set; }

    public void SetDigit(int digit)
    {
        CurrentDigit = digit;
        digitText.text = digit.ToString();
    }

    public void SelectEvent()
    {
        boder.Select();
    }
}
