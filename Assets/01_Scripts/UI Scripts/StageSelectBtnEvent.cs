using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class StageSelectBtnEvent : MonoBehaviour
{
    public List<Selectable> uiElements = new(); // 버튼, 드롭다운, 슬라이더 등을 포함한 UI 요소 리스트
    private int currentIndex = 0; // 현재 선택된 UI 요소의 인덱스

    private GameObject lastSelectedObj;
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

    private void Navigate(int direction)
    {
        if (uiElements.Count == 0) return;

        if (SoundManager.Instance != null)
            SoundManager.Instance.PlayClickSound();

        currentIndex -= direction;

        if (currentIndex < 0)
        {
            currentIndex = uiElements.Count - 1;
        }
        else if (currentIndex >= uiElements.Count)
        {
            currentIndex = 0;
        }

        SelectUIElement(currentIndex);
    }

    public void SelectUIElement(int index)
    {
        EventSystem.current.SetSelectedGameObject(uiElements[index].gameObject);
    }
}
