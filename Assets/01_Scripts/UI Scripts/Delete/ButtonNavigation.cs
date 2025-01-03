using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using System;

public class ButtonNavigation : MonoBehaviour
{
    public GameObject beforeUI;

    public List<Selectable> uiElements = new(); // 버튼, 드롭다운, 슬라이더 등을 포함한 UI 요소 리스트
    private int currentIndex = 0; // 현재 선택된 UI 요소의 인덱스

    private bool canNavigate = true; // 네비게이션 가능 여부
    private float inputCooldown = 0.2f; // 입력 간 최소 대기 시간
    private float lastInputTime; // 마지막 입력 시간 기록

    void OnEnable()
    {
        InputManager.instance.OnCancelEvent += ReturnUI;
        currentIndex = 0;
        if (uiElements.Count > 0)
        {
            SelectUIElement(currentIndex);
        }
    }

    private void OnDisable()
    {
        if (uiElements.Count > 0)
        {
            if(EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(null);
        }
        InputManager.instance.OnCancelEvent -= ReturnUI;
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
        if (canNavigate)
        {
            if (InputManager.instance.dir.y != 0f && Time.time - lastInputTime > inputCooldown)
            {
                Navigate((int)Mathf.Sign(InputManager.instance.dir.y));
                lastInputTime = Time.time; // 입력 시간을 갱신
                canNavigate = false; // 입력 잠금
            }
        }

        if (InputManager.instance.dir.y == 0f)
        {
            canNavigate = true; // 입력 잠금 해제
        }
    }

    public void AddUIElement(Selectable element)
    {
        uiElements.Add(element);
        AddMouseEnterListener(element, uiElements.Count - 1);
    }

    public void ClearUIElements()
    {
        uiElements.Clear();
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

    public void ReturnUI()
    {
        if (beforeUI != null)
        {
            beforeUI.SetActive(true);
            gameObject.SetActive(false);
        }
    }
}
