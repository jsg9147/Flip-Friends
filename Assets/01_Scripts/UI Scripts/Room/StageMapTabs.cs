using UnityEngine;
using UnityEngine.UI;

public class StageMapTabs : MonoBehaviour
{
    [SerializeField] private ScreenNavigator navigator;
    [SerializeField] private UIScreen builtInScreen;
    [SerializeField] private UIScreen customScreen;
    [SerializeField] private Button builtInTab;
    [SerializeField] private Button customTab;
    [SerializeField] private Image builtInIndicator;
    [SerializeField] private Image customIndicator;
    [SerializeField] private MapListUI mapList;
    [SerializeField] private Image builtInBackground;
    [SerializeField] private Image customBackground;
    [SerializeField] private Color activeTabColor = new Color(0.64f, 0.93f, 0.75f);
    [SerializeField] private Color inactiveTabColor = Color.white;

    private void Awake()
    {
        builtInTab.onClick.AddListener(ShowBuiltIn);
        customTab.onClick.AddListener(ShowCustom);
    }

    private void OnEnable()
    {
        ShowBuiltIn();
    }

    public void ShowBuiltIn()
    {
        navigator.Open(builtInScreen, false);
        UpdateIndicators(false);
    }

    public void ShowCustom()
    {
        navigator.Open(customScreen, false);
        mapList.RefreshList();
        UpdateIndicators(true);
    }

    private void UpdateIndicators(bool isCustom)
    {
        builtInIndicator.enabled = !isCustom;
        customIndicator.enabled = isCustom;
        builtInBackground.color = isCustom ? inactiveTabColor : activeTabColor;
        customBackground.color = isCustom ? activeTabColor : inactiveTabColor;
    }
}
