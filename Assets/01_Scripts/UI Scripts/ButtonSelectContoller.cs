using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using UnityEngine.EventSystems;

public class ButtonSelectController : MonoBehaviour
{
    public List<Button> tagetButtonList; // 타겟 버튼 리스트
    private int currentIndex = 0; // 현재 선택된 버튼 인덱스

    public float inputDelay = 0.2f; // 입력 딜레이 (초 단위)
    private float lastInputTime = 0f; // 마지막 입력 시간

    private void OnEnable()
    {
        // 첫 번째 버튼 선택
        tagetButtonList[0].Select();
        currentIndex = 0;
    }

    private void Update()
    {
        // 입력 딜레이 체크
        if (Time.time - lastInputTime > inputDelay)
        {
            // 방향키 입력 처리
            if (InputManager.instance.dir.y > 0)
            {
                SelectPreviousButton();
                lastInputTime = Time.time; // 입력 시간 갱신
            }
            else if (InputManager.instance.dir.y < 0)
            {
                SelectNextButton();
                lastInputTime = Time.time; // 입력 시간 갱신
            }
        }

        // 버튼 선택 상태 확인
        if (!IsAnyButtonSelected())
        {
            // 인덱스 기반으로 버튼 다시 선택
            tagetButtonList[currentIndex].Select();
        }
    }

    private void SelectPreviousButton()
    {
        // 이전 버튼 선택 (리스트 순환)
        currentIndex = (currentIndex - 1 + tagetButtonList.Count) % tagetButtonList.Count;
        tagetButtonList[currentIndex].Select();
    }

    private void SelectNextButton()
    {
        // 다음 버튼 선택 (리스트 순환)
        currentIndex = (currentIndex + 1) % tagetButtonList.Count;
        tagetButtonList[currentIndex].Select();
    }

    private bool IsAnyButtonSelected()
    {
        // 현재 UI 시스템에서 선택된 객체가 버튼 리스트에 포함되어 있는지 확인
        GameObject selectedObject = UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject;
        return selectedObject != null && tagetButtonList.Exists(button => button.gameObject == selectedObject);
    }

    public void SelectFirstBtn()
    {
        tagetButtonList[0].Select();
    }
}
