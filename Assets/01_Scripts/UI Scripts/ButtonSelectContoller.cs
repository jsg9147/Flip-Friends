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
        tagetButtonList[0].Select();
        currentIndex = 0;
    }

    private void Update()
    {
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
            tagetButtonList[currentIndex].Select();
        }
    }

    private void SelectPreviousButton()
    {
        currentIndex = (currentIndex - 1 + tagetButtonList.Count) % tagetButtonList.Count;
        tagetButtonList[currentIndex].Select();
    }

    private void SelectNextButton()
    {
        currentIndex = (currentIndex + 1) % tagetButtonList.Count;
        tagetButtonList[currentIndex].Select();
    }

    private bool IsAnyButtonSelected()
    {
        GameObject selectedObject = EventSystem.current.currentSelectedGameObject;
        return selectedObject != null && tagetButtonList.Exists(selectable => selectable != null && selectable.gameObject == selectedObject);
    }

    public void SelectFirstBtn()
    {
        tagetButtonList[0].Select();
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
