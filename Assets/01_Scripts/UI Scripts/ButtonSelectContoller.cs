using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using UnityEngine.EventSystems;

public class ButtonSelectController : MonoBehaviour
{
    public List<Selectable> tagetButtonList;
    private int currentIndex = 0;

    public float inputDelay = 0.2f;
    private float lastInputTime = 0f;

    private void OnEnable()
    {
        currentIndex = 0;
        if (tagetButtonList.Count > 0)
            UIInputModeController.RequestSelection(tagetButtonList[0].gameObject);
    }

    private void Update()
    {
        if (EventSystem.current == null || tagetButtonList.Count == 0) return;
        GameObject selected = EventSystem.current.currentSelectedGameObject;
        int selectedIndex = tagetButtonList.FindIndex(item => item != null && item.gameObject == selected);
        if (selectedIndex >= 0) currentIndex = selectedIndex;
        if (UIInputModeController.IsPointerMode) return;

        if (InputManager.instance != null && Time.unscaledTime - lastInputTime > inputDelay)
        {
            if (InputManager.instance.dir.y > 0)
            {
                SelectPreviousButton();
                lastInputTime = Time.unscaledTime;
            }
            else if (InputManager.instance.dir.y < 0)
            {
                SelectNextButton();
                lastInputTime = Time.unscaledTime;
            }
        }

        if (!IsAnyButtonSelected())
        {
            SelectButton(1);
        }
    }

    private void SelectPreviousButton()
    {
        SelectButton(-1);
    }

    private void SelectNextButton()
    {
        SelectButton(1);
    }

    private void SelectButton(int direction)
    {
        for (int offset = 1; offset <= tagetButtonList.Count; offset++)
        {
            int index = (currentIndex + direction * offset + tagetButtonList.Count) % tagetButtonList.Count;
            var selectable = tagetButtonList[index];
            if (selectable == null || !selectable.IsActive() || !selectable.IsInteractable()) continue;
            currentIndex = index;
            selectable.Select();
            return;
        }
    }

    private bool IsAnyButtonSelected()
    {
        GameObject selectedObject = EventSystem.current.currentSelectedGameObject;
        return selectedObject != null && tagetButtonList.Exists(selectable => selectable != null &&
            selectable.gameObject == selectedObject && selectable.IsActive() && selectable.IsInteractable());
    }

    public void SelectFirstBtn()
    {
        if (tagetButtonList.Count > 0)
            UIInputModeController.RequestSelection(tagetButtonList[0].gameObject);
    }

    // 런타임에 생기는 항목은 고정 버튼 사이에 끼워 넣어야 하므로 위치를 받는다
    public void InsertSelectable(int index, Selectable selectable)
    {
        if (selectable == null || tagetButtonList.Contains(selectable))
        {
            return;
        }

        tagetButtonList.Insert(Mathf.Clamp(index, 0, tagetButtonList.Count), selectable);
    }

    public void RemoveSelectable(Selectable selectable)
    {
        int index = tagetButtonList.IndexOf(selectable);
        if (index < 0)
        {
            return;
        }

        tagetButtonList.RemoveAt(index);

        // 항목이 빠진 뒤에도 Update가 범위 밖 인덱스로 선택하지 않게 맞춘다
        if (currentIndex > index || currentIndex >= tagetButtonList.Count)
        {
            currentIndex = Mathf.Max(0, currentIndex - 1);
        }
    }
}
