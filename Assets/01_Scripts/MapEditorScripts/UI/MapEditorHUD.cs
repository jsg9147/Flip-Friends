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

    private ObjectTransformEditor transformEditor;
    private TMP_Text deleteModeLabel;
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

    private void ConfigureEditorLayout()
    {
        RectTransform canvasRect = transform.parent as RectTransform;
        if (canvasRect == null) return;

        HorizontalLayoutGroup headerLayout = GetComponent<HorizontalLayoutGroup>();
        if (headerLayout != null) headerLayout.enabled = false;
        Image headerImage = GetComponent<Image>();
        if (headerImage != null)
        {
            headerImage.sprite = null;
            headerImage.color = Color.white;
            headerImage.raycastTarget = false;
        }
        SetRect(transform as RectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -48f), new Vector2(0f, 96f));

        SetChildRect("MapNameInput", canvasRect, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(420f, -48f), new Vector2(250f, 54f));
        StyleSecondaryButton(transform.Find("MapNameInput"));
        SetChildRect("NewMapButton", canvasRect, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
        SetCanvasRect("NewMapButton", canvasRect, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(760f, -48f), new Vector2(124f, 54f));
        SetCanvasRect("LoadButton", canvasRect, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(900f, -48f), new Vector2(142f, 54f));
        SetCanvasRect("SaveButton", canvasRect, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(1058f, -48f), new Vector2(122f, 54f));
        SetCanvasRect("ReturnButton", canvasRect, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(1196f, -48f), new Vector2(132f, 54f));
        SetCanvasRect("TestPlayButton", canvasRect, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(410f, -132f), new Vector2(250f, 58f));

        SetPanelRect("PalettePanel", canvasRect, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(132f, 0f), new Vector2(248f, -220f));
        Transform palettePanel = canvasRect.Find("PalettePanel");
        StyleWhitePanel(palettePanel);
        Transform paletteContainer = palettePanel?.Find("ButtonContainer");
        if (paletteContainer is RectTransform containerRect)
        {
            containerRect.anchorMin = Vector2.zero;
            containerRect.anchorMax = Vector2.one;
            containerRect.offsetMin = new Vector2(10f, 14f);
            containerRect.offsetMax = new Vector2(-10f, -72f);
        }
        TMP_Text managementTitle = CreateLayoutLabel(canvasRect, "맵 관리", new Vector2(92f, -48f), new Vector2(150f, 42f), 25f);
        SetRect(managementTitle.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(92f, -48f), new Vector2(150f, 42f));
        TMP_Text mapNameLabel = CreateLayoutLabel(canvasRect, "맵 이름", new Vector2(215f, -48f), new Vector2(70f, 42f), 18f);
        SetRect(mapNameLabel.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(215f, -48f), new Vector2(70f, 42f));
        TMP_Text paletteTitle = CreateLayoutLabel(palettePanel, "오브젝트", new Vector2(74f, -27f), new Vector2(150f, 42f), 23f);
        SetRect(paletteTitle.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(74f, -27f), new Vector2(150f, 42f));
        paletteTitle.alignment = TextAlignmentOptions.MidlineLeft;
        Transform paletteIcon = palettePanel?.Find("tile_icon");
        if (paletteIcon is RectTransform iconRect)
            SetRect(iconRect, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(38f, -27f), new Vector2(28f, 28f));

        GameObject toolRail = CreateUIObject("ToolRail", canvasRect);
        RectTransform railRect = toolRail.GetComponent<RectTransform>();
        SetRect(railRect, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-112f, 0f), new Vector2(220f, -220f));
        Image railImage = toolRail.AddComponent<Image>();
        railImage.color = Color.white;
        railImage.raycastTarget = false;
        AddThinOutline(toolRail, new Color(0.35f, 0.76f, 0.86f, 1f), 1f);

        MoveAndPlaceTool("DeleteButton", toolRail.transform, "지우개", new Vector2(0f, -170f));
        MoveAndPlaceTool("RotateButton", toolRail.transform, "회전", new Vector2(0f, -245f));
        MoveAndPlaceTool("FlipButton", toolRail.transform, "반전", new Vector2(0f, -320f));
        CreatePlaceButton(toolRail.transform);

        CreateLayoutLabel(toolRail.transform, "상자 선택됨", new Vector2(0f, -390f), new Vector2(190f, 36f), 18f, out selectedObjectText);
        selectedObjectText.text = $"{selectedObjectLabel} 선택됨";
        TMP_Text hint = CreateLayoutLabel(canvasRect, "도착 지점을 배치하면 테스트할 수 있어요", new Vector2(930f, 48f), new Vector2(630f, 28f), 17f);
        SetRect(hint.rectTransform, Vector2.zero, Vector2.zero, new Vector2(930f, 48f), new Vector2(630f, 28f));
        hint.alignment = TextAlignmentOptions.MidlineLeft;
        testHintText = hint;
        CreateStatusRule(canvasRect);
        CreateRule(canvasRect, new Vector2(0f, -96f));
        ConfigureEditorCamera();
    }

    private void CreateStatusRule(Transform parent)
    {
        CreateRule(parent, new Vector2(0f, 92f));
    }

    private void CreateRule(Transform parent, Vector2 position)
    {
        GameObject rule = CreateUIObject("StatusRule", parent);
        RectTransform rect = rule.GetComponent<RectTransform>();
        SetRect(rect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        bool isHeaderRule = position.y < 0f;
        rect.anchorMin = new Vector2(0f, isHeaderRule ? 1f : 0f);
        rect.anchorMax = new Vector2(1f, isHeaderRule ? 1f : 0f);
        rect.pivot = new Vector2(0.5f, isHeaderRule ? 1f : 0f);
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(-32f, 3f);
        Image image = rule.AddComponent<Image>();
        image.color = new Color(0.16f, 0.68f, 0.73f, 1f);
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
        editorCamera.backgroundColor = Color.white;
    }

    private void StyleWhitePanel(Transform panel)
    {
        if (panel == null) return;
        Image image = panel.GetComponent<Image>();
        if (image != null)
        {
            image.sprite = null;
            image.color = Color.white;
        }
        AddThinOutline(panel.gameObject, new Color(0.35f, 0.76f, 0.86f, 1f), 1f);
    }

    private void ObjectTransformEditorMode(bool isDeleteMode)
    {
        ObjectTransformEditor editor = GetTransformEditor();
        if (editor == null) return;
        if (editor.IsDeleteMode != isDeleteMode) editor.ToggleDeleteMode();
    }

    private void CreatePlaceButton(Transform parent)
    {
        GameObject placeButton = CreateUIObject("PlaceButton", parent);
        SetRect(placeButton.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -95f), new Vector2(178f, 62f));
        Image image = placeButton.AddComponent<Image>();
        image.color = new Color(0.68f, 0.94f, 0.31f, 1f);
        AddThinOutline(placeButton, new Color(0f, 0.45f, 0.52f, 1f), 1f);
        Button button = placeButton.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.targetGraphic = image;
        button.onClick.AddListener(() => ObjectTransformEditorMode(false));
        TMP_Text label = CreateLayoutLabel(placeButton.transform, "배치", Vector2.zero, new Vector2(165f, 52f), 20f);
        label.rectTransform.anchorMin = Vector2.zero;
        label.rectTransform.anchorMax = Vector2.one;
        label.rectTransform.offsetMin = new Vector2(8f, 4f);
        label.rectTransform.offsetMax = new Vector2(-8f, -4f);
        GameObject dot = CreateUIObject("PixelDot", placeButton.transform);
        SetRect(dot.GetComponent<RectTransform>(), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(18f, 0f), new Vector2(8f, 8f));
        Image dotImage = dot.AddComponent<Image>();
        dotImage.color = new Color(0f, 0.247f, 0.282f, 1f);
        dotImage.raycastTarget = false;
        label.rectTransform.offsetMin = new Vector2(34f, 4f);
    }

    private void MoveAndPlaceTool(string childName, Transform parent, string label, Vector2 position, string newName = null)
    {
        Transform child = transform.Find(childName);
        if (child == null) return;
        child.SetParent(parent, false);
        if (!string.IsNullOrEmpty(newName)) child.name = newName;
        SetRect(child as RectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), position, new Vector2(178f, 62f));
        StyleSecondaryButton(child);
        SetButtonLabel(child, label);
    }

    private void MoveAndPlaceTool(string childName, Transform parent, string label, Vector2 anchor, Vector2 position, Vector2 size)
    {
        Transform child = transform.Find(childName);
        if (child == null) return;
        child.SetParent(parent, false);
        SetRect(child as RectTransform, anchor, anchor, position, size);
        if (childName != "TestPlayButton") StyleSecondaryButton(child);
        SetButtonLabel(child, label);
    }

    private void SetButtonLabel(Transform button, string label)
    {
        TMP_Text text = button.GetComponentInChildren<TMP_Text>(true);
        if (text == null) return;
        text.text = label;
        RectTransform rect = text.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        bool hasIcon = button.GetComponentsInChildren<Image>(true).Length > 1;
        rect.offsetMin = new Vector2(hasIcon ? 42f : 8f, 4f);
        rect.offsetMax = new Vector2(-8f, -4f);
        text.alignment = TextAlignmentOptions.Midline;
        text.color = new Color(0f, 0.247f, 0.282f, 1f);
        text.fontSize = label == "테스트 플레이" ? 19f : 20f;
    }

    private void SetChildRect(string name, RectTransform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 position, Vector2 size)
    {
        Transform child = transform.Find(name);
        if (child != null) SetRect(child as RectTransform, anchorMin, anchorMax, position, size);
    }

    private void SetCanvasRect(string name, RectTransform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 position, Vector2 size)
    {
        Transform child = transform.Find(name);
        if (child == null) return;
        child.SetParent(parent, false);
        SetRect(child as RectTransform, anchorMin, anchorMax, position, size);
        if (name != "TestPlayButton") StyleSecondaryButton(child);
        SetButtonLabel(child, name == "TestPlayButton" ? "테스트 플레이" : name == "NewMapButton" ? "새 맵" : name == "LoadButton" ? "불러오기" : name == "SaveButton" ? "저장" : "나가기");
    }

    private void StyleSecondaryButton(Transform button)
    {
        if (button == null) return;
        Image image = button.GetComponent<Image>();
        if (image == null) return;
        image.sprite = null;
        image.color = Color.white;
        Button control = button.GetComponent<Button>();
        if (control != null) control.transition = Selectable.Transition.None;
        AddThinOutline(button.gameObject, new Color(0.35f, 0.76f, 0.86f, 1f), 1f);
    }

    private void AddThinOutline(GameObject target, Color color, float width)
    {
        Outline outline = target.GetComponent<Outline>();
        if (outline == null) outline = target.AddComponent<Outline>();
        outline.effectColor = color;
        outline.effectDistance = new Vector2(width, -width);
        outline.useGraphicAlpha = true;
    }

    private void SetPanelRect(string name, RectTransform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 position, Vector2 size)
    {
        Transform child = parent.Find(name);
        if (child != null) SetRect(child as RectTransform, anchorMin, anchorMax, position, size);
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

    private TMP_Text CreateLayoutLabel(Transform parent, string label, Vector2 position, Vector2 size, float fontSize)
    {
        return CreateLayoutLabel(parent, label, position, size, fontSize, out _);
    }

    private TMP_Text CreateLayoutLabel(Transform parent, string label, Vector2 position, Vector2 size, float fontSize, out TMP_Text text)
    {
        GameObject labelObject = CreateUIObject($"Label_{label}", parent);
        RectTransform rect = labelObject.GetComponent<RectTransform>();
        SetRect(rect, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), position, size);
        text = labelObject.AddComponent<TextMeshProUGUI>();
        text.font = mapNameInput.textComponent.font;
        text.fontSize = fontSize;
        text.alignment = TextAlignmentOptions.Center;
        text.color = new Color(0f, 0.247f, 0.282f, 1f);
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
        labelRect.offsetMax = new Vector2(-88f, labelRect.offsetMax.y);

        GameObject deleteObject = CreateUIObject("DeleteButton", item.transform);
        RectTransform deleteRect = deleteObject.GetComponent<RectTransform>();
        deleteRect.anchorMin = new Vector2(1f, 0.5f);
        deleteRect.anchorMax = new Vector2(1f, 0.5f);
        deleteRect.anchoredPosition = new Vector2(-42f, 0f);
        deleteRect.sizeDelta = new Vector2(72f, 32f);

        Image image = deleteObject.AddComponent<Image>();
        image.color = new Color(0.7f, 0.18f, 0.18f, 1f);
        Button deleteButton = deleteObject.AddComponent<Button>();
        deleteButton.targetGraphic = image;
        deleteButton.onClick.AddListener(() => ShowDeletePopup(mapName));

        TMP_Text deleteLabel = CreateButtonLabel(deleteObject.transform);
        deleteLabel.text = "삭제";
        deleteLabel.fontSize = 18f;
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
        Transform deleteButton = GameObject.Find("DeleteButton")?.transform;
        deleteModeLabel = deleteButton?.GetComponentInChildren<TMP_Text>(true);
        transformEditor = GetTransformEditor();
        if (deleteModeLabel == null)
            Debug.LogError("삭제 모드 상태를 표시할 DeleteButton의 TMP_Text가 없습니다.", this);
        if (transformEditor == null) return;

        transformEditor.DeleteModeChanged += OnDeleteModeChanged;
        OnDeleteModeChanged(transformEditor.IsDeleteMode);
    }

    private void OnDeleteModeChanged(bool isEnabled)
    {
        if (deleteModeLabel != null)
            deleteModeLabel.text = "지우개";

        if (statusText != null)
            statusText.text = isEnabled ? "지우개 도구 선택됨" : $"배치 도구 | {selectedObjectLabel} 선택됨";

        Transform button = GameObject.Find("DeleteButton")?.transform;
        Image image = button != null ? button.GetComponent<Image>() : null;
        if (image != null)
            image.color = isEnabled ? new Color(0.68f, 0.94f, 0.31f, 1f) : Color.white;

        Transform placeButton = GameObject.Find("PlaceButton")?.transform;
        Image placeImage = placeButton != null ? placeButton.GetComponent<Image>() : null;
        if (placeImage != null)
            placeImage.color = isEnabled ? Color.white : new Color(0.68f, 0.94f, 0.31f, 1f);
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
        GameObject statusObject = new("StatusText", typeof(RectTransform), typeof(CanvasRenderer));
        statusObject.layer = gameObject.layer;
        RectTransform rectTransform = statusObject.GetComponent<RectTransform>();
        rectTransform.SetParent(transform.parent, false);
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.zero;
        rectTransform.pivot = new Vector2(0f, 0.5f);
        rectTransform.anchoredPosition = new Vector2(300f, 63f);
        rectTransform.sizeDelta = new Vector2(610f, 26f);

        TextMeshProUGUI text = statusObject.AddComponent<TextMeshProUGUI>();
        text.font = mapNameInput.textComponent.font;
        text.fontSize = 19f;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.raycastTarget = false;
        text.color = new Color(0f, 0.247f, 0.282f, 1f);
        return text;
    }

    private TMP_Text CreateValidationText()
    {
        GameObject validationObject = new("ValidationText", typeof(RectTransform), typeof(CanvasRenderer));
        validationObject.layer = gameObject.layer;
        RectTransform rectTransform = validationObject.GetComponent<RectTransform>();
        rectTransform.SetParent(transform.parent, false);
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.zero;
        rectTransform.pivot = new Vector2(0f, 0.5f);
        rectTransform.anchoredPosition = new Vector2(300f, 29f);
        rectTransform.sizeDelta = new Vector2(610f, 42f);

        TextMeshProUGUI text = validationObject.AddComponent<TextMeshProUGUI>();
        text.font = mapNameInput.textComponent.font;
        text.fontSize = 17f;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.color = new Color(0f, 0.247f, 0.282f, 1f);
        text.raycastTarget = false;
        return text;
    }

    private GameObject CreateOverwritePopup()
    {
        GameObject blocker = CreateUIObject("OverwritePopup", transform.parent);
        RectTransform blockerRect = blocker.GetComponent<RectTransform>();
        StretchToParent(blockerRect);
        Image blockerImage = blocker.AddComponent<Image>();
        blockerImage.color = new Color(0f, 0f, 0f, 0.65f);

        GameObject dialog = CreateUIObject("Dialog", blocker.transform);
        RectTransform dialogRect = dialog.GetComponent<RectTransform>();
        dialogRect.sizeDelta = new Vector2(560f, 220f);
        Image dialogImage = dialog.AddComponent<Image>();
        dialogImage.color = new Color(0.08f, 0.08f, 0.12f, 1f);

        overwriteMessage = CreatePopupMessage(dialog.transform);
        CreatePopupButton(dialog.transform, "확인", new Vector2(-110f, -70f), ConfirmPendingAction);
        CreatePopupButton(dialog.transform, "취소", new Vector2(110f, -70f), CancelConfirmation);
        return blocker;
    }

    private TMP_Text CreatePopupMessage(Transform parent)
    {
        GameObject messageObject = CreateUIObject("Message", parent);
        RectTransform rectTransform = messageObject.GetComponent<RectTransform>();
        rectTransform.anchoredPosition = new Vector2(0f, 35f);
        rectTransform.sizeDelta = new Vector2(500f, 100f);

        TextMeshProUGUI text = messageObject.AddComponent<TextMeshProUGUI>();
        text.font = mapNameInput.textComponent.font;
        text.fontSize = 28f;
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
        return text;
    }

    private void CreatePopupButton(
        Transform parent,
        string label,
        Vector2 position,
        UnityEngine.Events.UnityAction onClick)
    {
        GameObject buttonObject = CreateUIObject($"{label}Button", parent);
        RectTransform rectTransform = buttonObject.GetComponent<RectTransform>();
        rectTransform.anchoredPosition = position;
        rectTransform.sizeDelta = new Vector2(180f, 52f);
        Image image = buttonObject.AddComponent<Image>();
        image.color = new Color(0.2f, 0.45f, 0.75f, 1f);
        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(onClick);

        TMP_Text text = CreateButtonLabel(buttonObject.transform);
        text.text = label;
    }

    private TMP_Text CreateButtonLabel(Transform parent)
    {
        GameObject labelObject = CreateUIObject("Label", parent);
        StretchToParent(labelObject.GetComponent<RectTransform>());
        TextMeshProUGUI text = labelObject.AddComponent<TextMeshProUGUI>();
        text.font = mapNameInput.textComponent.font;
        text.fontSize = 24f;
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
        return text;
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
