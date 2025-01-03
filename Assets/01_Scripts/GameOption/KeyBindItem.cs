using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class KeyBindItem : MonoBehaviour
{
    public TMP_Text antionNameText;
    public TMP_Text keyBindText;

    public Button keyBindBtn;

    public void SetBindText(string keyText)
    {
        keyBindText.text = keyText;
    }
}
