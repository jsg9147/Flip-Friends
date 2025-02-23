using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Mirror;

public class StageSelectBtnEvent : NetworkBehaviour
{
    public List<Selectable> uiElements = new(); // 버튼, 드롭다운, 슬라이더 등을 포함한 UI 요소 리스트
    private GameObject lastSelectedObj;

    private void Update()
    {
        if (!isServer)
            return;

        if (EventSystem.current.currentSelectedGameObject == null)
        {
            RpcSelectUIElement(0);
        }

        if (EventSystem.current.currentSelectedGameObject != lastSelectedObj)
        {
            lastSelectedObj = EventSystem.current.currentSelectedGameObject;
            int index = uiElements.FindIndex(x => x.gameObject == lastSelectedObj);
            RpcSelectUIElement(index);
        }
    }

    public void ButtonInit()
    {
        RpcSelectUIElement(0);
    }

    [ClientRpc]

    public void RpcSelectUIElement(int index)
    {
        EventSystem.current.SetSelectedGameObject(uiElements[index].gameObject);
    }
}
