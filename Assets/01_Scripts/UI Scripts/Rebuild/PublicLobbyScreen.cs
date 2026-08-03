using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class PublicLobbyScreen : UIScreen
{
    private enum LobbyViewState
    {
        Loading,
        Ready,
        Empty,
        Error,
        Joining
    }

    [Header("Navigation")]
    [SerializeField] private ScreenNavigator navigator;

    [Header("UI References")]
    [SerializeField] private LobbyItem lobbyItemPrefab;
    [SerializeField] private Transform lobbyItemContent;
    [SerializeField] private Button refreshButton;
    [SerializeField] private Button backButton;
    [SerializeField] private GameObject listRoot;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private GameObject loadingStateRoot;
    [SerializeField] private GameObject emptyStateRoot;
    [SerializeField] private GameObject errorStateRoot;
    [SerializeField] private GameObject joiningStateRoot;

    [Header("Map Filter")]
    [SerializeField] private LobbyMapKind mapKindFilter = LobbyMapKind.None;
    [SerializeField, Range(0, 4)] private int partySizeFilter;

    private readonly List<LobbyItem> lobbyItems = new();
    private int refreshRequestVersion;
    private LobbyViewState currentState;

    protected override void Awake()
    {
        base.Awake();

        if (navigator == null)
        {
            navigator = FindFirstObjectByType<ScreenNavigator>(FindObjectsInactive.Include);
        }

        if (refreshButton != null)
        {
            refreshButton.onClick.AddListener(RequestRefresh);
        }

        if (backButton != null)
        {
            backButton.onClick.AddListener(HandleBack);
        }
    }

    private void OnEnable()
    {
        if (InputManager.instance != null)
        {
            InputManager.instance.OnCancelEvent += HandleCancel;
        }
    }

    private void OnDisable()
    {
        if (InputManager.instance != null)
        {
            InputManager.instance.OnCancelEvent -= HandleCancel;
        }
    }

    protected override void OnShow()
    {
        RequestRefresh();
    }

    protected override void OnHide()
    {
        refreshRequestVersion++;
        ClearLobbyItems();
        SetState(LobbyViewState.Loading, string.Empty);
    }

    public void RequestRefresh()
    {
        _ = RefreshAsync();
    }

    public void OnLobbyListUpdateButtonClicked()
    {
        RequestRefresh();
    }

    public async Task RefreshAsync()
    {
        int requestVersion = ++refreshRequestVersion;

        ClearLobbyItems();
        SetState(LobbyViewState.Loading, "Loading lobbies...");

        if (SteamRoomManager.Instance == null || !SteamManager.Initialized)
        {
            SetState(LobbyViewState.Error, "Steam is not ready.");
            return;
        }

        try
        {
            List<SteamLobbyInfo> lobbyInfoList = await SteamRoomManager.Instance
                .GetLobbyListAsync(new LobbyMapFilter(mapKindFilter, partySizeFilter));

            if (!IsVisible || requestVersion != refreshRequestVersion)
            {
                return;
            }

            if (lobbyInfoList == null || lobbyInfoList.Count == 0)
            {
                SetState(LobbyViewState.Empty, "No public lobbies found.");
                return;
            }

            foreach (SteamLobbyInfo info in lobbyInfoList)
            {
                LobbyItem item = Instantiate(lobbyItemPrefab, lobbyItemContent);
                item.SetLobbyInfo(info);
                item.SetJoinAction(JoinLobby);
                lobbyItems.Add(item);
            }

            SetState(LobbyViewState.Ready, string.Empty);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);

            if (!IsVisible || requestVersion != refreshRequestVersion)
            {
                return;
            }

            SetState(LobbyViewState.Error, "Failed to load lobbies.");
        }
    }

    public void SetMapKindFilter(LobbyMapKind mapKind)
    {
        mapKindFilter = mapKind;
        RequestRefresh();
    }

    public void SetPartySizeFilter(int partySize)
    {
        partySizeFilter = Mathf.Clamp(partySize, 0, 4);
        RequestRefresh();
    }

    public void JoinLobby(SteamLobbyInfo lobbyInfo)
    {
        if (lobbyInfo == null || SteamRoomManager.Instance == null)
        {
            SetState(LobbyViewState.Error, "Unable to join the selected lobby.");
            return;
        }

        SetState(LobbyViewState.Joining, $"Joining {lobbyInfo.LobbyName}...");
        SteamRoomManager.Instance.JoinLobby(lobbyInfo.LobbyID);
    }

    public void HandleBack()
    {
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

    private void ClearLobbyItems()
    {
        foreach (Transform child in lobbyItemContent)
        {
            if (child != null)
            {
                Destroy(child.gameObject);
            }
        }

        lobbyItems.Clear();
    }

    private void SetState(LobbyViewState state, string message)
    {
        currentState = state;

        if (listRoot != null)
        {
            listRoot.SetActive(state == LobbyViewState.Ready);
        }

        SetOptionalState(loadingStateRoot, state == LobbyViewState.Loading);
        SetOptionalState(emptyStateRoot, state == LobbyViewState.Empty);
        SetOptionalState(errorStateRoot, state == LobbyViewState.Error);
        SetOptionalState(joiningStateRoot, state == LobbyViewState.Joining);

        if (statusText != null)
        {
            statusText.gameObject.SetActive(!string.IsNullOrWhiteSpace(message) || state != LobbyViewState.Ready);
            statusText.text = message;
        }

        if (refreshButton != null)
        {
            refreshButton.interactable = state != LobbyViewState.Loading && state != LobbyViewState.Joining;
        }
    }

    private static void SetOptionalState(GameObject target, bool isActive)
    {
        if (target != null)
        {
            target.SetActive(isActive);
        }
    }
}
