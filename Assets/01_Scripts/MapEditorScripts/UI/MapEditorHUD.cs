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

    [Header("도트 공통 프리팹")]
    [SerializeField] private PixelButton buttonPrefab;
    [SerializeField] private PixelButton primaryButtonPrefab;
    [SerializeField] private Image panelPrefab;
    [SerializeField] private Sprite fieldFrame;

    [Header("HUD 버튼")]
    [SerializeField] private PixelButton newMapButton;
    [SerializeField] private PixelButton loadButton;
    [SerializeField] private PixelButton saveButton;
    [SerializeField] private PixelButton returnButton;
    [SerializeField] private PixelButton testPlayButton;
    [SerializeField] private PixelButton deleteButton;
    [SerializeField] private PixelButton rotateButton;
    [SerializeField] private PixelButton flipButton;

    private static readonly Color Ink = new Color32(7, 53, 56, 255);
    // 메뉴 화면의 남색 도트 배경과 같은 색이라 Main↔MapEditor 전환 때 바탕이 튀지 않는다.
    private static readonly Color EditorBackground = new Color32(5, 38, 52, 255);
    private const float ToolButtonHeight = 64f;
    private const float ToolButtonSpacing = 76f;

    private ObjectTransformEditor transformEditor;
    private PixelButton placeButton;
    private TMP_Text validationText;
    private GameObject loadPopupBlocker;
    private GameObject overwritePopup;
    private TMP_Text overwriteMessage;
    private ConfirmationAction pendingConfirmation;
    private string pendingDeleteMapName;
    private bool isStartingTestPlay;
    private TMP_Text selectedObjectText;
    private TMP_Text testHintText;
    private string selectedObjectLabel = "오브젝트를 선택하세요";

    private void Start()
    {
        if (!ValidateReferences()) return;

        ConfigureEditorLayout();
        InitializeStatusText();
        InitializeValidationText();
        InitializeLoadPopupBlocker();
        InitializeOverwritePopup();
        MapDataRepository.OperationFailed += ShowError;
        SubscribeToMapChanges();
        InitializeDeleteModeUI();
        mapNameInput.onEndEdit.AddListener(OnMapNameChanged);
        mapNameInput.text = MapEditorManager.instance?.CurrentMapData?.mapName ?? "새 맵";
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

    // 1600×900 기준으로 상단 맵 관리 막대, 왼쪽 팔레트, 오른쪽 도구 패널, 하단 상태 막대를 배치한다.
    private void ConfigureEditorLayout()
    {
        RectTransform canvasRect = transform.parent as RectTransform;
        if (canvasRect == null) return;

        HorizontalLayoutGroup headerLayout = GetComponent<HorizontalLayoutGroup>();
        if (headerLayout != null) headerLayout.enabled = false;
        ApplyPanelFrame(GetComponent<Image>());
        RectTransform headerRect = (RectTransform)transform;
        headerRect.anchorMin = new Vector2(0f, 1f);
        headerRect.anchorMax = new Vector2(1f, 1f);
        headerRect.pivot = new Vector2(0.5f, 1f);
        headerRect.anchoredPosition = new Vector2(0f, -8f);
        headerRect.sizeDelta = new Vector2(-16f, 88f);

        TMP_Text managementTitle = CreateLayoutLabel(canvasRect, "맵 관리", new Vector2(0f, 1f), new Vector2(140f, -52f), new Vector2(120f, 42f), 26f);
        managementTitle.alignment = TextAlignmentOptions.MidlineLeft;
        TMP_Text mapNameLabel = CreateLayoutLabel(canvasRect, "맵 이름", new Vector2(0f, 1f), new Vector2(250f, -52f), new Vector2(70f, 42f), 18f);
        mapNameLabel.alignment = TextAlignmentOptions.MidlineRight;
        PlaceMapNameInput(canvasRect);
        PlaceButton(newMapButton, canvasRect, "새 맵", new Vector2(0f, 1f), new Vector2(690f, -52f), new Vector2(130f, ToolButtonHeight));
        PlaceButton(loadButton, canvasRect, "불러오기", new Vector2(0f, 1f), new Vector2(840f, -52f), new Vector2(160f, ToolButtonHeight));
        PlaceButton(saveButton, canvasRect, "저장", new Vector2(0f, 1f), new Vector2(995f, -52f), new Vector2(140f, ToolButtonHeight));
        PlaceButton(returnButton, canvasRect, "나가기", new Vector2(0f, 1f), new Vector2(1150f, -52f), new Vector2(160f, ToolButtonHeight));
        PlaceButton(testPlayButton, canvasRect, "테스트 플레이", new Vector2(1f, 1f), new Vector2(-150f, -52f), new Vector2(260f, ToolButtonHeight));

        ConfigurePalettePanel(canvasRect);

        Image toolRail = CreatePanel("ToolRail", canvasRect, "도구");
        SetRect(toolRail.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-132f, 0f), new Vector2(248f, -220f));
        placeButton = Instantiate(buttonPrefab, toolRail.transform);
        placeButton.name = "PlaceButton";
        placeButton.Button.onClick.AddListener(() => ObjectTransformEditorMode(false));
        PlaceToolButton(placeButton, toolRail.transform, "배치", 0);
        PlaceToolButton(deleteButton, toolRail.transform, "지우개", 1);
        PlaceToolButton(rotateButton, toolRail.transform, "회전", 2);
        PlaceToolButton(flipButton, toolRail.transform, "반전", 3);
        selectedObjectText = CreateLayoutLabel(toolRail.transform, $"{selectedObjectLabel} 선택됨", new Vector2(0.5f, 1f),
            new Vector2(0f, -110f - ToolButtonSpacing * 4f), new Vector2(196f, 60f), 18f);
        selectedObjectText.textWrappingMode = TextWrappingModes.Normal;

        Image statusBar = CreatePanel("StatusBar", canvasRect, null);
        RectTransform statusRect = statusBar.rectTransform;
        statusRect.anchorMin = Vector2.zero;
        statusRect.anchorMax = new Vector2(1f, 0f);
        statusRect.pivot = new Vector2(0.5f, 0f);
        statusRect.anchoredPosition = new Vector2(0f, 8f);
        statusRect.sizeDelta = new Vector2(-16f, 92f);
        testHintText = CreateLayoutLabel(canvasRect, "도착 지점을 배치하면 테스트할 수 있어요", Vector2.zero, new Vector2(1190f, 54f), new Vector2(560f, 28f), 17f);
        testHintText.alignment = TextAlignmentOptions.MidlineRight;
        ConfigureEditorCamera();
    }

    private void ConfigurePalettePanel(RectTransform canvasRect)
    {
        Transform palettePanel = canvasRect.Find("PalettePanel");
        if (palettePanel == null) return;
        SetRect((RectTransform)palettePanel, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(132f, 0f), new Vector2(248f, -220f));
        ApplyPanelFrame(palettePanel.GetComponent<Image>());
        if (palettePanel.Find("ButtonContainer") is RectTransform containerRect)
        {
            containerRect.anchorMin = Vector2.zero;
            containerRect.anchorMax = Vector2.one;
            containerRect.offsetMin = new Vector2(24f, 28f);
            containerRect.offsetMax = new Vector2(-24f, -84f);
        }
        if (palettePanel.Find("tile_icon") is RectTransform iconRect)
            SetRect(iconRect, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(52f, -52f), new Vector2(28f, 28f));
        TMP_Text paletteTitle = CreateLayoutLabel(palettePanel, "오브젝트", new Vector2(0f, 1f), new Vector2(144f, -52f), new Vector2(140f, 40f), 24f);
        paletteTitle.alignment = TextAlignmentOptions.MidlineLeft;
    }

    private void PlaceMapNameInput(RectTransform canvasRect)
    {
        mapNameInput.transform.SetParent(canvasRect, false);
        SetRect((RectTransform)mapNameInput.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(455f, -52f), new Vector2(320f, 60f));
        if (mapNameInput.image != null && fieldFrame != null)
        {
            mapNameInput.image.sprite = fieldFrame;
            mapNameInput.image.type = Image.Type.Sliced;
            mapNameInput.image.color = Color.white;
        }
        mapNameInput.textComponent.color = Ink;
        mapNameInput.textComponent.alignment = TextAlignmentOptions.MidlineLeft;
        mapNameInput.textComponent.fontSize = 22f;
        mapNameInput.pointSize = 22f;
        if (mapNameInput.placeholder is TMP_Text placeholder)
        {
            placeholder.color = new Color(Ink.r, Ink.g, Ink.b, 0.6f);
            placeholder.alignment = TextAlignmentOptions.MidlineLeft;
        }
        if (mapNameInput.textViewport != null)
        {
            mapNameInput.textViewport.offsetMin = new Vector2(20f, 10f);
            mapNameInput.textViewport.offsetMax = new Vector2(-20f, -10f);
        }
    }

    private void PlaceButton(PixelButton button, RectTransform parent, string label, Vector2 anchor, Vector2 position, Vector2 size)
    {
        if (button == null) return;
        button.transform.SetParent(parent, false);
        SetRect((RectTransform)button.transform, anchor, anchor, position, size);
        button.Text = label;
        button.SetFontRange(16f, 24f);
    }

    private void PlaceToolButton(PixelButton button, Transform rail, string label, int index)
    {
        PlaceButton(button, (RectTransform)rail, label, new Vector2(0.5f, 1f),
            new Vector2(0f, -110f - ToolButtonSpacing * index), new Vector2(196f, ToolButtonHeight));
    }

    private Image CreatePanel(string panelName, Transform parent, string title)
    {
        Image panel = Instantiate(panelPrefab, parent);
        panel.name = panelName;
        TMP_Text titleText = panel.GetComponentInChildren<TMP_Text>(true);
        if (titleText != null)
        {
            titleText.gameObject.SetActive(title != null);
            titleText.text = title ?? string.Empty;
            titleText.fontSizeMax = 26f;
            titleText.rectTransform.offsetMin = new Vector2(28f, -76f);
            titleText.rectTransform.offsetMax = new Vector2(-28f, -28f);
        }
        return panel;
    }

    private void ApplyPanelFrame(Image image)
    {
        if (image == null || panelPrefab == null) return;
        image.sprite = panelPrefab.sprite;
        image.type = Image.Type.Sliced;
        image.pixelsPerUnitMultiplier = 1f;
        image.color = Color.white;
        image.raycastTarget = false;
    }

    private void ConfigureEditorCamera()
    {
        Camera editorCamera = null;
        foreach (Camera candidate in FindObjectsByType<Camera>())
        {
            if (candidate.gameObject.scene == gameObject.scene)
            {
                editorCamera = candidate;
                break;
            }
        }
        if (editorCamera == null) return;
        editorCamera.clearFlags = CameraClearFlags.SolidColor;
        editorCamera.backgroundColor = EditorBackground;
    }

    private void ObjectTransformEditorMode(bool isDeleteMode)
    {
        ObjectTransformEditor editor = GetTransformEditor();
        if (editor == null) return;
        if (editor.IsDeleteMode != isDeleteMode) editor.ToggleDeleteMode();
    }

    private void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 position, Vector2 size)
    {
        if (rect == null) return;
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        rect.pivot = new Vector2(0.5f, 0.5f);
    }

    private TMP_Text CreateLayoutLabel(Transform parent, string label, Vector2 anchor, Vector2 position, Vector2 size, float fontSize)
    {
        GameObject labelObject = CreateUIObject($"Label_{label}", parent);
        RectTransform rect = labelObject.GetComponent<RectTransform>();
        SetRect(rect, anchor, anchor, position, size);
        TextMeshProUGUI text = labelObject.AddComponent<TextMeshProUGUI>();
        text.font = mapNameInput.textComponent.font;
        text.fontSize = fontSize;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Ink;
        text.raycastTarget = false;
        text.text = label;
        return text;
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
        foreach (string mapName in mapNames)
        {
            CreateLoadListItem(mapName);
        }
    }

    private void CreateLoadListItem(string mapName)
    {
        GameObject item = Instantiate(loadListItemPrefab, loadListContainer);
        TMP_Text label = item.GetComponentInChildren<TMP_Text>();
        Button button = item.GetComponent<Button>();
        if (label == null || button == null)
        {
            Debug.LogError($"맵 목록 버튼 프리팹에 필요한 TMP_Text 또는 Button이 없습니다: {loadListItemPrefab.name}", loadListItemPrefab);
            Destroy(item);
            return;
        }

        label.text = mapName;
        AddDeleteButton(item, label, mapName);
        button.onClick.AddListener(() =>
        {
            MapEditorManager.instance?.LoadMap(mapName);
            mapNameInput.text = mapName;
            ShowCurrentValidation();
            SetLoadPopupVisible(false);
        });
    }

    private void AddDeleteButton(GameObject item, TMP_Text label, string mapName)
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
        deleteItemButton.Button.onClick.AddListener(() => ShowDeletePopup(mapName));
    }

    private bool ValidateReferences()
    {
        bool isValid = true;
        isValid &= ValidateReference(mapNameInput, nameof(mapNameInput));
        isValid &= ValidateReference(loadPopup, nameof(loadPopup));
        isValid &= ValidateReference(loadListContainer, nameof(loadListContainer));
        isValid &= ValidateReference(loadListItemPrefab, nameof(loadListItemPrefab));
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

    private void InitializeStatusText()
    {
        if (statusText == null)
            statusText = CreateStatusText();

        statusText.text = $"배치 도구 | {selectedObjectLabel} 선택됨";
    }

    private void InitializeValidationText()
    {
        validationText = CreateValidationText();
        ShowCurrentValidation();
    }

    private void InitializeOverwritePopup()
    {
        overwritePopup = CreateOverwritePopup();
        overwritePopup.SetActive(false);
    }

    private void InitializeLoadPopupBlocker()
    {
        loadPopupBlocker = CreateUIObject("LoadPopupBlocker", loadPopup.transform.parent);
        RectTransform blockerRect = loadPopupBlocker.GetComponent<RectTransform>();
        StretchToParent(blockerRect);
        Image blockerImage = loadPopupBlocker.AddComponent<Image>();
        blockerImage.color = Color.clear;
        Button blockerButton = loadPopupBlocker.AddComponent<Button>();
        blockerButton.targetGraphic = blockerImage;
        blockerButton.transition = Selectable.Transition.None;
        blockerButton.onClick.AddListener(() => SetLoadPopupVisible(false));
        loadPopupBlocker.transform.SetSiblingIndex(loadPopup.transform.GetSiblingIndex());
    }

    private void SetLoadPopupVisible(bool isVisible)
    {
        loadPopupBlocker?.SetActive(isVisible);
        loadPopup.SetActive(isVisible);
    }

    private TMP_Text CreateStatusText()
    {
        TMP_Text text = CreateLayoutLabel(transform.parent, string.Empty, Vector2.zero, new Vector2(450f, 66f), new Vector2(620f, 26f), 19f);
        text.name = "StatusText";
        text.alignment = TextAlignmentOptions.MidlineLeft;
        return text;
    }

    private TMP_Text CreateValidationText()
    {
        TMP_Text text = CreateLayoutLabel(transform.parent, string.Empty, Vector2.zero, new Vector2(450f, 34f), new Vector2(620f, 36f), 17f);
        text.name = "ValidationText";
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.textWrappingMode = TextWrappingModes.Normal;
        return text;
    }

    private GameObject CreateOverwritePopup()
    {
        GameObject blocker = CreateUIObject("OverwritePopup", transform.parent);
        RectTransform blockerRect = blocker.GetComponent<RectTransform>();
        StretchToParent(blockerRect);
        Image blockerImage = blocker.AddComponent<Image>();
        blockerImage.color = new Color(0f, 0f, 0f, 0.65f);

        Image dialog = CreatePanel("Dialog", blocker.transform, null);
        SetRect(dialog.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(620f, 280f));

        overwriteMessage = CreateLayoutLabel(dialog.transform, string.Empty, new Vector2(0.5f, 0.5f), new Vector2(0f, 40f), new Vector2(540f, 110f), 26f);
        overwriteMessage.name = "Message";
        CreatePopupButton(primaryButtonPrefab, dialog.transform, "확인", new Vector2(-120f, -80f), ConfirmPendingAction);
        CreatePopupButton(buttonPrefab, dialog.transform, "취소", new Vector2(120f, -80f), CancelConfirmation);
        return blocker;
    }

    private void CreatePopupButton(
        PixelButton prefab,
        Transform parent,
        string label,
        Vector2 position,
        UnityEngine.Events.UnityAction onClick)
    {
        PixelButton button = Instantiate(prefab, parent);
        button.name = $"{label}Button";
        SetRect((RectTransform)button.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), position, new Vector2(200f, ToolButtonHeight));
        button.Text = label;
        button.Button.onClick.AddListener(onClick);
    }

    private GameObject CreateUIObject(string objectName, Transform parent)
    {
        GameObject uiObject = new(objectName, typeof(RectTransform), typeof(CanvasRenderer));
        uiObject.layer = gameObject.layer;
        uiObject.GetComponent<RectTransform>().SetParent(parent, false);
        return uiObject;
    }

    private void StretchToParent(RectTransform rectTransform)
    {
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
    }

    private void ShowOverwritePopup(string mapName)
    {
        pendingConfirmation = ConfirmationAction.Overwrite;
        overwriteMessage.text = $"'{mapName}' 맵이 이미 존재합니다.\n덮어쓰시겠습니까?";
        overwritePopup.SetActive(true);
    }

    private void ShowDeletePopup(string mapName)
    {
        pendingDeleteMapName = mapName;
        pendingConfirmation = ConfirmationAction.Delete;
        overwriteMessage.text = $"'{mapName}' 맵을 삭제하시겠습니까?\n이 작업은 되돌릴 수 없습니다.";
        overwritePopup.SetActive(true);
    }

    private void ConfirmPendingAction()
    {
        overwritePopup.SetActive(false);
        if (pendingConfirmation == ConfirmationAction.Overwrite)
            SaveCurrentMap(true);
        else if (pendingConfirmation == ConfirmationAction.Delete)
            DeletePendingMap();

        ClearPendingConfirmation();
    }

    private void CancelConfirmation()
    {
        overwritePopup.SetActive(false);
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
