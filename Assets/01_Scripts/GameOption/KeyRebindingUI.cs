using UnityEngine;

public class KeyRebindingUI : MonoBehaviour
{
    public GameObject settingUI;
    public GameObject keyboardUI;
    public GameObject gamepadUI;

    private void OnEnable()
    {
        if(InputManager.instance != null)
            InputManager.instance.OnCancelEvent += CloseKeyBind;
    }

    private void OnDisable()
    {
        if (InputManager.instance != null)
            InputManager.instance.OnCancelEvent -= CloseKeyBind;
    }

    public void CloseUI()
    {
        gameObject.SetActive(false);
    }

    public void CloseKeyBind()
    {
        if (keyboardUI.activeSelf)
        {
            SetKeyboardUI(false);
        }
        if (gamepadUI.activeSelf)
        {
            SetGamepadUI(false);
        }
    }


    public void SetKeyboardUI(bool isActive)
    {
        keyboardUI.SetActive(isActive);
        settingUI.SetActive(!isActive);
    }
    public void SetGamepadUI(bool isActive)
    {
        gamepadUI.SetActive(isActive);
        settingUI.SetActive(!isActive);
    }
}
