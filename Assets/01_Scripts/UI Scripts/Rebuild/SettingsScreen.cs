using UnityEngine;

// 설정 트리의 루트 화면. 하위 창과 같은 내비게이터를 쓰되 화면별 로직을 담을 자리를 분리해 둔다.
public class SettingsScreen : NavigableScreen
{
    [SerializeField] private SettingManager settingManager;

    protected override void Awake()
    {
        base.Awake();

        if (settingManager == null)
        {
            settingManager = FindAnyObjectByType<SettingManager>(FindObjectsInactive.Include);
        }
    }

    // 하위 창에서 돌아왔을 때도 확정한다. 해상도 ±는 하위 창에 있어서 OnHide만 두면 그 창에서 바뀐 값이 늦게 적용된다
    protected override void OnShow()
    {
        base.OnShow();
        ApplyPendingSettings();
    }

    // 해상도는 ±를 누를 때마다 바꾸면 조작 중 창이 계속 흔들려서, 설정 화면을 벗어날 때 확정한다
    protected override void OnHide()
    {
        base.OnHide();
        ApplyPendingSettings();
    }

    private void ApplyPendingSettings()
    {
        if (settingManager == null)
        {
            Debug.LogWarning($"'{name}' 화면이 SettingManager를 찾지 못해 설정을 적용하지 못했습니다.", this);
            return;
        }

        settingManager.ApplySettings();
    }
}
