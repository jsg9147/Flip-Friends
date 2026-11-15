using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MapEditorHUD : MonoBehaviour
{
    private enum ConfirmationAction
    {
        None,
        Overwrite,
        Delete
    }

    [Header("맵 이름")]
    [SerializeField] private TMP_InputField mapNameInput;
    [SerializeField] private TMP_Text statusText;

    [Header("맵 불러오기 팝업")]
    [SerializeField] private GameObject loadPopup;
    [SerializeField] private Transform loadListContainer;
    [SerializeField] private GameObject loadListItemPrefab;

    [Tooltip("불러오기 목록 항목마다 붙이는 삭제 버튼")]
    [SerializeField] private PixelButton buttonPrefab;

    [Header("HUD 버튼")]
    [SerializeField] private PixelButton newMapButton;
    [SerializeField] private PixelButton loadButton;
    [SerializeField] private PixelButton saveButton;
    [SerializeField] private PixelButton returnButton;
    [SerializeField] private PixelButton testPlayButton;
    [SerializeField] private PixelButton placeButton;
    [SerializeField] private PixelButton deleteButton;
    [SerializeField] private PixelButton rotateButton;
    [SerializeField] private PixelButton flipButton;

    [Header("상태 표시")]
    [SerializeField] private TMP_Text validationText;
    [SerializeField] private TMP_Text selectedObjectText;
    [SerializeField] private TMP_Text testHintText;

    [Header("팝업")]
    [SerializeField] private Button loadPopupBlocker;
    [SerializeField] private GameObject overwritePopup;
    [SerializeField] private TMP_Text overwriteMessage;
    [SerializeField] private PixelButton confirmButton;
    [SerializeField] private PixelButton cancelButton;

    [Header("키보드·패드 탐색")]
    [SerializeField] private PaletteUI paletteUI;

    private ObjectTransformEditor transformEditor;
    private GameObject confirmationReturnSelection;
    private bool isCancelSubscribed;
    private ConfirmationAction pendingConfirmation;
    private string pendingDeleteMapName;
    private bool isStartingTestPlay;
    private string selectedObjectLabel = "오브젝트를 선택하세요";

    // 배치는 MapEditor 씬에 있다(1600×900 기준). 여기서는 동작만 연결한다.
    private void Start()
    {
        if (!ValidateReferences()) return;

        placeButton.Button.onClick.AddListener(() => ObjectTransformEditorMode(false));
        loadPopupBlocker.onClick.AddListener(() => SetLoadPopupVisible(false));
        confirmButton.Button.onClick.AddListener(ConfirmPendingAction);
        cancelButton.Button.onClick.AddListener(CancelConfirmation);
        overwritePopup.SetActive(false);
        statusText.text = $"배치 도구 | {selectedObjectLabel} 선택됨";
        ShowCurrentValidation();
        MapDataRepository.OperationFailed += ShowError;
        SubscribeToMapChanges();
        InitializeDeleteModeUI();
        mapNameInput.onEndEdit.AddListener(OnMapNameChanged);
        mapNameInput.text = MapEditorManager.instance?.CurrentMapData?.mapName ?? "새 맵";
        SetLoadPopupVisible(false);
        LinkHudNavigation();
        SubscribeToCancel();
        // Main에서 남은 선택은 씬과 함께 사라졌다. 키보드·패드로 처음 움직일 때 배치 도구부터 고르게 한다.
        if (placeButton != null) UIInputModeController.RequestSelection(placeButton.gameObject);
    }

    // 자동 탐색은 화면 가운데가 비어 있어 반전↓대포, 새 맵↓대포처럼 먼 버튼으로 튄다.
    // 상단 막대(가로)·왼쪽 팔레트(세로)·오른쪽 도구(세로) 세 구역으로 나눠 직접 잇는다.
    private void LinkHudNavigation()
    {
        IReadOnlyList<Button> palette = paletteUI != null ? paletteUI.Buttons : System.Array.Empty<Button>();
        if (paletteUI == null) Debug.LogWarning("MapEditorHUD에 PaletteUI 참조가 없어 팔레트 키보드 탐색을 잇지 못했습니다.", this);
        Selectable firstPalette = palette.Count > 0 ? palette[0] : null;

        List<Selectable> topBar = CollectSelectables(mapNameInput, ButtonOf(newMapButton), ButtonOf(loadButton), ButtonOf(saveButton),
            ButtonOf(returnButton), ButtonOf(testPlayButton));
        List<Selectable> tools = CollectSelectables(ButtonOf(placeButton), ButtonOf(deleteButton), ButtonOf(rotateButton), ButtonOf(flipButton));
        Selectable firstTool = tools.Count > 0 ? tools[0] : null;

        // 상단 막대 왼쪽 절반은 팔레트로, 오른쪽 절반은 도구로 내려간다(화면 위치와 맞춘다).
        for (int i = 0; i < topBar.Count; i++)
        {
            Selectable down = i < topBar.Count / 2 ? firstPalette : firstTool;
            SetExplicitNavigation(topBar[i], null, down ?? firstTool ?? firstPalette,
                i > 0 ? topBar[i - 1] : null, i < topBar.Count - 1 ? topBar[i + 1] : null);
        }

        Selectable topLeft = topBar.Count > 0 ? topBar[0] : null;
        Selectable topRight = topBar.Count > 0 ? topBar[topBar.Count - 1] : null;
        for (int i = 0; i < palette.Count; i++)
            SetExplicitNavigation(palette[i], i > 0 ? palette[i - 1] : topLeft,
                i < palette.Count - 1 ? palette[i + 1] : null, null, firstTool);
        for (int i = 0; i < tools.Count; i++)
            SetExplicitNavigation(tools[i], i > 0 ? tools[i - 1] : topRight,
                i < tools.Count - 1 ? tools[i + 1] : null, firstPalette, null);
    }

    private static Selectable ButtonOf(PixelButton button)
    {
        return button != null ? button.Button : null;
    }

    private static List<Selectable> CollectSelectables(params Selectable[] candidates)
    {
        List<Selectable> result = new();
        foreach (Selectable candidate in candidates)
            if (candidate != null) result.Add(candidate);
        return result;
    }

    private void SubscribeToCancel()
    {
        if (InputManager.instance == null) return;
        InputManager.instance.OnCancelEvent += HandleCancel;
        isCancelSubscribed = true;
    }

    // 키보드 Esc·패드 B는 열린 팝업만 닫는다. 에디터 나가기는 저장 안 된 편집을 잃을 수 있어 버튼으로만 한다.
    private void HandleCancel()
    {
        if (overwritePopup != null && overwritePopup.activeSelf)
            CancelConfirmation();
        else if (loadPopup.activeSelf)
            SetLoadPopupVisible(false);
    }

    public void SetSelectedObjectLabel(string label)
    {
        selectedObjectLabel = string.IsNullOrWhiteSpace(label) ? "오브젝트를 선택하세요" : label;
        if (selectedObjectText != null)
            selectedObjectText.text = $"{selectedObjectLabel} 선택됨";
        if (statusText != null && (transformEditor == null || !transformEditor.IsDeleteMode))
            statusText.text = $"배치 도구 | {selectedObjectLabel} 선택됨";
    }

    private void ObjectTransformEditorMode(bool isDeleteMode)
    {
        ObjectTransformEditor editor = GetTransformEditor();
        if (editor == null) return;
        if (editor.IsDeleteMode != isDeleteMode) editor.ToggleDeleteMode();
    }

    private void OnMapNameChanged(string newName)
    {
        if (MapEditorManager.instance?.SetMapName(newName) == true)
        {
            mapNameInput.text = MapEditorManager.instance.CurrentMapData.mapName;
            return;
        }

        ShowMapNameError(newName);
    }

    public void OnSaveButtonClicked()
    {
        MapEditorManager manager = MapEditorManager.instance;
        if (manager == null || !manager.SetMapName(mapNameInput.text))
        {
            ShowMapNameError(mapNameInput.text);
            return;
        }

        mapNameInput.text = manager.CurrentMapData.mapName;
        if (!MapDataRepository.TryExists(
                manager.CurrentMapData.mapName,
                out bool alreadyExists))
            return;

        if (alreadyExists)
        {
            ShowOverwritePopup(manager.CurrentMapData.mapName);
            return;
        }

        SaveCurrentMap(false);
    }

    public void OnNewMapButtonClicked()
    {
        MapEditorManager manager = MapEditorManager.instance;
        if (manager == null || !manager.NewMap(mapNameInput.text))
        {
            ShowMapNameError(mapNameInput.text);
            return;
        }

        ObjectPlacer.instance?.ClearDisplayObjects();
        mapNameInput.text = manager.CurrentMapData.mapName;
        ShowValidation(manager.ValidateCurrentMap());
        ShowStatus($"새 맵 생성: {manager.CurrentMapData.mapName}", Color.white);
    }

    public void OnLoadButtonClicked()
    {
        bool isOpen = !loadPopup.activeSelf;
        SetLoadPopupVisible(isOpen);
        if (isOpen) PopulateLoadList();
    }

    public void OnReturnButtonClicked()
    {
        if (MapEditorManager.instance == null)
        {
            Debug.LogError("Main으로 돌아갈 수 없습니다. MapEditorManager 참조가 없습니다.", this);
            return;
        }

        MapEditorManager.instance.ReturnToMain();
    }

    public async void OnTestPlayButtonClicked()
    {
        if (isStartingTestPlay) return;
        MapEditorManager manager = MapEditorManager.instance;
        if (manager == null)
        {
            Debug.LogError("테스트 플레이를 시작할 수 없습니다. MapEditorManager 참조가 없습니다.", this);
            return;
        }

        ShowCurrentValidation();
        isStartingTestPlay = true;
        try
        {
            ShowStatus("테스트 플레이를 시작합니다...", Color.white);
            string error = await manager.StartTestPlayAsync();
            if (this != null && error != null) ShowError(error);
        }
        catch (System.Exception error)
        {
            Debug.LogException(error, this);
            if (this != null) ShowError("테스트 플레이를 시작하지 못했습니다.");
        }
        finally
        {
            isStartingTestPlay = false;
        }
    }

    public void OnRotateButtonClicked()
    {
        GetTransformEditor()?.RotatePlacementPreview();
    }

    public void OnFlipButtonClicked()
    {
        GetTransformEditor()?.FlipPlacementPreview();
    }

    public void OnDeleteButtonClicked()
    {
        GetTransformEditor()?.ToggleDeleteMode();
    }

    private void PopulateLoadList()
    {
        foreach (Transform child in loadListContainer)
            Destroy(child.gameObject);

        List<string> mapNames = MapDataRepository.GetAllMapNames();
        List<Button> items = new();
        List<Button> deleteButtons = new();
        foreach (string mapName in mapNames)
        {
            if (!CreateLoadListItem(mapName, out Button item, out Button deleteItemButton)) continue;
            items.Add(item);
            deleteButtons.Add(deleteItemButton);
        }

        LinkLoadListNavigation(items, deleteButtons);
        // 목록이 비면 불러오기 버튼에 선택이 남아 같은 버튼으로 팝업을 다시 닫을 수 있다.
        if (items.Count > 0) UIInputModeController.RequestSelection(items[0].gameObject);
    }

    // 자동 탐색은 팝업 뒤 HUD 버튼까지 고르므로, 목록 안에서만 움직이도록 직접 잇는다(이름 ↔ 삭제, 위아래는 같은 열).
    private static void LinkLoadListNavigation(List<Button> items, List<Button> deleteButtons)
    {
        for (int i = 0; i < items.Count; i++)
        {
            Button previousItem = i > 0 ? items[i - 1] : null;
            Button nextItem = i < items.Count - 1 ? items[i + 1] : null;
            SetExplicitNavigation(items[i], previousItem, nextItem, null, deleteButtons[i]);
            SetExplicitNavigation(deleteButtons[i], i > 0 ? deleteButtons[i - 1] : null,
                i < deleteButtons.Count - 1 ? deleteButtons[i + 1] : null, items[i], null);
        }
    }

    private static void SetExplicitNavigation(Selectable selectable, Selectable up, Selectable down, Selectable left, Selectable right)
    {
        selectable.navigation = new Navigation
        {
            mode = Navigation.Mode.Explicit,
            selectOnUp = up,
            selectOnDown = down,
            selectOnLeft = left,
            selectOnRight = right
        };
    }

    private bool CreateLoadListItem(string mapName, out Button button, out Button deleteItemButton)
    {
        deleteItemButton = null;
        GameObject item = Instantiate(loadListItemPrefab, loadListContainer);
        TMP_Text label = item.GetComponentInChildren<TMP_Text>();
        button = item.GetComponent<Button>();
        if (label == null || button == null)
        {
            Debug.LogError($"맵 목록 버튼 프리팹에 필요한 TMP_Text 또는 Button이 없습니다: {loadListItemPrefab.name}", loadListItemPrefab);
            Destroy(item);
            return false;
        }

        label.text = mapName;
        deleteItemButton = AddDeleteButton(item, label, mapName);
        button.onClick.AddListener(() =>
        {
            MapEditorManager.instance?.LoadMap(mapName);
            mapNameInput.text = mapName;
            ShowCurrentValidation();
            SetLoadPopupVisible(false);
        });
        return true;
    }

    private Button AddDeleteButton(GameObject item, TMP_Text label, string mapName)
    {
        RectTransform labelRect = label.rectTransform;
        labelRect.offsetMax = new Vector2(-130f, labelRect.offsetMax.y);

        PixelButton deleteItemButton = Instantiate(buttonPrefab, item.transform);
        deleteItemButton.name = "DeleteButton";
        RectTransform deleteRect = (RectTransform)deleteItemButton.transform;
        deleteRect.anchorMin = new Vector2(1f, 0.5f);
        deleteRect.anchorMax = new Vector2(1f, 0.5f);
        deleteRect.anchoredPosition = new Vector2(-70f, 0f);
        deleteRect.sizeDelta = new Vector2(100f, 48f);
        deleteItemButton.Text = "삭제";
        deleteItemButton.SetFontRange(14f, 20f);
        deleteItemButton.SetLabelInsets(18f, 18f);
        deleteItemButton.Button.onClick.AddListener(() => ShowDeletePopup(mapName, deleteItemButton.gameObject));
        return deleteItemButton.Button;
    }

    private bool ValidateReferences()
    {
        bool isValid = true;
        isValid &= ValidateReference(mapNameInput, nameof(mapNameInput));
        isValid &= ValidateReference(loadPopup, nameof(loadPopup));
        isValid &= ValidateReference(loadListContainer, nameof(loadListContainer));
        isValid &= ValidateReference(loadListItemPrefab, nameof(loadListItemPrefab));
        isValid &= ValidateReference(buttonPrefab, nameof(buttonPrefab));
        isValid &= ValidateReference(statusText, nameof(statusText));
        isValid &= ValidateReference(placeButton, nameof(placeButton));
        isValid &= ValidateReference(loadPopupBlocker, nameof(loadPopupBlocker));
        isValid &= ValidateReference(overwritePopup, nameof(overwritePopup));
        isValid &= ValidateReference(overwriteMessage, nameof(overwriteMessage));
        isValid &= ValidateReference(confirmButton, nameof(confirmButton));
        isValid &= ValidateReference(cancelButton, nameof(cancelButton));
        return isValid;
    }

    private ObjectTransformEditor GetTransformEditor()
    {
        if (transformEditor != null) return transformEditor;

        transformEditor = ObjectPlacer.instance?.GetComponent<ObjectTransformEditor>();
        if (transformEditor != null) return transformEditor;

        Debug.LogError("HUD 편집 동작을 처리할 ObjectTransformEditor가 없습니다.", this);
        return null;
    }

    private void InitializeDeleteModeUI()
    {
        transformEditor = GetTransformEditor();
        if (transformEditor == null) return;

        transformEditor.DeleteModeChanged += OnDeleteModeChanged;
        OnDeleteModeChanged(transformEditor.IsDeleteMode);
    }

    private void OnDeleteModeChanged(bool isEnabled)
    {
        if (statusText != null)
            statusText.text = isEnabled ? "지우개 도구 선택됨" : $"배치 도구 | {selectedObjectLabel} 선택됨";

        if (deleteButton != null) deleteButton.SetHighlighted(isEnabled);
        if (placeButton != null) placeButton.SetHighlighted(!isEnabled);
    }

    private void OnDestroy()
    {
        MapDataRepository.OperationFailed -= ShowError;
        if (isCancelSubscribed && InputManager.instance != null)
            InputManager.instance.OnCancelEvent -= HandleCancel;
        UnsubscribeFromMapChanges();
        if (transformEditor != null)
            transformEditor.DeleteModeChanged -= OnDeleteModeChanged;
    }

    private void SubscribeToMapChanges()
    {
        if (MapEditorManager.instance != null)
            MapEditorManager.instance.MapDataChanged += ShowCurrentValidation;
    }

    private void UnsubscribeFromMapChanges()
    {
        if (MapEditorManager.instance != null)
            MapEditorManager.instance.MapDataChanged -= ShowCurrentValidation;
    }

    private void SetLoadPopupVisible(bool isVisible)
    {
        bool wasVisible = loadPopup.activeSelf;
        loadPopupBlocker.gameObject.SetActive(isVisible);
        loadPopup.SetActive(isVisible);
        if (wasVisible && !isVisible && loadButton != null)
            UIInputModeController.RequestSelection(loadButton.gameObject);
    }

    private void ShowOverwritePopup(string mapName)
    {
        pendingConfirmation = ConfirmationAction.Overwrite;
        overwriteMessage.text = $"'{mapName}' 맵이 이미 존재합니다.\n덮어쓰시겠습니까?";
        OpenConfirmation(saveButton != null ? saveButton.gameObject : null);
    }

    private void ShowDeletePopup(string mapName, GameObject returnSelection)
    {
        pendingDeleteMapName = mapName;
        pendingConfirmation = ConfirmationAction.Delete;
        overwriteMessage.text = $"'{mapName}' 맵을 삭제하시겠습니까?\n이 작업은 되돌릴 수 없습니다.";
        OpenConfirmation(returnSelection);
    }

    // 덮어쓰기·삭제는 되돌릴 수 없으므로 패드로 연속 입력해도 바로 실행되지 않게 취소를 먼저 고른다.
    private void OpenConfirmation(GameObject returnSelection)
    {
        confirmationReturnSelection = returnSelection;
        overwritePopup.SetActive(true);
        UIInputModeController.RequestSelection(cancelButton.gameObject);
    }

    private void CloseConfirmation()
    {
        overwritePopup.SetActive(false);
        if (confirmationReturnSelection != null)
            UIInputModeController.RequestSelection(confirmationReturnSelection);
        confirmationReturnSelection = null;
    }

    private void ConfirmPendingAction()
    {
        CloseConfirmation();
        if (pendingConfirmation == ConfirmationAction.Overwrite)
            SaveCurrentMap(true);
        else if (pendingConfirmation == ConfirmationAction.Delete)
            DeletePendingMap();

        ClearPendingConfirmation();
    }

    private void CancelConfirmation()
    {
        CloseConfirmation();
        string message = pendingConfirmation == ConfirmationAction.Delete
            ? "삭제를 취소했습니다."
            : "저장을 취소했습니다.";
        ClearPendingConfirmation();
        ShowStatus(message, Color.white);
    }

    private void SaveCurrentMap(bool replaceExistingFile)
    {
        MapEditorManager manager = MapEditorManager.instance;
        if (manager == null) return;

        MapValidationReport report = manager.ValidateCurrentMap();
        ShowValidation(report);
        if (manager.SaveMap(replaceExistingFile, out MapSaveResult _))
            ShowStatus($"저장 완료: {manager.CurrentMapData.mapName}", Color.white);
    }

    private void ShowCurrentValidation()
    {
        MapEditorManager manager = MapEditorManager.instance;
        if (manager != null)
            ShowValidation(manager.ValidateCurrentMap());
    }

    private void ShowValidation(MapValidationReport report)
    {
        if (validationText == null || report == null) return;
        if (report.Issues.Count == 0)
        {
            validationText.text = "플레이 가능성 검증 통과";
            validationText.color = new Color(0f, 0.247f, 0.282f, 1f);
            if (testHintText != null) testHintText.text = "테스트 플레이를 실행할 수 있어요";
            return;
        }

        validationText.text = BuildValidationMessage(report);
        validationText.color = report.HasErrors
            ? new Color(0.65f, 0.17f, 0.2f)
            : new Color(0f, 0.247f, 0.282f, 1f);
        if (testHintText != null)
            testHintText.text = report.CanStartPlay
                ? "테스트 플레이를 실행할 수 있어요"
                : "시작점과 도착점을 배치하면 테스트할 수 있어요";
    }

    private string BuildValidationMessage(MapValidationReport report)
    {
        if (report.CanStartPlay)
            return $"플레이 가능 | 경고 {report.Issues.Count}건";

        MapValidationIssue firstIssue = report.Issues[0];
        return report.Issues.Count == 1
            ? firstIssue.Message
            : $"{firstIssue.Message} 외 {report.Issues.Count - 1}건";
    }

    private void DeletePendingMap()
    {
        string mapName = pendingDeleteMapName;
        if (!MapDataRepository.Delete(mapName)) return;

        PopulateLoadList();
        ShowStatus($"삭제 완료: {mapName}", Color.white);
    }

    private void ClearPendingConfirmation()
    {
        pendingConfirmation = ConfirmationAction.None;
        pendingDeleteMapName = null;
    }

    private void ShowMapNameError(string mapName)
    {
        if (MapDataRepository.TryNormalizeMapName(mapName, out _, out string error))
            error = "맵 이름을 변경할 수 없습니다.";

        ShowError(error);
    }

    private void ShowError(string message)
    {
        ShowStatus(message, new Color(1f, 0.35f, 0.35f));
    }

    private void ShowStatus(string message, Color color)
    {
        if (statusText == null) return;

        statusText.text = message;
        statusText.color = color == Color.white ? new Color(0f, 0.247f, 0.282f, 1f) : color;
    }

    private bool ValidateReference(Object reference, string fieldName)
    {
        if (reference != null) return true;

        Debug.LogError($"MapEditorHUD의 Inspector 참조가 누락되었습니다: {fieldName}", this);
        return false;
    }
}
