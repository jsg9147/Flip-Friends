using UnityEngine;

public class MainUI : MonoBehaviour
{
    [SerializeField] private GameObject gameModeMenuUI;
    [SerializeField] private GameObject SettingMenuUI;

    public void GameModeMenuUIOpen()
    {
        gameObject.SetActive(false);
        gameModeMenuUI.SetActive(true);
    }

    public void SettingUIOpen()
    {
        SettingMenuUI.SetActive(true);
    }
}
