using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class StageSelectBtnEvent : MonoBehaviour
{
    [SerializeField] private List<Selectable> uiElements = new();

    public void ButtonInit()
    {
        SelectFirstValid(uiElements);
    }

    public void SelectFirstValidIn(Transform root)
    {
        if (root == null) return;

        SelectFirstValid(root.GetComponentsInChildren<Selectable>(false));
    }

    private void SelectFirstValid(IEnumerable<Selectable> selectables)
    {
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null) return;

        foreach (Selectable selectable in selectables)
        {
            if (!IsValid(selectable)) continue;

            eventSystem.SetSelectedGameObject(selectable.gameObject);
            return;
        }
    }

    private bool IsValid(Selectable selectable) =>
        selectable != null &&
        selectable.gameObject.activeInHierarchy &&
        selectable.IsActive() &&
        selectable.IsInteractable();
}
