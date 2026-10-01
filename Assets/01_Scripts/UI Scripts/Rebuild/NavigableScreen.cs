using UnityEngine;

// Cancel 입력으로 한 단계 되돌아가는 처리를 화면마다 복사하지 않기 위해 공통 베이스로 둔다.
[DisallowMultipleComponent]
public class NavigableScreen : UIScreen
{
    [SerializeField] private ScreenNavigator navigator;

    protected ScreenNavigator Navigator => navigator;

    protected override void Awake()
    {
        base.Awake();

        if (navigator == null)
        {
            navigator = FindFirstObjectByType<ScreenNavigator>(FindObjectsInactive.Include);
        }
    }

    protected virtual void OnEnable()
    {
        if (InputManager.instance != null)
        {
            InputManager.instance.OnCancelEvent += HandleCancel;
        }
    }

    protected virtual void OnDisable()
    {
        if (InputManager.instance != null)
        {
            InputManager.instance.OnCancelEvent -= HandleCancel;
        }
    }

    private void HandleCancel()
    {
        // 보이지 않는 화면까지 Cancel을 처리하면 히스토리가 한 번에 여러 단계 풀린다
        if (!IsVisible)
        {
            return;
        }

        if (navigator == null)
        {
            Debug.LogWarning($"'{name}' 화면이 ScreenNavigator를 찾지 못해 Cancel 입력을 무시했습니다.", this);
            return;
        }

        navigator.Back();
    }
}
