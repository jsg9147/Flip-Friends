using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

public class ObjectTransformEditor : MonoBehaviour
{
    [Header("참조")]
    [SerializeField] private ObjectPlacer objectPlacer;

    [Header("편집")]
    [SerializeField] private float rotationStep = 15f;
    [SerializeField] private Color selectionColor = new(0.35f, 1f, 1f, 1f);

    private PlacedObjectView selectedView;
    private readonly HashSet<Vector3> paintedGridPositions = new();
    private bool isPlacementDragging;
    private bool isDeleteMode;

    public PlacedObjectView SelectedView => selectedView;
    public bool IsDeleteMode => isDeleteMode;
    public event Action<bool> DeleteModeChanged;

    private void Awake()
    {
        if (objectPlacer == null)
            objectPlacer = GetComponent<ObjectPlacer>();

        if (objectPlacer != null) return;

        Debug.LogError("ObjectTransformEditor의 ObjectPlacer 참조가 누락되었습니다.", this);
        enabled = false;
    }

    private void Update()
    {
        HandlePointerRelease();
        if (isDeleteMode)
        {
            UpdateDeleteHover();
            HandleContinuousDelete();
        }
        else
        {
            HandlePrimaryPointer();
            HandleContinuousPlacement();
        }

        HandleKeyboard();
    }

    public void ClearSelection()
    {
        Select(null);
    }

    public void RotatePlacementPreview()
    {
        SetDeleteMode(false);
        objectPlacer.RotatePreview(rotationStep);
    }

    public void FlipPlacementPreview()
    {
        SetDeleteMode(false);
        objectPlacer.FlipPreview();
    }

    public bool ToggleDeleteMode()
    {
        SetDeleteMode(!isDeleteMode);
        return isDeleteMode;
    }

    public void ExitDeleteMode()
    {
        SetDeleteMode(false);
    }

    public void DeleteSelected()
    {
        if (!TryGetSelection(out PlacedObjectView view)) return;

        ClearSelection();
        objectPlacer.Remove(view);
    }

    private void HandlePrimaryPointer()
    {
        if (!Input.GetMouseButtonDown(0) || IsPointerOverUI()) return;

        ClearSelection();
        BeginPlacementDrag();
    }

    private void BeginPlacementDrag()
    {
        isPlacementDragging = true;
        paintedGridPositions.Clear();
        PlaceAtCurrentGridPosition();
    }

    private void HandleContinuousPlacement()
    {
        if (!isPlacementDragging || !Input.GetMouseButton(0) || IsPointerOverUI()) return;
        PlaceAtCurrentGridPosition();
    }

    private void PlaceAtCurrentGridPosition()
    {
        Vector3 position = objectPlacer.GetSnappedWorldPosition();
        if (!paintedGridPositions.Add(position)) return;

        objectPlacer.TryPlaceCurrent(position);
    }

    private void UpdateDeleteHover()
    {
        if (IsPointerOverUI())
        {
            ClearSelection();
            return;
        }

        Select(objectPlacer.FindTopViewAt(objectPlacer.GetWorldMousePosition()));
    }

    private void HandleContinuousDelete()
    {
        if (!Input.GetMouseButton(0) || IsPointerOverUI()) return;
        if (!TryGetSelection(out PlacedObjectView view)) return;

        Select(null);
        objectPlacer.Remove(view);
    }

    private void HandlePointerRelease()
    {
        if (!Input.GetMouseButtonUp(0)) return;

        isPlacementDragging = false;
        paintedGridPositions.Clear();
    }

    private void HandleKeyboard()
    {
        if (IsKeyboardInputBlocked()) return;

        if (Input.GetKeyDown(KeyCode.R))
            RotatePlacementPreview();
        if (Input.GetKeyDown(KeyCode.F))
            FlipPlacementPreview();
        if (!isDeleteMode &&
            (Input.GetKeyDown(KeyCode.Delete) || Input.GetKeyDown(KeyCode.Backspace)))
            DeleteSelected();
        if (Input.GetKeyDown(KeyCode.Escape))
            SetDeleteMode(false);
    }

    private void Select(PlacedObjectView view)
    {
        if (selectedView == view) return;

        if (selectedView != null)
            selectedView.SetSelected(false, selectionColor);

        selectedView = view;
        if (selectedView != null)
            selectedView.SetSelected(true, selectionColor);

        objectPlacer.SetGhostVisible(selectedView == null && !isDeleteMode);
    }

    private void SetDeleteMode(bool shouldEnable)
    {
        if (isDeleteMode == shouldEnable)
        {
            ClearSelection();
            return;
        }

        isDeleteMode = shouldEnable;
        isPlacementDragging = false;
        paintedGridPositions.Clear();
        ClearSelection();
        objectPlacer.SetGhostVisible(!isDeleteMode);
        DeleteModeChanged?.Invoke(isDeleteMode);
    }

    private bool TryGetSelection(out PlacedObjectView view)
    {
        view = selectedView;
        if (view != null && view.Data != null) return true;

        if (view != null)
            Debug.LogWarning($"데이터 연결이 소실된 오브젝트 선택을 해제합니다: {view.name}", view);
        ClearSelection();
        return false;
    }

    private bool IsKeyboardInputBlocked()
    {
        GameObject selectedUI = EventSystem.current?.currentSelectedGameObject;
        if (selectedUI == null) return false;

        TMP_InputField tmpInput = selectedUI.GetComponent<TMP_InputField>();
        if (tmpInput != null && tmpInput.isFocused) return true;

        InputField legacyInput = selectedUI.GetComponent<InputField>();
        return legacyInput != null && legacyInput.isFocused;
    }

    private bool IsPointerOverUI()
    {
        if (EventSystem.current != null)
            return EventSystem.current.IsPointerOverGameObject();

        Debug.LogError("UI 입력 차단에 필요한 EventSystem이 씬에 없습니다.", this);
        enabled = false;
        return true;
    }

    private void OnValidate()
    {
        if (rotationStep <= 0f)
            rotationStep = 15f;
    }
}
