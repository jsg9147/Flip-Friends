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
}
