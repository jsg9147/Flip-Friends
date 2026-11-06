using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[DefaultExecutionOrder(-200)]
[DisallowMultipleComponent]
public class UIInputModeController : MonoBehaviour
{
    private const float MouseMoveThreshold = 3f;
    private const float NavigationThreshold = 0.5f;
    private static UIInputModeController instance;
    private readonly List<GraphicRaycaster> suppressedRaycasters = new();
    private GameObject lastSelectedObject;
    private Vector2 mouseAnchor;
    private bool hasMousePosition;

    public static bool IsPointerMode => instance == null || instance.isPointerMode;
    private bool isPointerMode = true;

    private void OnEnable()
    {
        instance = this;
        InputSystem.onActionChange += HandleActionChange;
        hasMousePosition = false;
    }

    private void OnDisable()
    {
        InputSystem.onActionChange -= HandleActionChange;
        RestoreRaycasters();
        if (instance == this) instance = null;
    }

    public static void RequestSelection(GameObject target)
    {
        if (target == null) return;
        if (instance != null) instance.lastSelectedObject = target;
        if (!IsPointerMode && EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(target);
    }

    private void HandleActionChange(object source, InputActionChange change)
    {
        if (change != InputActionChange.ActionPerformed || source is not InputAction action ||
            action.activeControl == null || action.activeControl.device is Mouse) return;

        bool isDirection = action.name == "Move" || action.name == "Navigate";
        bool isCommand = action.name == "Submit" || action.name == "Cancel" || action.name == "Menu";
        if (!isDirection && !isCommand) return;
        if (isDirection && action.ReadValue<Vector2>().sqrMagnitude < NavigationThreshold * NavigationThreshold) return;

        SetPointerMode(false);
    }

    private void Update()
    {
        Mouse mouse = Mouse.current;
        if (mouse != null)
        {
            Vector2 position = mouse.position.ReadValue();
            if (!hasMousePosition)
            {
                mouseAnchor = position;
                hasMousePosition = true;
            }

            bool hasMoved = (position - mouseAnchor).sqrMagnitude >= MouseMoveThreshold * MouseMoveThreshold;
            if (hasMoved || mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame ||
                mouse.middleButton.wasPressedThisFrame || mouse.scroll.ReadValue().sqrMagnitude > 0f)
            {
                mouseAnchor = position;
                SetPointerMode(true);
            }
        }

        if (!isPointerMode) SuppressRaycasters();
        if (EventSystem.current != null)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = isPointerMode;
        }
    }

    private void LateUpdate()
    {
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null) return;
        GameObject selected = eventSystem.currentSelectedGameObject;
        if (selected != null) lastSelectedObject = selected;

        // 텍스트 입력 포커스는 유지해야 마우스로 연 참가 코드 입력을 계속 쓸 수 있다.
        if (isPointerMode && selected != null && selected.GetComponent<TMP_InputField>() == null)
            eventSystem.SetSelectedGameObject(null);
    }

    private void SetPointerMode(bool usePointer)
    {
        if (isPointerMode == usePointer) return;
        isPointerMode = usePointer;
        EventSystem eventSystem = EventSystem.current;
        if (usePointer)
        {
            RestoreRaycasters();
            if (eventSystem != null && eventSystem.currentSelectedGameObject != null)
            {
                lastSelectedObject = eventSystem.currentSelectedGameObject;
                if (lastSelectedObject.GetComponent<TMP_InputField>() == null)
                    eventSystem.SetSelectedGameObject(null);
            }
        }
        else
        {
            SuppressRaycasters();
            if (eventSystem != null && eventSystem.currentSelectedGameObject == null && lastSelectedObject != null &&
                lastSelectedObject.activeInHierarchy && lastSelectedObject.TryGetComponent(out Selectable selectable) &&
                selectable.IsInteractable())
                eventSystem.SetSelectedGameObject(lastSelectedObject);
        }
    }

    private void SuppressRaycasters()
    {
        // 커서만 숨기면 정지한 마우스의 호버가 남는다. 포인터 판정도 함께 쉬어야 한다.
        List<BaseRaycaster> raycasters = RaycasterManager.GetRaycasters();
        for (int i = raycasters.Count - 1; i >= 0; i--)
        {
            if (raycasters[i] is not GraphicRaycaster raycaster || !raycaster.isActiveAndEnabled) continue;
            suppressedRaycasters.Add(raycaster);
            raycaster.enabled = false;
        }
    }

    private void RestoreRaycasters()
    {
        foreach (GraphicRaycaster raycaster in suppressedRaycasters)
            if (raycaster != null) raycaster.enabled = true;
        suppressedRaycasters.Clear();
    }
}
