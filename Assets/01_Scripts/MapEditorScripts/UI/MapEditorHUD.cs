using System.Collections.Generic;
using System.Text;
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

    private const string DeleteModeEnabledText = "삭제 ON";
    private const string DeleteModeDisabledText = "삭제 OFF";

    private ObjectTransformEditor transformEditor;
    private TMP_Text deleteModeLabel;
    private TMP_Text validationText;
    private GameObject loadPopupBlocker;
    private GameObject overwritePopup;
    private TMP_Text overwriteMessage;
    private ConfirmationAction pendingConfirmation;
    private string pendingDeleteMapName;

    private void Start()
    {
        if (!ValidateReferences()) return;

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
        Transform deleteButton = transform.Find("DeleteButton");
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
            deleteModeLabel.text = isEnabled ? DeleteModeEnabledText : DeleteModeDisabledText;
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

        statusText.text = string.Empty;
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
        rectTransform.anchorMin = new Vector2(0.5f, 1f);
        rectTransform.anchorMax = new Vector2(0.5f, 1f);
        rectTransform.pivot = new Vector2(0.5f, 1f);
        rectTransform.anchoredPosition = new Vector2(0f, -70f);
        rectTransform.sizeDelta = new Vector2(900f, 36f);

        TextMeshProUGUI text = statusObject.AddComponent<TextMeshProUGUI>();
        text.font = mapNameInput.textComponent.font;
        text.fontSize = 24f;
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
        return text;
    }

    private TMP_Text CreateValidationText()
    {
        GameObject validationObject = new("ValidationText", typeof(RectTransform), typeof(CanvasRenderer));
        validationObject.layer = gameObject.layer;
        RectTransform rectTransform = validationObject.GetComponent<RectTransform>();
        rectTransform.SetParent(transform.parent, false);
        rectTransform.anchorMin = new Vector2(0.5f, 1f);
        rectTransform.anchorMax = new Vector2(0.5f, 1f);
        rectTransform.pivot = new Vector2(0.5f, 1f);
        rectTransform.anchoredPosition = new Vector2(0f, -110f);
        rectTransform.sizeDelta = new Vector2(1100f, 220f);

        TextMeshProUGUI text = validationObject.AddComponent<TextMeshProUGUI>();
        text.font = mapNameInput.textComponent.font;
        text.fontSize = 20f;
        text.alignment = TextAlignmentOptions.TopLeft;
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
            validationText.color = new Color(0.35f, 1f, 0.55f);
            return;
        }

        validationText.text = BuildValidationMessage(report);
        validationText.color = report.HasErrors
            ? new Color(1f, 0.35f, 0.35f)
            : new Color(1f, 0.8f, 0.25f);
    }

    private string BuildValidationMessage(MapValidationReport report)
    {
        var builder = new StringBuilder();
        builder.AppendLine(report.CanStartPlay
            ? "플레이 가능(경고 확인 권장)"
            : "저장 가능 / 테스트 플레이 불가");
        foreach (MapValidationIssue issue in report.Issues)
            builder.AppendLine($"[{GetSeverityLabel(issue.Severity)}] {issue.Message}");

        return builder.ToString().TrimEnd();
    }

    private string GetSeverityLabel(MapValidationSeverity severity)
    {
        return severity switch
        {
            MapValidationSeverity.Error => "오류",
            MapValidationSeverity.Warning => "경고",
            _ => "정보"
        };
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
        statusText.color = color;
    }

    private bool ValidateReference(Object reference, string fieldName)
    {
        if (reference != null) return true;

        Debug.LogError($"MapEditorHUD의 Inspector 참조가 누락되었습니다: {fieldName}", this);
        return false;
    }
}
