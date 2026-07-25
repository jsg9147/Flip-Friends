using UnityEngine;

[DisallowMultipleComponent]
public class ModeSelectScreen : UIScreen
{
    [SerializeField] private ScreenNavigator navigator;

    protected override void Awake()
    {
        base.Awake();

        if (navigator == null)
        {
            navigator = FindFirstObjectByType<ScreenNavigator>(FindObjectsInactive.Include);
        }
    }

    private void OnEnable()
    {
        if (InputManager.instance != null)
        {
            InputManager.instance.OnCancelEvent += HandleCancel;
        }
    }

    private void OnDisable()
    {
        if (InputManager.instance != null)
        {
            InputManager.instance.OnCancelEvent -= HandleCancel;
        }
    }

    private void HandleCancel()
    {
        if (navigator != null && IsVisible)
        {
            navigator.Back();
        }
    }
}
