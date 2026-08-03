using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class HostRoomScreen : UIScreen
{
    [Header("Navigation")]
    [SerializeField] private ScreenNavigator navigator;

    [Header("UI References")]
    [SerializeField] private Button roomTypeButton;
    [SerializeField] private Button maxPlayerCountButton;
    [SerializeField] private Button createButton;
    [SerializeField] private Button cancelButton;
    [SerializeField] private TMP_Text selectedMapText;
    [SerializeField] private TMP_Text validationMessageText;

    [Header("Host Settings")]
    [SerializeField] private RoomType roomType = RoomType.Public;
    [SerializeField] private int minPlayerCount = 2;
    [SerializeField] private int maxPlayerLimit = 4;
    [SerializeField] private int maxPlayerCount = 4;
    [SerializeField] private float horizontalInputCooldown = 0.2f;

    private float lastHorizontalInputTime;
    private bool canReadHorizontalInput = true;
    private PendingRoomMapSelection selectedMap;
    private string selectionValidationMessage;

    public RoomMapPolicy SelectedMapPolicy => selectedMap?.Policy ?? RoomMapPolicy.OfficialOnly;
    public LobbyMapKind SelectedMapKind => selectedMap?.Kind ?? LobbyMapKind.None;

    protected override void Awake()
    {
        base.Awake();

        if (navigator == null)
        {
            navigator = FindFirstObjectByType<ScreenNavigator>(FindObjectsInactive.Include);
        }

        if (roomTypeButton != null)
        {
            roomTypeButton.onClick.AddListener(ToggleRoomType);
        }

        if (maxPlayerCountButton != null)
        {
            maxPlayerCountButton.onClick.AddListener(CycleMaxPlayerCount);
        }

        if (createButton != null)
        {
            createButton.onClick.AddListener(CreateRoom);
        }

        if (cancelButton != null)
        {
            cancelButton.onClick.AddListener(HandleBack);
        }

        maxPlayerCount = Mathf.Clamp(maxPlayerCount, minPlayerCount, maxPlayerLimit);
        RefreshLabels();
    }

    private void OnEnable()
    {
        MapCompletionProgress.Changed += HandleCompletionChanged;
        if (InputManager.instance != null)
        {
            InputManager.instance.OnCancelEvent += HandleCancel;
        }
    }

    private void OnDisable()
    {
        MapCompletionProgress.Changed -= HandleCompletionChanged;
        if (InputManager.instance != null)
        {
            InputManager.instance.OnCancelEvent -= HandleCancel;
        }
    }

    private void Update()
    {
        if (!IsVisible || InputManager.instance == null)
        {
            return;
        }

        HandleHorizontalInput();
    }

    protected override void OnShow()
    {
        RefreshLabels();
    }

    private void HandleHorizontalInput()
    {
        float horizontal = InputManager.instance.dir.x;

        if (Mathf.Approximately(horizontal, 0f))
        {
            canReadHorizontalInput = true;
            return;
        }

        if (!canReadHorizontalInput || Time.unscaledTime - lastHorizontalInputTime < horizontalInputCooldown)
        {
            return;
        }

        GameObject selectedObject = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;

        if (selectedObject == roomTypeButton?.gameObject)
        {
            ToggleRoomType();
        }
        else if (selectedObject == maxPlayerCountButton?.gameObject)
        {
            ChangeMaxPlayerCount(horizontal > 0f ? 1 : -1);
        }

        lastHorizontalInputTime = Time.unscaledTime;
        canReadHorizontalInput = false;
    }

    public void ToggleRoomType()
    {
        roomType = roomType == RoomType.Public ? RoomType.Private : RoomType.Public;
        RefreshLabels();
    }

    public void CycleMaxPlayerCount()
    {
        int nextPlayerCount = maxPlayerCount + 1;

        if (nextPlayerCount > maxPlayerLimit)
        {
            nextPlayerCount = minPlayerCount;
        }

        maxPlayerCount = nextPlayerCount;
        RefreshLabels();
    }

    public void ChangeMaxPlayerCount(int delta)
    {
        maxPlayerCount = Mathf.Clamp(maxPlayerCount + delta, minPlayerCount, maxPlayerLimit);
        RefreshLabels();
    }

    public void CreateRoom()
    {
        SteamRoomManager roomManager = SteamRoomManager.Instance;

        if (roomManager == null)
        {
            Debug.LogWarning("HostRoomScreen could not find SteamRoomManager.");
            return;
        }

        if (!TryValidateRoomCreation(out string validationMessage))
        {
            ShowValidationMessage(validationMessage, true);
            return;
        }
        if (!roomManager.TrySetPendingMapSelection(selectedMap)) return;

        if (createButton != null)
        {
            createButton.interactable = false;
        }

        roomManager.HostLobby(roomType, maxPlayerCount);
    }

    public void SelectOfficialMap(OfficialMapEntry entry)
    {
        if (!PendingRoomMapSelection.TryCreateOfficial(entry, out selectedMap))
        {
            selectedMap = null;
            selectionValidationMessage = "공식맵 선택 데이터가 올바르지 않습니다.";
            RefreshCreateState();
            return;
        }

        selectionValidationMessage = null;
        RefreshSelection();
    }

    public void SelectCustomMap(SavedMapListEntry entry)
    {
        if (!PendingRoomMapSelection.TryCreateCustom(entry, out selectedMap))
        {
            selectedMap = null;
            selectionValidationMessage = "커스텀맵 선택 데이터가 올바르지 않습니다.";
            RefreshCreateState();
            return;
        }

        selectionValidationMessage = null;
        RefreshSelection();
    }

    public void ClearMapSelection()
    {
        selectedMap = null;
        selectionValidationMessage = null;
        if (selectedMapText != null)
            selectedMapText.text = "선택된 맵 없음";
        RefreshCreateState();
    }

    public void HandleBack()
    {
        if (createButton != null)
        {
            createButton.interactable = true;
        }

        if (navigator != null && navigator.Back())
        {
            return;
        }

        if (MainUIManager.instance != null)
        {
            MainUIManager.instance.GameModeUIOpen();
        }
    }

    private void HandleCancel()
    {
        if (IsVisible)
        {
            HandleBack();
        }
    }

    private void RefreshLabels()
    {
        SetButtonText(roomTypeButton, roomType.ToString());
        SetButtonText(maxPlayerCountButton, maxPlayerCount.ToString());
        RefreshCreateState();
    }

    private bool TryValidateRoomCreation(out string message)
    {
        RoomCreationEligibility eligibility = EvaluateRoomCreation();
        message = GetValidationMessage(eligibility);
        return eligibility.CanCreate;
    }

    private void RefreshSelection()
    {
        RefreshSelectedMapText();
        RefreshCreateState();
    }

    private void RefreshSelectedMapText()
    {
        if (selectedMapText == null || selectedMap == null) return;

        CurrentRevisionVerificationState verification =
            MapCompletionProgress.GetVerificationState(selectedMap.CompletionTarget);
        string completionLabel = verification == CurrentRevisionVerificationState.Unavailable
            ? "완료 상태 확인 불가"
            : MapCompletionDisplay.GetLabel(
                MapCompletionProgress.GetState(selectedMap.CompletionTarget));
        selectedMapText.text =
            $"{selectedMap.DisplayName}\n" +
            $"제작자: {selectedMap.AuthorName} / v{selectedMap.Version}\n" +
            $"최소 인원: {selectedMap.MinimumPlayersToClear}명 / {completionLabel}";
    }

    private void RefreshCreateState()
    {
        RoomCreationEligibility eligibility = EvaluateRoomCreation();
        if (createButton != null)
            createButton.interactable = eligibility.CanCreate;
        ShowValidationMessage(
            GetValidationMessage(eligibility),
            !eligibility.CanCreate);
    }

    private RoomCreationEligibility EvaluateRoomCreation()
    {
        if (selectedMap == null ||
            !selectedMap.TryCreateLobbyMetadata(out LobbyMapMetadata metadata))
        {
            return RoomCreationEligibility.Blocked(
                RoomCreationBlockReason.InvalidMapSelection);
        }

        return MapCompletionProgress.EvaluateRoomCreation(
            roomType == RoomType.Public,
            maxPlayerCount,
            metadata,
            selectedMap.CompletionTarget);
    }

    private string GetValidationMessage(RoomCreationEligibility eligibility)
    {
        return eligibility.BlockReason switch
        {
            RoomCreationBlockReason.InvalidMapSelection =>
                selectionValidationMessage ??
                "방을 만들기 전에 플레이할 맵을 선택하세요.",
            RoomCreationBlockReason.InsufficientCapacity =>
                $"이 맵은 최소 {selectedMap?.MinimumPlayersToClear ?? 1}명이 필요합니다.",
            RoomCreationBlockReason.CompletionStatusUnavailable =>
                "완료 기록을 확인할 수 없어 이 맵으로 공개방을 만들 수 없습니다.",
            RoomCreationBlockReason.CurrentRevisionNotCompleted =>
                "현재 리비전을 먼저 비공개 방에서 완료해야 공개방을 만들 수 있습니다.",
            _ => roomType == RoomType.Public
                ? "현재 리비전 완료 기록이 확인되어 공개방을 만들 수 있습니다."
                : "비공개 방은 완료 기록과 관계없이 만들 수 있습니다."
        };
    }

    private void HandleCompletionChanged()
    {
        RefreshSelectedMapText();
        RefreshCreateState();
    }

    private void ShowValidationMessage(string message, bool isError)
    {
        if (validationMessageText == null) return;

        validationMessageText.text = message ?? string.Empty;
        validationMessageText.color = isError
            ? new Color(1f, 0.35f, 0.35f)
            : Color.white;
    }

    private static void SetButtonText(Button button, string value)
    {
        if (button == null)
        {
            return;
        }

        TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
        {
            label.text = value;
        }
    }
}
