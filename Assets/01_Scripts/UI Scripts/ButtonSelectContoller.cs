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
        SelectFirstBtn();
    }

    private void Update()
    {
        if (InputManager.instance == null || tagetButtonList == null || tagetButtonList.Count == 0) return;
        if (Time.time - lastInputTime > inputDelay)
        {
            if (InputManager.instance.dir.y > 0)
            {
                SelectPreviousButton();
                lastInputTime = Time.time;
            }
            else if (InputManager.instance.dir.y < 0)
            {
                SelectNextButton();
                lastInputTime = Time.time;
            }
        }

        if (!IsAnyButtonSelected())
        {
            SelectFirstBtn();
        }
    }

    private void SelectPreviousButton()
    {
        MoveSelection(-1);
    }

    private void SelectNextButton()
    {
        MoveSelection(1);
    }

    private void MoveSelection(int direction)
    {
        if (tagetButtonList == null || tagetButtonList.Count == 0) return;
        GameObject selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        int selectedIndex = tagetButtonList.FindIndex(item => item != null && item.gameObject == selected);
        if (selectedIndex >= 0) currentIndex = selectedIndex;
        for (int i = 0; i < tagetButtonList.Count; i++)
        {
            currentIndex = (currentIndex + direction + tagetButtonList.Count) % tagetButtonList.Count;
            if (!IsSelectable(tagetButtonList[currentIndex])) continue;
            tagetButtonList[currentIndex].Select();
            return;
        }
    }

    private static bool IsSelectable(Selectable item) =>
        item != null && item.IsActive() && item.IsInteractable();

    private bool IsAnyButtonSelected()
    {
        GameObject selectedObject = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        return selectedObject != null && tagetButtonList.Exists(selectable => IsSelectable(selectable) && selectable.gameObject == selectedObject);
    }

    public void SelectFirstBtn()
    {
        if (tagetButtonList == null) return;
        for (int i = 0; i < tagetButtonList.Count; i++)
        {
            if (!IsSelectable(tagetButtonList[i])) continue;
            currentIndex = i;
            tagetButtonList[i].Select();
            return;
        }
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
