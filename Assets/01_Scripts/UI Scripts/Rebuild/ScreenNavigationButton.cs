using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Button))]
public class ScreenNavigationButton : MonoBehaviour
{
    public enum NavigationAction
    {
        OpenScreen,
        Back,
        QuitApplication
    }

    [SerializeField] private ScreenNavigator navigator;
    [SerializeField] private NavigationAction action = NavigationAction.OpenScreen;
    [SerializeField] private string targetScreenId;
    [SerializeField] private bool rememberCurrent = true;

    private Button button;

    private void Awake()
    {
        button = GetComponent<Button>();

        if (navigator == null)
        {
            navigator = FindFirstObjectByType<ScreenNavigator>(FindObjectsInactive.Include);
        }

        button.onClick.AddListener(HandleClick);
    }

    private void OnDestroy()
    {
        if (button != null)
        {
            button.onClick.RemoveListener(HandleClick);
        }
    }

    private void HandleClick()
    {
        if (navigator == null)
        {
            Debug.LogWarning($"ScreenNavigationButton on '{name}' could not find a ScreenNavigator.", this);
            return;
        }

        switch (action)
        {
            case NavigationAction.OpenScreen:
                navigator.Open(targetScreenId, rememberCurrent);
                break;
            case NavigationAction.Back:
                navigator.Back();
                break;
            case NavigationAction.QuitApplication:
                Application.Quit();
                break;
        }
    }
}
