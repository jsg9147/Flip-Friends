using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class ScrollViewController : MonoBehaviour
{
    public ScrollRect scrollRect; // ScrollRect 컴포넌트
    public RectTransform content; // ScrollView의 Content
    public RectTransform selectedButton; // 선택된 버튼의 RectTransform

    public ButtonSelectController buttonSelectController;

    private void Start()
    {
        buttonSelectController = GetComponent<ButtonSelectController>();
    }

    private void Update()
    {
        if (EventSystem.current.currentSelectedGameObject != null)
        {
            if (buttonSelectController.tagetButtonList.Contains(EventSystem.current.currentSelectedGameObject.GetComponent<Button>()))
            {
                selectedButton = EventSystem.current.currentSelectedGameObject.transform.parent.GetComponent<RectTransform>();
                CenterOnButton();
            }
        }
    }

    public void CenterOnButton()
    {
        // Viewport 높이와 Content 높이 가져오기
        float viewportHeight = scrollRect.viewport.rect.height;
        float contentHeight = content.rect.height;

        // 선택된 버튼의 anchoredPosition.y 사용
        float buttonCenterY = -selectedButton.anchoredPosition.y; // 스크롤 방향에 따라 반전 필요

        // Content 높이에서 버튼 위치 비율 계산
        float normalizedPositionY = 1 - ((buttonCenterY - (viewportHeight / 2)) / (contentHeight - viewportHeight));

        // ScrollRect의 normalizedPosition 설정
        scrollRect.normalizedPosition = new Vector2(scrollRect.normalizedPosition.x, Mathf.Clamp01(normalizedPositionY));
    }

}
