using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class ScreenNavigator : MonoBehaviour
{
    [SerializeField] private UIScreen initialScreen;
    [SerializeField] private List<UIScreen> screens = new();
    [SerializeField] private bool autoDiscoverScreens = true;

    private readonly Dictionary<string, UIScreen> screenLookup = new();
    private readonly Stack<UIScreen> history = new();

    public UIScreen CurrentScreen { get; private set; }

    private void Awake()
    {
        RebuildLookup();
        HideAllScreens();
    }

    private void Start()
    {
        if (initialScreen != null)
        {
            Open(initialScreen, false);
        }
    }

    public void Open(string screenId, bool rememberCurrent = true)
    {
        if (string.IsNullOrWhiteSpace(screenId))
        {
            Debug.LogWarning("ScreenNavigator.Open called with an empty screen id.", this);
            return;
        }

        if (!screenLookup.TryGetValue(screenId, out UIScreen targetScreen) || targetScreen == null)
        {
            Debug.LogWarning($"ScreenNavigator could not find screen '{screenId}'.", this);
            return;
        }

        Open(targetScreen, rememberCurrent);
    }

    public void Open(UIScreen targetScreen, bool rememberCurrent = true)
    {
        if (targetScreen == null)
        {
            Debug.LogWarning("ScreenNavigator.Open called with a null screen.", this);
            return;
        }

        if (CurrentScreen == targetScreen)
        {
            targetScreen.SelectDefault();
            return;
        }

        if (CurrentScreen != null)
        {
            if (rememberCurrent)
            {
                history.Push(CurrentScreen);
            }

            CurrentScreen.Hide();
        }

        CurrentScreen = targetScreen;
        CurrentScreen.Show();
    }

    public bool Back()
    {
        while (history.Count > 0)
        {
            UIScreen previousScreen = history.Pop();

            if (previousScreen == null)
            {
                continue;
            }

            Open(previousScreen, false);
            return true;
        }

        return false;
    }

    public void ClearHistory()
    {
        history.Clear();
    }

    public void Register(UIScreen screen)
    {
        if (screen == null)
        {
            return;
        }

        if (!screens.Contains(screen))
        {
            screens.Add(screen);
        }

        AddToLookup(screen);
    }

    private void RebuildLookup()
    {
        screenLookup.Clear();

        for (int i = screens.Count - 1; i >= 0; i--)
        {
            if (screens[i] == null)
            {
                screens.RemoveAt(i);
            }
        }

        if (autoDiscoverScreens)
        {
            UIScreen[] discoveredScreens = Object.FindObjectsByType<UIScreen>(FindObjectsInactive.Include);

            foreach (UIScreen screen in discoveredScreens)
            {
                if (screen == null || screens.Contains(screen))
                {
                    continue;
                }

                screens.Add(screen);
            }
        }

        foreach (UIScreen screen in screens)
        {
            AddToLookup(screen);
        }
    }

    private void AddToLookup(UIScreen screen)
    {
        if (screen == null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(screen.ScreenId))
        {
            Debug.LogWarning($"UIScreen on '{screen.name}' is missing a screen id.", screen);
            return;
        }

        if (screenLookup.ContainsKey(screen.ScreenId))
        {
            Debug.LogWarning($"Duplicate UIScreen id '{screen.ScreenId}' found on '{screen.name}'.", screen);
            return;
        }

        screenLookup.Add(screen.ScreenId, screen);
    }

    private void HideAllScreens()
    {
        foreach (UIScreen screen in screens)
        {
            if (screen == null)
            {
                continue;
            }

            screen.HideImmediate();
        }

        CurrentScreen = null;
        history.Clear();
    }
}
