using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class ButtonNav : MonoBehaviour
{
    public List<Selectable> uiElements = new(); // 버튼, 드롭다운, 슬라이더 등을 포함한 UI 요소 리스트
    private int currentIndex = 0; // 현재 선택된 UI 요소의 인덱스
    private GameObject lastSelectedObj;

    private float inputDelay = 0.2f; // 딜레이 시간 (초)
    private float lastInputTime = 0f; // 마지막 입력 시간

    void OnEnable()
    {
        if (uiElements.Count > 0)
        {
            SelectUIElement(currentIndex);
        }
    }

    private void OnDisable()
    {
        if (uiElements.Count > 0)
        {
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(null);
        }
    }

    void Start()
    {
        for (int i = 0; i < uiElements.Count; i++)
        {
            AddMouseEnterListener(uiElements[i], i);
        }

        if (uiElements.Count > 0)
        {
            SelectUIElement(currentIndex);
        }
    }

    private void Update()
    {
        if (EventSystem.current.currentSelectedGameObject == null || !EventSystem.current.currentSelectedGameObject.activeSelf)
        {
            EventSystem.current.SetSelectedGameObject(lastSelectedObj);
        }

        if (EventSystem.current.currentSelectedGameObject != lastSelectedObj)
        {
            lastSelectedObj = EventSystem.current.currentSelectedGameObject;
        }

        // 딜레이 확인 후 방향키 입력 감지
        if (Time.time - lastInputTime >= inputDelay)
        {
            float horizontal = InputManager.instance.dir.x;
            float vertical = InputManager.instance.dir.y;

            if (horizontal != 0 || vertical != 0)
            {
                NavigateByDirection(new Vector2(horizontal, vertical));
                lastInputTime = Time.time; // 마지막 입력 시간 업데이트
            }
        }
    }

    private void AddMouseEnterListener(Selectable uiElement, int index)
    {
        EventTrigger trigger = uiElement.gameObject.GetComponent<EventTrigger>();
        if (trigger == null)
        {
            trigger = uiElement.gameObject.AddComponent<EventTrigger>();
        }

        EventTrigger.Entry entry = new EventTrigger.Entry
        {
            eventID = EventTriggerType.PointerEnter
        };
        entry.callback.AddListener((data) => OnMouseEnterInUI(index));
        trigger.triggers.Add(entry);
    }

    private void OnMouseEnterInUI(int index)
    {
        currentIndex = index; // 인덱스 업데이트
        SelectUIElement(currentIndex);
    }

    private void NavigateByDirection(Vector2 direction)
    {
        if (uiElements.Count == 0) return;

        RectTransform currentElement = uiElements[currentIndex].GetComponent<RectTransform>();
        Vector2 currentPosition = currentElement.position;

        // 후보 리스트를 담을 변수
        List<int> candidates = new List<int>();

        // 좌우 방향 입력일 경우 우선적으로 좌우만 확인
        if (direction.x != 0f)
        {
            // direction.x > 0 이면 오른쪽, < 0 이면 왼쪽에 있는 요소만 후보로
            foreach (var uiElement in uiElements)
            {
                if (uiElement == uiElements[currentIndex]) continue;
                RectTransform element = uiElement.GetComponent<RectTransform>();
                Vector2 elementPos = element.position;
                Vector2 diff = elementPos - currentPosition;

                // 좌우 우선 탐색: diff.x 방향이 입력 방향과 같은지 확인
                if ((direction.x > 0 && diff.x > 0) || (direction.x < 0 && diff.x < 0))
                {
                    candidates.Add(uiElements.IndexOf(uiElement));
                }
            }
        }

        int closestIndex = currentIndex;
        float closestDistance = float.MaxValue;

        // 좌/우 탐색에서 후보를 찾지 못했다면 기존 로직 수행
        if (candidates.Count == 0)
        {
            for (int i = 0; i < uiElements.Count; i++)
            {
                if (i == currentIndex) continue; // 현재 선택된 요소는 스킵

                RectTransform element = uiElements[i].GetComponent<RectTransform>();
                Vector2 elementPosition = element.position;

                Vector2 directionToElement = elementPosition - currentPosition;
                float angle = Vector2.Dot(direction.normalized, directionToElement.normalized);

                if (angle > 0.5f) // 방향이 일정 범위 내에 있는 경우
                {
                    float distance = directionToElement.sqrMagnitude;
                    if (distance < closestDistance)
                    {
                        closestDistance = distance;
                        closestIndex = i;
                    }
                }
            }
        }
        else
        {
            // 좌/우 방향 후보가 있을 경우, 거리 비교를 통해 가장 가까운 것을 찾는다.
            foreach (var i in candidates)
            {
                RectTransform element = uiElements[i].GetComponent<RectTransform>();
                Vector2 elementPosition = element.position;

                Vector2 directionToElement = elementPosition - currentPosition;
                float distance = directionToElement.sqrMagnitude;
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestIndex = i;
                }
            }
        }

        // 가장 가까운 UI 요소 선택
        if (closestIndex != currentIndex)
        {
            currentIndex = closestIndex;
            SelectUIElement(currentIndex);
        }
    }


    public void SelectUIElement(int index)
    {
        EventSystem.current.SetSelectedGameObject(uiElements[index].gameObject);
    }

    public void AddUI(Selectable ui)
    {
        uiElements.Add(ui);
    }
}
