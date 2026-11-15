using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 키보드·게임패드로 고른 항목을 바깥 테두리와 화살표로 보여 준다.
// 맵 에디터의 현재 도구 표시(PixelButton.SetHighlighted, 안쪽 프레임 교체)와 겹쳐도 구분되도록 버튼 바깥에만 그린다.
// 선택 이벤트 대신 EventSystem 선택을 매 프레임 확인한다. 선택된 채 비활성화됐다 다시 켜지는 화면 전환에서도 표시가 어긋나지 않는다.
[RequireComponent(typeof(Selectable))]
public sealed class SelectionFocusIndicator : MonoBehaviour
{
    [SerializeField] private GameObject indicator;

    private Selectable selectable;

    private void Awake()
    {
        selectable = GetComponent<Selectable>();
        SetVisible(false);
    }

    private void OnDisable()
    {
        SetVisible(false);
    }

    private void LateUpdate()
    {
        EventSystem eventSystem = EventSystem.current;
        // 포인터 모드에서는 마우스 호버가 위치를 보여 주므로 탐색 모드에서만 켠다.
        bool isFocused = eventSystem != null && eventSystem.currentSelectedGameObject == gameObject &&
            !UIInputModeController.IsPointerMode && selectable.IsInteractable();
        SetVisible(isFocused);
    }

    private void SetVisible(bool isVisible)
    {
        if (indicator != null && indicator.activeSelf != isVisible) indicator.SetActive(isVisible);
    }
}
