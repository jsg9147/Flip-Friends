using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class ScrollViewController : MonoBehaviour
{
    [SerializeField] private ScrollRect scrollRect;
    [SerializeField] private RectTransform content;
    [SerializeField] private float edgePadding = 10f;

    private GameObject lastSelected;

    public RectTransform Content => content;

    // 선택이 바뀐 순간에만 스크롤한다. 매 프레임 맞추면 마우스 휠 스크롤을 계속 되돌린다
    private void LateUpdate()
    {
        GameObject selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        if (selected == lastSelected)
        {
            return;
        }

        lastSelected = selected;

        RectTransform item = selected != null ? FindContentChild(selected.transform) : null;
        if (item != null)
        {
            ScrollIntoView(item);
        }
    }

    private void OnDisable()
    {
        lastSelected = null;
    }

    // 선택 대상이 항목 루트일 수도(LobbyItem), 항목 안의 버튼일 수도(KeyBindItem) 있어서 Content 바로 아래 자식을 기준으로 삼는다
    private RectTransform FindContentChild(Transform target)
    {
        for (Transform current = target; current != null; current = current.parent)
        {
            if (current.parent == content)
            {
                return current as RectTransform;
            }
        }

        return null;
    }

    // 보이는 영역 밖에 있을 때만 필요한 만큼 움직인다. 이미 보이는 항목을 클릭해도 목록이 튀지 않는다
    private void ScrollIntoView(RectTransform item)
    {
        if (scrollRect == null || !scrollRect.vertical)
        {
            return;
        }

        RectTransform viewport = scrollRect.viewport != null ? scrollRect.viewport : (RectTransform)scrollRect.transform;

        // 같은 프레임에 생성된 항목은 레이아웃이 아직 계산되지 않았을 수 있다
        Canvas.ForceUpdateCanvases();

        float scrollableHeight = content.rect.height - viewport.rect.height;
        if (scrollableHeight <= 0f)
        {
            return;
        }

        Bounds itemBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(viewport, item);
        Rect viewRect = viewport.rect;

        float overflow = 0f;
        if (itemBounds.max.y + edgePadding > viewRect.yMax)
        {
            overflow = itemBounds.max.y + edgePadding - viewRect.yMax;
        }
        else if (itemBounds.min.y - edgePadding < viewRect.yMin)
        {
            overflow = itemBounds.min.y - edgePadding - viewRect.yMin;
        }

        if (Mathf.Approximately(overflow, 0f))
        {
            return;
        }

        scrollRect.StopMovement();
        scrollRect.verticalNormalizedPosition = Mathf.Clamp01(scrollRect.verticalNormalizedPosition + overflow / scrollableHeight);
    }
}
