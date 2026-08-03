using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class MapListUI : MonoBehaviour
{
    [SerializeField] private GameObject panel;
    [SerializeField] private Transform listContainer;
    [SerializeField] private GameObject listItemPrefab;
    [SerializeField] private OfficialMapCatalog officialMapCatalog;
    [SerializeField] private MapEditorPalette mapEditorPalette;
    [SerializeField] private MapSelectionManager mapSelectionManager;
    [SerializeField] private HostRoomScreen hostRoomScreen;
    [SerializeField] private TMP_Text messageText;
    [SerializeField] private Button officialMapsButton;
    [SerializeField] private Button customMapsButton;
    [SerializeField] private Button startButton;

    private readonly List<GameObject> createdItems = new();
    private SavedMapCatalog customMapCatalog;
    private MapListView currentView = MapListView.All;
    private bool IsHostCreationContext => hostRoomScreen != null;
    private bool CanChangeSelection => IsHostCreationContext ||
        mapSelectionManager != null && mapSelectionManager.IsLocalHost &&
        !mapSelectionManager.IsSelectionLocked;

    private void Start()
    {
        if (!ValidateReferences()) return;

        customMapCatalog = new SavedMapCatalog(mapEditorPalette);
        BindButtons();
        ShowAllMaps();
        RefreshState();
    }

    private void OnEnable()
    {
        if (mapSelectionManager != null)
            mapSelectionManager.SelectionChanged += RefreshState;
        MapCompletionProgress.Changed += RefreshVisibleList;
    }

    private void OnDisable()
    {
        if (mapSelectionManager != null)
            mapSelectionManager.SelectionChanged -= RefreshState;
        MapCompletionProgress.Changed -= RefreshVisibleList;
    }

    public void ShowOfficialMaps()
    {
        currentView = MapListView.Official;
        PopulateOfficialMaps();
    }

    public void ShowCustomMaps()
    {
        currentView = MapListView.Custom;
        PopulateCustomMaps();
    }

    public void ShowAllMaps()
    {
        currentView = MapListView.All;
        ClearList();
        AppendOfficialMaps();
        AppendCustomMaps();
        SelectFirstItem();
    }

    public void StartSelectedMap()
    {
        mapSelectionManager?.StartSelectedMap();
    }

    public void ShowSelectionMessage(string message, bool isError)
    {
        if (messageText == null) return;

        messageText.text = message ?? "맵 선택 상태를 확인할 수 없습니다.";
        messageText.color = isError
            ? new Color(1f, 0.35f, 0.35f)
            : Color.white;
    }

    private void PopulateOfficialMaps()
    {
        ClearList();
        AppendOfficialMaps();
        SelectFirstItem();
    }

    private void AppendOfficialMaps()
    {
        if (officialMapCatalog == null)
        {
            ShowSelectionMessage("공식맵 카탈로그가 연결되지 않았습니다.", true);
            return;
        }

        foreach (OfficialMapEntry entry in officialMapCatalog.Entries)
        {
            if (entry != null)
                CreateOfficialItem(entry);
        }
    }

    private void PopulateCustomMaps()
    {
        ClearList();
        AppendCustomMaps();
        SelectFirstItem();
    }

    private void AppendCustomMaps()
    {
        IReadOnlyList<SavedMapListEntry> entries = customMapCatalog.GetEntries();
        if (entries.Count == 0)
        {
            ShowSelectionMessage("로컬에 저장된 커스텀맵이 없습니다.", false);
            return;
        }

        foreach (SavedMapListEntry entry in entries)
            CreateCustomItem(entry);
    }

    private void CreateOfficialItem(OfficialMapEntry entry)
    {
        string completion = GetOfficialCompletionLabel(entry);
        Button button = CreateItem(
            $"{entry.DisplayName}\n제작자: {entry.AuthorName} / v{entry.Version}\n" +
            $"최소 인원: {entry.MinimumPlayersToClear}명 · {completion}");
        if (button == null) return;

        button.interactable = CanChangeSelection;
        button.onClick.AddListener(() => SelectOfficial(entry));
    }

    private void CreateCustomItem(SavedMapListEntry entry)
    {
        string state = entry.CanSelect ? "선택 가능" : $"선택 불가 · {entry.Message}";
        string completion = GetCustomCompletionLabel(entry);
        Button button = CreateItem(
            $"{entry.MapName}\n제작자: {entry.AuthorName} / v{entry.Version}\n" +
            $"최소 인원: {entry.MinimumPlayersToClear}명 · {state} · {completion}");
        if (button == null) return;

        button.interactable = CanChangeSelection && entry.CanSelect;
        button.onClick.AddListener(() => SelectCustom(entry));
    }

    private Button CreateItem(string labelText)
    {
        GameObject item = Instantiate(listItemPrefab, listContainer);
        Button button = item.GetComponent<Button>();
        TMP_Text label = item.GetComponentInChildren<TMP_Text>(true);
        if (button == null || label == null)
        {
            Debug.LogError($"맵 목록 아이템 프리팹 구성이 올바르지 않습니다: {item.name}");
            Destroy(item);
            return null;
        }

        createdItems.Add(item);
        label.text = labelText;
        button.onClick.RemoveAllListeners();
        return button;
    }

    private void SelectOfficial(OfficialMapEntry entry)
    {
        if (IsHostCreationContext)
            hostRoomScreen.SelectOfficialMap(entry);
        else
            mapSelectionManager.SelectOfficialMap(entry.MapId);
        RefreshState();
    }

    private void SelectCustom(SavedMapListEntry entry)
    {
        SavedMapListEntry refreshed = customMapCatalog.Refresh(entry);
        if (refreshed == null || !refreshed.CanSelect)
        {
            ShowSelectionMessage(
                refreshed?.Message ?? "선택한 MapId의 맵을 다시 찾을 수 없습니다.",
                true);
            ShowCustomMaps();
            return;
        }

        if (IsHostCreationContext)
            hostRoomScreen.SelectCustomMap(refreshed);
        else
            mapSelectionManager.SelectCustomMap(refreshed);
        RefreshState();
    }

    private void RefreshState()
    {
        if (startButton != null)
        {
            startButton.gameObject.SetActive(!IsHostCreationContext);
            startButton.interactable = mapSelectionManager != null &&
                                       mapSelectionManager.IsLocalHost &&
                                       mapSelectionManager.SelectedMapKind != LobbyMapKind.None;
        }
        if (!IsHostCreationContext && mapSelectionManager != null)
        {
            ShowSelectionMessage(
                $"{BuildSelectionSummary()}\n{BuildSelectionStatus()}",
                false);
        }
        else if (IsHostCreationContext)
        {
            ShowSelectionMessage("방 종류, 맵, 최대 인원을 확인한 뒤 방을 생성하세요.", false);
        }
    }

    private void RefreshVisibleList()
    {
        if (!isActiveAndEnabled || customMapCatalog == null) return;

        switch (currentView)
        {
            case MapListView.Official:
                PopulateOfficialMaps();
                break;
            case MapListView.Custom:
                PopulateCustomMaps();
                break;
            default:
                ClearList();
                AppendOfficialMaps();
                AppendCustomMaps();
                SelectFirstItem();
                break;
        }
        RefreshState();
    }

    private static string GetOfficialCompletionLabel(OfficialMapEntry entry)
    {
        if (entry == null ||
            !MapCompletionTarget.TryCreateOfficial(
                entry.MapId, entry.CompletionRevision, out MapCompletionTarget target))
            return "완료 상태 확인 불가";

        return MapCompletionDisplay.GetLabel(MapCompletionProgress.GetState(target));
    }

    private static string GetCustomCompletionLabel(SavedMapListEntry entry)
    {
        if (entry == null ||
            !MapCompletionTarget.TryCreateCustom(
                entry.MapId, entry.ContentHash, out MapCompletionTarget target))
            return "완료 상태 확인 불가";

        return MapCompletionDisplay.GetLabel(MapCompletionProgress.GetState(target));
    }

    private string BuildSelectionSummary()
    {
        if (mapSelectionManager.SelectedMapKind == LobbyMapKind.None)
            return "선택된 맵 없음";

        string version = string.IsNullOrEmpty(mapSelectionManager.SelectedVersion)
            ? string.Empty
            : $" / v{mapSelectionManager.SelectedVersion}";
        return
            $"{mapSelectionManager.SelectedMapName}\n" +
            $"제작자: {mapSelectionManager.SelectedAuthorName}{version}\n" +
            $"최소 인원: {mapSelectionManager.SelectedMinimumPlayersToClear}명";
    }

    private string BuildSelectionStatus()
    {
        if (mapSelectionManager.AvailabilityState != LobbyMapAvailabilityState.Idle &&
            !string.IsNullOrEmpty(mapSelectionManager.AvailabilityMessage))
            return mapSelectionManager.AvailabilityMessage;
        if (!mapSelectionManager.IsLocalHost)
            return "방장만 맵 선택을 변경할 수 있습니다.";
        if (mapSelectionManager.SelectedMapKind == LobbyMapKind.None)
            return "공식맵 또는 커스텀맵을 선택하세요.";
        if (mapSelectionManager.SelectedMapKind == LobbyMapKind.Custom &&
            mapSelectionManager.SelectedMapHasWarnings)
        {
            return
                $"경고가 있는 맵입니다. 선택 가능 / " +
                $"{mapSelectionManager.SelectedMapByteCount} bytes";
        }

        return "게임을 시작할 수 있습니다.";
    }

    private void BindButtons()
    {
        officialMapsButton?.onClick.AddListener(ShowOfficialMaps);
        customMapsButton?.onClick.AddListener(ShowCustomMaps);
        startButton?.onClick.AddListener(StartSelectedMap);
    }

    private void SelectFirstItem()
    {
        if (createdItems.Count == 0 || EventSystem.current == null) return;

        EventSystem.current.SetSelectedGameObject(createdItems[0]);
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
        bool hasContext = hostRoomScreen != null || mapSelectionManager != null;
        bool valid = panel != null && listContainer != null && listItemPrefab != null &&
                     officialMapCatalog != null && mapEditorPalette != null && hasContext;
        if (valid) return true;

        Debug.LogError("MapListUI의 목록, 카탈로그, 팔레트 또는 사용 화면 참조가 없습니다.");
        enabled = false;
        return false;
    }

    private enum MapListView
    {
        All,
        Official,
        Custom
    }
}
