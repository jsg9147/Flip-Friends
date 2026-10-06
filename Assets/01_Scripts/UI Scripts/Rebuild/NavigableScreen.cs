using UnityEngine;

// Cancel 입력으로 한 단계 되돌아가는 처리를 화면마다 복사하지 않기 위해 공통 베이스로 둔다.
[DisallowMultipleComponent]
public class NavigableScreen : UIScreen
{
    [SerializeField] private ScreenNavigator navigator;

    protected ScreenNavigator Navigator => navigator;

    // 히스토리가 비었을 때 돌아갈 화면. null이면 Back 실패 시 아무것도 하지 않는다.
    protected virtual string FallbackScreenId => null;

    protected override void Awake()
    {
        base.Awake();

        if (navigator == null)
        {
            navigator = FindAnyObjectByType<ScreenNavigator>(FindObjectsInactive.Include);
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

    // 화면마다 Cancel 전에 정리할 상태가 다르므로 재정의할 수 있게 둔다
    protected virtual void OnCancel()
    {
        NavigateBack();
    }

    protected void NavigateBack()
    {
        if (navigator == null)
        {
            Debug.LogWarning($"'{name}' 화면이 ScreenNavigator를 찾지 못해 뒤로 가기를 무시했습니다.", this);
            return;
        }

        if (navigator.Back() || string.IsNullOrEmpty(FallbackScreenId))
        {
            return;
        }

        // 히스토리 없이 열린 화면에서도 Cancel이 막다른 길이 되지 않게 한다
        navigator.Open(FallbackScreenId, false);
    }

    private void HandleCancel()
    {
        // 보이지 않는 화면까지 Cancel을 처리하면 히스토리가 한 번에 여러 단계 풀린다
        if (!IsVisible)
        {
            return;
        }

        OnCancel();
    }
}
