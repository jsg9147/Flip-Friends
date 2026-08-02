using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MapListUI : MonoBehaviour
{
    [SerializeField] private GameObject panel;
    [SerializeField] private Transform listContainer;
    [SerializeField] private GameObject listItemPrefab;
    [SerializeField] private MapSelectionManager mapSelectionManager;
    [SerializeField] private TMP_Text messageText;
    [SerializeField] private Button openButton;
    [SerializeField] private Button startButton;
    [SerializeField] private Button clearSelectionButton;

    private readonly List<GameObject> createdItems = new();
    private SavedMapCatalog catalog;

    private void Start()
    {
        if (!ValidateReferences()) return;

        catalog = new SavedMapCatalog(mapSelectionManager.Palette);
        CreateMissingControls();
        panel.SetActive(false);
        BindButtons();
        RefreshAuthorityState();
    }

    private void OnEnable()
    {
        if (mapSelectionManager != null)
            mapSelectionManager.SelectionChanged += RefreshAuthorityState;
    }

    private void OnDisable()
    {
        if (mapSelectionManager != null)
            mapSelectionManager.SelectionChanged -= RefreshAuthorityState;
    }

    public void TogglePanel()
    {
        if (!mapSelectionManager.IsLocalHost)
        {
            ShowSelectionMessage("방장만 커스텀 맵을 선택할 수 있습니다.", true);
            return;
        }

        bool isOpen = !panel.activeSelf;
        panel.SetActive(isOpen);
        if (isOpen)
            PopulateList();
    }

    public void StartSelectedMap()
    {
        mapSelectionManager.StartSelectedMap();
    }

    public void ClearSelection()
    {
        mapSelectionManager.ClearCustomMapSelection();
        ShowSelectionMessage("커스텀 맵 선택을 해제했습니다.", false);
    }

    public void ShowSelectionMessage(string message, bool isError)
    {
        if (messageText == null) return;

        messageText.text = message ?? "맵 선택 상태를 확인할 수 없습니다.";
        messageText.color = isError
            ? new Color(1f, 0.35f, 0.35f)
            : Color.white;
    }

    private void PopulateList()
    {
        ClearList();
        IReadOnlyList<SavedMapListEntry> entries = catalog.GetEntries();
        if (entries.Count == 0)
        {
            ShowSelectionMessage("로컬에 저장된 커스텀 맵이 없습니다.", false);
            return;
        }

        foreach (SavedMapListEntry entry in entries)
            CreateListItem(entry);

        mapSelectionManager.SelectFirstLocalMapListItem(listContainer);
    }

    private void CreateListItem(SavedMapListEntry entry)
    {
        GameObject item = Instantiate(listItemPrefab, listContainer);
        Button button = item.GetComponent<Button>();
        TMP_Text label = item.GetComponentInChildren<TMP_Text>();
        if (button == null || label == null)
        {
            Debug.LogError($"맵 목록 아이템 프리팹 구성이 올바르지 않습니다: {item.name}");
            Destroy(item);
            return;
        }

        createdItems.Add(item);
        PositionListItem(item.GetComponent<RectTransform>(), createdItems.Count - 1);
        label.text = BuildItemLabel(entry);
        button.interactable = mapSelectionManager.IsLocalHost && entry.CanSelect;
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => OnMapSelected(entry));
    }

    private void PositionListItem(RectTransform rectTransform, int index)
    {
        if (rectTransform == null) return;

        rectTransform.anchorMin = new Vector2(0.5f, 1f);
        rectTransform.anchorMax = new Vector2(0.5f, 1f);
        rectTransform.pivot = new Vector2(0.5f, 1f);
        rectTransform.anchoredPosition = new Vector2(0f, -20f - index * 125f);
        rectTransform.sizeDelta = new Vector2(760f, 110f);
    }

    private string BuildItemLabel(SavedMapListEntry entry)
    {
        string state = entry.CanSelect
            ? entry.Availability == SavedMapAvailability.PlayableWithWarnings
                ? "선택 가능 · 경고 있음"
                : "선택 가능"
            : $"선택 불가 · {entry.Message}";
        return $"{entry.MapName}\n제작자: {entry.AuthorName} / v{entry.Version}\n{state}";
    }

    private void OnMapSelected(SavedMapListEntry entry)
    {
        SavedMapListEntry refreshed = catalog.Refresh(entry);
        if (refreshed == null || !refreshed.CanSelect)
        {
            ShowSelectionMessage(
                refreshed?.Message ?? "선택한 MapId의 맵을 다시 찾을 수 없습니다.",
                true);
            PopulateList();
            return;
        }

        mapSelectionManager.SelectCustomMap(refreshed);
        ShowSelectionMessage(refreshed.Message, false);
        panel.SetActive(false);
    }

    private void RefreshAuthorityState()
    {
        bool isHost = mapSelectionManager != null && mapSelectionManager.IsLocalHost;
        if (openButton != null)
            openButton.interactable = isHost;
        if (startButton != null)
            startButton.interactable =
                isHost && mapSelectionManager.SelectedMapKind != LobbyMapKind.None;
        if (clearSelectionButton != null)
            clearSelectionButton.interactable =
                isHost && mapSelectionManager.SelectedMapKind == LobbyMapKind.Custom;
        if (!isHost && panel != null)
            panel.SetActive(false);
        if (messageText != null)
            ShowSelectionMessage(
                $"{mapSelectionManager.SelectionSummary}\n" +
                mapSelectionManager.SelectionStatus,
                false);
    }

    private void BindButtons()
    {
        openButton?.onClick.AddListener(TogglePanel);
        startButton?.onClick.AddListener(StartSelectedMap);
        clearSelectionButton?.onClick.AddListener(ClearSelection);
    }

    private void CreateMissingControls()
    {
        Transform controlParent = panel.transform.parent;
        openButton = openButton != null
            ? openButton
            : CreateControlButton(controlParent, "커스텀 맵", new Vector2(-360f, -80f));
        startButton = startButton != null
            ? startButton
            : CreateControlButton(controlParent, "선택 맵 시작", new Vector2(0f, -80f));
        clearSelectionButton = clearSelectionButton != null
            ? clearSelectionButton
            : CreateControlButton(controlParent, "커스텀 해제", new Vector2(360f, -80f));
        if (messageText == null)
            messageText = CreateMessageText(controlParent);
    }

    private Button CreateControlButton(
        Transform parent,
        string label,
        Vector2 anchoredPosition)
    {
        GameObject item = Instantiate(listItemPrefab, parent);
        item.name = label;
        RectTransform rectTransform = item.GetComponent<RectTransform>();
        rectTransform.anchorMin = new Vector2(0.5f, 0f);
        rectTransform.anchorMax = new Vector2(0.5f, 0f);
        rectTransform.anchoredPosition = anchoredPosition;
        rectTransform.sizeDelta = new Vector2(260f, 70f);
        TMP_Text text = item.GetComponentInChildren<TMP_Text>();
        if (text != null)
            text.text = label;
        Button button = item.GetComponent<Button>();
        button.onClick.RemoveAllListeners();
        return button;
    }

    private TMP_Text CreateMessageText(Transform parent)
    {
        TMP_Text template = listItemPrefab.GetComponentInChildren<TMP_Text>();
        TMP_Text text = Instantiate(template, parent);
        text.name = "Custom Map Message";
        RectTransform rectTransform = text.rectTransform;
        rectTransform.anchorMin = new Vector2(0.5f, 0f);
        rectTransform.anchorMax = new Vector2(0.5f, 0f);
        rectTransform.anchoredPosition = new Vector2(0f, 20f);
        rectTransform.sizeDelta = new Vector2(1200f, 60f);
        text.alignment = TextAlignmentOptions.Center;
        text.fontSize = 28f;
        return text;
    }

    private void ClearList()
    {
        foreach (GameObject item in createdItems)
        {
            if (item != null)
                Destroy(item);
        }

        createdItems.Clear();
    }

    private bool ValidateReferences()
    {
        if (panel != null &&
            listContainer != null &&
            mapSelectionManager != null &&
            IsValidListItemPrefab())
            return true;

        Debug.LogError("MapListUI의 패널, 목록 컨테이너, 아이템 프리팹 또는 선택 관리자 참조가 없습니다.");
        enabled = false;
        return false;
    }

    private bool IsValidListItemPrefab()
    {
        return listItemPrefab != null &&
               listItemPrefab.GetComponent<Button>() != null &&
               listItemPrefab.GetComponentInChildren<TMP_Text>(true) != null;
    }
}
