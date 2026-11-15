using System;
using System.Linq;
using FlipFriends.Network;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using System.Collections;

public sealed class QuantumPrepareScreen : NavigableScreen
{
    [SerializeField] private Button staticMapButton;
    [SerializeField] private Button ropeMapButton;
    [SerializeField] private Button offlineButton;
    [SerializeField] private Button onlineButton;
    [SerializeField] private Button cancelButton;
    [SerializeField] private TMP_Text status;
    [SerializeField] private PixelButton buttonPrefab;
    [SerializeField] private PixelButton primaryButtonPrefab;
    [SerializeField] private TMP_InputField codeFieldPrefab;
    [SerializeField] private Sprite panelBackground;
    [SerializeField] private Sprite hostIcon;
    private static readonly Color32 ControlInk = new(7, 53, 56, 255);
    private const float HostIconSize = 48;
    private const int MapColumns = 4;
    private const float MapButtonWidth = 290;
    private const float MapButtonHeight = 64;
    private const float MapButtonGap = 12;
    private const float MapRowTop = 245;
    private const float MiddleRowHeight = 64;
    private static readonly Vector2 OfflineStatusPosition = new(0, 60);
    private static readonly Vector2 OfflineStatusSize = new(1150, 120);
    private static readonly Vector2 OfflineTitlePosition = new(0, 322);
    private static readonly Vector2 OfflineTitleSize = new(1100, 60);
    private QuantumGameFlow flow;
    private Button interactionMapButton;
    private Button respawnMapButton;
    private Button deviceMapButton;
    private Button tilemapButton;
    private Button officialStageButton;
    private Button participantButton;
    private Button savedMapButton;
    private Button joinSavedMapButton;
    private Button hostLobbyButton;
    private Button submitCodeButton;
    private Button inviteButton;
    private Button joinInviteButton;
    private Sprite inviteSprite;
    private TMP_InputField codeInput;
    private TMP_Text lobbyHeader;
    private TMP_Text lobbyNotice;
    private TMP_Text title;
    private string offlineTitle;
    private Selectable[] offlineControls;
    private (Vector2 Position, Vector2 Size)[] offlineLayout;
    private bool isOnlineScreen;
    private bool wasInLobby;
    private float copiedUntil;
    private string onlineMessage;
    private bool isLobbyView;
    private bool isViewTransitioning;
    private bool refreshQueued;
    private GameObject rememberedOfflineSelection;
    private GameObject rememberedOnlineSelection;
    private GameObject pendingTransitionSelection;
    private CanvasGroup screenGroup;
    private System.Collections.Generic.IReadOnlyList<FlipFriends.Maps.QuantumOfficialStageAsset> officialStages = Array.Empty<FlipFriends.Maps.QuantumOfficialStageAsset>();

    protected override void Awake()
    {
        base.Awake();
        screenGroup = GetComponent<CanvasGroup>();
        ApplyPanelArtwork();
        interactionMapButton = AddMapChoice("FlipFriendsInteractionMapManifest", "상호작용 맵 선택", "밟기/운반 상자", 2);
        respawnMapButton = AddMapChoice("FlipFriendsRespawnMapManifest", "복귀 맵 선택", "리셋/추락 복귀", 3);
        deviceMapButton = AddMapChoice("FlipFriendsDeviceMapManifest", "장치 맵 선택", "체크포인트/소환", 4);
        tilemapButton = AddMapChoice("FlipFriendsTilemapMapManifest", "Tilemap 사용례 선택", "타일 지형 리셋", 5);
        AddOfficialStageButton();
        LayoutMapChoices();
        participantButton = CreateButton(buttonPrefab, "참가 인원 및 장치 배정", string.Empty);
        var participantRect = (RectTransform)participantButton.transform;
        participantRect.anchoredPosition = new Vector2(0, -45);
        participantRect.sizeDelta = new Vector2(440, MiddleRowHeight);
        Pixel(participantButton).SetFontRange(16, 26);
        savedMapButton = AddSavedMapControl("저장 맵 선택", -420, () => flow.SelectNextSavedMap());
        joinSavedMapButton = AddSavedMapControl("코드로 참가", 420, () =>
        {
            if (flow.Lobby != null) JoinSavedMap();
            else OpenCodeEntry();
        });
        status.richText = false;
        status.color = ControlInk;
        status.rectTransform.anchoredPosition = OfflineStatusPosition;
        status.rectTransform.sizeDelta = OfflineStatusSize;
        SetLabel(offlineButton, "오프라인 플레이 시작");
        participantButton.onClick.AddListener(() =>
        {
            if (flow.Lobby != null) flow.Lobby.ToggleReady();
            else flow.CycleParticipants();
        });
        GetComponent<ButtonSelectController>().tagetButtonList.Insert(0, participantButton);
        staticMapButton.onClick.AddListener(() => flow.SelectMap(0));
        ropeMapButton.onClick.AddListener(() => flow.SelectMap(1));
        offlineButton.onClick.AddListener(StartGame);
        onlineButton.onClick.AddListener(() =>
        {
            OpenOnlineEntry();
        });
        cancelButton.onClick.AddListener(OnCancel);
        offlineControls = GetComponent<ButtonSelectController>().tagetButtonList.ToArray();
        title = GetComponentsInChildren<TMP_Text>(true).FirstOrDefault(label =>
            label.transform.parent == status.transform.parent && label != status);
        offlineTitle = "플레이 준비";
        ApplyOfflineTitle();
        offlineLayout = offlineControls.Select(control =>
        {
            var rect = (RectTransform)control.transform;
            return (rect.anchoredPosition, rect.sizeDelta);
        }).ToArray();
        hostLobbyButton = AddLobbyButton(primaryButtonPrefab, "방 만들기", 0, 175, HostLobby);
        Pixel(hostLobbyButton).SetFontRange(18, 46);
        Pixel(hostLobbyButton).SetIcon(hostIcon, HostIconSize);
        submitCodeButton = AddLobbyButton(buttonPrefab, "참가", 370, 65, JoinSavedMap);
        inviteButton = AddLobbyButton(buttonPrefab, "친구 초대", 230, 230, InviteFriend);
        joinInviteButton = AddLobbyButton(buttonPrefab, "받은 초대로 참가", 0, -30, InviteFriend);
        ApplyInviteArtwork();
        lobbyHeader = Instantiate(status, status.transform.parent);
        lobbyHeader.rectTransform.anchoredPosition = new Vector2(0, 285);
        lobbyHeader.rectTransform.sizeDelta = new Vector2(950, 56);
        lobbyNotice = Instantiate(status, status.transform.parent);
        lobbyNotice.rectTransform.anchoredPosition = new Vector2(0, -235);
        lobbyNotice.rectTransform.sizeDelta = new Vector2(1000, 65);
        lobbyNotice.enableAutoSizing = true;
        lobbyNotice.fontSizeMin = 20;
        lobbyNotice.fontSizeMax = 28;
        codeInput = CreateCodeInput();
        codeInput.gameObject.SetActive(false);
        lobbyHeader.gameObject.SetActive(false);
        lobbyNotice.gameObject.SetActive(false);
        foreach (var button in new[] { hostLobbyButton, submitCodeButton, inviteButton, joinInviteButton })
            button.gameObject.SetActive(false);
    }

    private void ApplyPanelArtwork()
    {
        if (panelBackground == null) return;
        Image panel = GetComponent<Image>();
        if (panel == null) return;
        panel.sprite = panelBackground;
        panel.type = Image.Type.Sliced;
        panel.pixelsPerUnitMultiplier = 1f;
        panel.color = Color.white;
        panel.raycastTarget = false;
    }

    private Button CreateButton(PixelButton prefab, string objectName, string label)
    {
        var button = Instantiate(prefab, staticMapButton.transform.parent);
        button.name = objectName;
        button.Text = label;
        return button.Button;
    }

    private Button AddMapChoice(string manifest, string objectName, string label, int mapIndex)
    {
        if (Resources.Load<FlipFriends.Maps.QuantumCustomMapAsset>(manifest) == null) return null;
        var button = CreateButton(buttonPrefab, objectName, label);
        button.onClick.AddListener(() => flow.SelectMap(mapIndex));
        GetComponent<ButtonSelectController>().tagetButtonList.Insert(mapIndex, button);
        return button;
    }

    private static PixelButton Pixel(Button button) => button.GetComponent<PixelButton>();

    private void TransitionView(System.Action swapContent)
    {
        if (isViewTransitioning) return;
        StartCoroutine(TransitionViewRoutine(swapContent));
    }

    private IEnumerator TransitionViewRoutine(System.Action swapContent)
    {
        isViewTransitioning = true;
        if (screenGroup != null)
        {
            screenGroup.interactable = false;
            screenGroup.blocksRaycasts = false;
            for (int step = 1; step <= 4; step++)
            {
                screenGroup.alpha = 1f - step / 4f;
                yield return new WaitForSecondsRealtime(0.025f);
            }
        }

        swapContent?.Invoke();

        if (screenGroup != null)
        {
            screenGroup.interactable = true;
            screenGroup.blocksRaycasts = true;
            for (int step = 1; step <= 4; step++)
            {
                screenGroup.alpha = step / 4f;
                yield return new WaitForSecondsRealtime(0.025f);
            }
        }

        isViewTransitioning = false;
        if (pendingTransitionSelection != null)
        {
            var pendingTarget = pendingTransitionSelection;
            pendingTransitionSelection = null;
            RequestRememberedSelection(pendingTarget, pendingTarget.GetComponent<Selectable>());
        }
        if (refreshQueued)
        {
            refreshQueued = false;
            Refresh();
        }
    }

    private void ApplyInviteArtwork()
    {
        const string artworkPath = "CodexUI/codex_invite_button";
        var source = Resources.Load<Sprite>(artworkPath);
        var texture = source != null ? source.texture : Resources.Load<Texture2D>(artworkPath);
        if (texture == null) return;

        // 생성 이미지의 투명 여백을 제외해 버튼 높이와 클릭 영역을 맞춘다.
        var crop = new Rect(texture.width * 0.015f, texture.height * 0.145f,
            texture.width * 0.97f, texture.height * 0.71f);
        inviteSprite = Sprite.Create(texture, crop, new Vector2(0.5f, 0.5f));
        inviteButton.image.sprite = inviteSprite;
        inviteButton.image.type = Image.Type.Simple;
        // 초대 PNG는 밝은 바탕이라 보조 프레임용 밝은 글자 대신 짙은 글자를 쓴다.
        var label = Pixel(inviteButton).Label;
        label.color = ControlInk;
        label.fontSizeMax = 26;
    }

    private Button AddLobbyButton(PixelButton prefab, string label, float x, float y, UnityEngine.Events.UnityAction click)
    {
        var button = AddSavedMapControl(prefab, label, x, click);
        ((RectTransform)button.transform).anchoredPosition = new Vector2(x, y);
        return button;
    }

    private TMP_InputField CreateCodeInput()
    {
        var input = Instantiate(codeFieldPrefab, staticMapButton.transform.parent);
        input.name = "방 코드 입력";
        var rect = (RectTransform)input.transform;
        rect.anchoredPosition = new Vector2(-137, -25);
        rect.sizeDelta = new Vector2(590, 88);
        input.onValueChanged.AddListener(HandleCodeChanged);
        return input;
    }

    private void HandleCodeChanged(string value)
    {
        if (submitCodeButton == null) return;
        bool pending = flow != null && flow.IsBusy || SteamLobbyService.Current?.IsLobbyRequestPending == true;
        submitCodeButton.interactable = !pending && !string.IsNullOrWhiteSpace(value);
    }

    private Button AddSavedMapControl(string label, float x, UnityEngine.Events.UnityAction click) =>
        AddSavedMapControl(buttonPrefab, label, x, click);

    private Button AddSavedMapControl(PixelButton prefab, string label, float x, UnityEngine.Events.UnityAction click)
    {
        var button = CreateButton(prefab, label, label);
        var rect = (RectTransform)button.transform;
        rect.anchoredPosition = new Vector2(x, -45);
        rect.sizeDelta = new Vector2(370, MiddleRowHeight);
        Pixel(button).Label.richText = false;
        button.onClick.AddListener(click);
        GetComponent<ButtonSelectController>().tagetButtonList.Add(button);
        return button;
    }

    private async void JoinSavedMap()
    {
        try
        {
            if (flow.Lobby != null)
            {
                GUIUtility.systemCopyBuffer = flow.Lobby.Code;
                copiedUntil = Time.unscaledTime + 1.5f;
                Refresh();
                return;
            }
            if (flow.IsBusy || SteamLobbyService.Current?.IsLobbyRequestPending == true) return;
            onlineMessage = null;
            string code = codeInput.text.Trim();
            if (code.Contains(':')) await flow.JoinSavedMapAsync(code);
            else if (code.Length != 8 || !code.All(character => character >= '0' && character <= '9'))
                flow.ReportLobby("방 코드 숫자 8자리를 확인해 주세요.");
            else if (SteamLobbyService.Instance != null)
                await SteamLobbyService.Instance.JoinQuantumLobbyByCodeAsync(code);
            else flow.ReportLobby("Steam에 연결할 수 없어요.");
        }
        catch (Exception error) { Debug.LogException(error, this); }
    }

    private void OpenOnlineEntry(bool focusCode = false)
    {
        if (EventSystem.current != null && !UIInputModeController.IsPointerMode)
        {
            var current = EventSystem.current.currentSelectedGameObject;
            var selectedControl = current != null ? current.GetComponent<Selectable>() : null;
            rememberedOfflineSelection = offlineControls != null && offlineControls.Contains(selectedControl)
                ? current
                : onlineButton.gameObject;
        }
        else rememberedOfflineSelection = onlineButton.gameObject;
        isOnlineScreen = true;
        onlineMessage = flow.Message;
        TransitionView(() =>
        {
            RefreshContent();
            pendingTransitionSelection = focusCode ? codeInput.gameObject : hostLobbyButton.gameObject;
        });
    }

    private void OpenCodeEntry()
    {
        OpenOnlineEntry(true);
    }

    private async void InviteFriend()
    {
        try
        {
            if (flow.Lobby != null) flow.Lobby.Invite();
            else if (flow.CanInviteSavedMapFriends) flow.InviteSavedMapFriends();
            else if (flow.HasSteamInvite) await flow.JoinSavedMapAsync(null);
        }
        catch (Exception error) { Debug.LogException(error, this); }
    }

    // 공식 스테이지는 개수가 늘어나므로 버튼 하나로 목록을 차례로 고른다.
    private void AddOfficialStageButton()
    {
        try { officialStages = FlipFriends.Maps.QuantumOfficialStageCatalog.LoadStages(); }
        catch (InvalidOperationException error) { Debug.LogException(error, this); return; }
        if (officialStages.Count == 0 || tilemapButton == null) return;
        officialStageButton = CreateButton(buttonPrefab, "공식 스테이지 선택", string.Empty);
        officialStageButton.onClick.AddListener(SelectNextOfficialStage);
        GetComponent<ButtonSelectController>().tagetButtonList.Insert(6, officialStageButton);
    }

    // 맵 버튼을 한 줄에 몰면 패널 폭을 넘고 라벨이 두 줄로 잘려서, 4열 격자에 한 줄 라벨로 배치한다.
    private void LayoutMapChoices()
    {
        var choices = new[] { staticMapButton, ropeMapButton, interactionMapButton, respawnMapButton, deviceMapButton, tilemapButton, officialStageButton }
            .Where(button => button != null).ToArray();
        for (int i = 0; i < choices.Length; i++)
        {
            int row = i / MapColumns;
            int column = i % MapColumns;
            int columnsInRow = Mathf.Min(MapColumns, choices.Length - row * MapColumns);
            float x = (column - (columnsInRow - 1) * 0.5f) * (MapButtonWidth + MapButtonGap);
            float y = MapRowTop - row * (MapButtonHeight + MapButtonGap);
            var rect = (RectTransform)choices[i].transform;
            rect.anchoredPosition = new Vector2(x, y);
            rect.sizeDelta = new Vector2(MapButtonWidth, MapButtonHeight);
            Pixel(choices[i]).SetFontRange(16, 24);
        }
    }

    private void SelectNextOfficialStage()
    {
        int first = flow.OfficialMapIndex(0);
        int next = flow.SelectedMap >= first ? flow.SelectedMap + 1 : first;
        flow.SelectMap(next < flow.MapCount ? next : first);
    }

    protected override void OnShow()
    {
        flow = QuantumGameFlow.Instance;
        isLobbyView = flow.Lobby != null;
        if (flow.ShouldShowLobbyScreen) isOnlineScreen = true;
        flow.AcknowledgeLobbyScreen();
        flow.Changed += Refresh;
        InputSystem.onDeviceChange += OnDeviceChange;
        Refresh();
    }

    protected override void OnHide()
    {
        InputSystem.onDeviceChange -= OnDeviceChange;
        if (flow != null)
        {
            flow.Changed -= Refresh;
            if (!flow.IsBusy) flow.ClearSavedSelection();
        }
    }

    private void HostLobby()
    {
        onlineMessage = null;
        if (SteamLobbyService.Instance != null)
            SteamLobbyService.Instance.HostQuantumLobby(QuantumGameContent.MaxPlayers);
        else flow.ReportLobby("Steam에 연결할 수 없어요.");
    }

    private async void StartGame()
    {
        try
        {
            if (flow.Lobby != null) flow.Lobby.Start();
            else await flow.StartAsync(false);
        }
        catch (Exception error) { Debug.LogException(error); }
    }

    protected override async void OnCancel()
    {
        if (flow == null) return;
        try
        {
            if (flow.Lobby != null)
            {
                if (flow.Lobby.IsLocked) await flow.ReturnAsync("로비 시작을 취소했습니다.");
                else flow.Lobby.Leave("Steam 로비를 나갔습니다.");
                return;
            }
            if (SteamLobbyService.Current?.IsLobbyRequestPending == true) return;
            if (isOnlineScreen && !flow.IsBusy)
            {
                if (EventSystem.current != null) rememberedOnlineSelection = EventSystem.current.currentSelectedGameObject;
                isOnlineScreen = false;
                TransitionView(() =>
                {
                    RefreshContent();
                    pendingTransitionSelection = rememberedOfflineSelection != null
                        ? rememberedOfflineSelection
                        : onlineButton.gameObject;
                });
                return;
            }
            await flow.ReturnAsync("시작을 취소했습니다. 다시 시작할 수 있습니다.");
            if (isOnlineScreen) return;
            if (this != null && IsVisible) NavigateBack();
        }
        catch (Exception error) { Debug.LogException(error); }
    }

    private void OnDeviceChange(InputDevice device, InputDeviceChange change)
    {
        if (device is Keyboard || device is Gamepad) Refresh();
    }

    public override void SelectDefault() => EnsureSelection();

    private void Update()
    {
        if (copiedUntil <= 0 || Time.unscaledTime < copiedUntil) return;
        copiedUntil = 0;
        if (IsVisible) Refresh();
    }

    private void RefreshOnlineEntry()
    {
        bool searching = SteamLobbyService.Current?.IsLobbyRequestPending == true;
        bool pending = flow.IsBusy || searching;
        hostLobbyButton.interactable = codeInput.interactable = !pending;
        submitCodeButton.interactable = !pending && !string.IsNullOrWhiteSpace(codeInput.text);
        cancelButton.interactable = !searching && flow.State != QuantumGameState.Stopping;
        SetLabel(cancelButton, flow.State == QuantumGameState.Starting ? "취소" : "뒤로");
        inviteButton.interactable = !pending || flow.CanInviteSavedMapFriends;
        lobbyHeader.text = title == null ? "온라인 플레이" : string.Empty;
        if (title != null)
        {
            title.text = "온라인 플레이";
            title.color = ControlInk;
            title.enableAutoSizing = true;
            title.fontSizeMin = 40;
            title.fontSizeMax = 54;
        }
        lobbyHeader.color = ControlInk;
        joinInviteButton.interactable = !pending;
        status.text = string.Empty;
        lobbyNotice.text = !pending && flow.Message == onlineMessage ? string.Empty : flow.Message;
        if (title != null)
        {
            title.rectTransform.anchoredPosition = new Vector2(0, 305);
            title.rectTransform.sizeDelta = new Vector2(1100, 70);
            title.alignment = TextAlignmentOptions.Center;
            title.fontSize = 50;
        }
        lobbyNotice.rectTransform.anchoredPosition = new Vector2(0, -112);
        lobbyNotice.rectTransform.sizeDelta = new Vector2(1080, 42);
        lobbyNotice.fontSizeMin = 18;
        lobbyNotice.fontSizeMax = 25;
        lobbyNotice.alignment = TextAlignmentOptions.Center;
        SetLayout((hostLobbyButton, 0, 130, 900), (submitCodeButton, 305, -25, 270),
            (flow.CanInviteSavedMapFriends ? inviteButton : flow.HasSteamInvite ? joinInviteButton : null, 0, -185,
                flow.CanInviteSavedMapFriends && inviteSprite != null ? 360 : 440),
            (cancelButton, 0, -290, 300));
        ((RectTransform)codeInput.transform).anchoredPosition = new Vector2(-137, -25);
        ((RectTransform)codeInput.transform).sizeDelta = new Vector2(590, 88);
        codeInput.gameObject.SetActive(true);
        GetComponent<ButtonSelectController>().tagetButtonList.Insert(1, codeInput);
        EnsureSelection();
    }

    private string LobbyMessage(QuantumLobby lobby)
    {
        if (flow.State == QuantumGameState.Failed || lobby.IsLocked) return flow.Message;
        return lobby.IsOwner ? lobby.StartBlockReason ?? "모두 준비했어요." :
            lobby.MapName == null ? "방장이 맵을 고르는 중이에요." :
            lobby.IsReady ? "방장이 시작하기를 기다려요." : "맵을 확인하고 준비하세요.";
    }

    private void SetLayout(params (Button Button, float X, float Y, float Width)[] controls)
    {
        var navigation = GetComponent<ButtonSelectController>().tagetButtonList;
        navigation.Clear();
        foreach (var button in offlineControls.Concat(new[] { hostLobbyButton, submitCodeButton, inviteButton, joinInviteButton }))
            button.gameObject.SetActive(controls.Any(control => control.Button == button));
        foreach (var (button, x, y, width) in controls)
        {
            if (button == null) continue;
            var rect = (RectTransform)button.transform;
            rect.anchoredPosition = new Vector2(x, y);
            float height = button == hostLobbyButton ? 124 : button == submitCodeButton ? 88 : button == cancelButton ? 80 : 72;
            rect.sizeDelta = new Vector2(width, height);
            // 초대 PNG는 왼쪽에 슬라임 그림이 있어 라벨을 오른쪽 빈 칸에만 둔다.
            if (button == inviteButton && inviteSprite != null)
                Pixel(button).SetLabelInsets(width * 0.34f, width * 0.04f);
            navigation.Add(button);
        }
        bool inLobby = flow.Lobby != null;
        if (inLobby) codeInput.gameObject.SetActive(false);
        lobbyHeader.gameObject.SetActive(true);
        lobbyNotice.gameObject.SetActive(true);
        status.rectTransform.anchoredPosition = new Vector2(0, 78);
        status.rectTransform.sizeDelta = new Vector2(1150, 220);
        status.enableAutoSizing = true;
        status.fontSizeMin = 16;
        status.fontSizeMax = 32;
        if (inLobby) EnsureSelection();
    }

    private void RestoreOfflineLayout()
    {
        var navigation = GetComponent<ButtonSelectController>().tagetButtonList;
        navigation.Clear();
        for (int i = 0; i < offlineControls.Length; i++)
        {
            var control = offlineControls[i];
            control.gameObject.SetActive(true);
            var rect = (RectTransform)control.transform;
            var layout = offlineLayout[i];
            rect.anchoredPosition = layout.Position;
            rect.sizeDelta = layout.Size;
            navigation.Add(control);
        }
        foreach (var button in new[] { hostLobbyButton, submitCodeButton, inviteButton, joinInviteButton })
            button.gameObject.SetActive(false);
        codeInput.gameObject.SetActive(false);
        lobbyHeader.gameObject.SetActive(false);
        lobbyNotice.gameObject.SetActive(false);
        status.rectTransform.anchoredPosition = OfflineStatusPosition;
        status.rectTransform.sizeDelta = OfflineStatusSize;
        status.enableAutoSizing = true;
        status.fontSizeMin = 18;
        status.fontSizeMax = 26;
        ApplyOfflineTitle();
        SetLabel(cancelButton, "뒤로");
    }

    // 온라인 화면이 제목 위치·크기를 바꾸므로 오프라인으로 돌아올 때마다 다시 맞춘다.
    private void ApplyOfflineTitle()
    {
        if (title == null) return;
        title.text = offlineTitle;
        // 씬의 흰 제목색은 크림색 패널 위에서 읽히지 않는다.
        title.color = ControlInk;
        title.rectTransform.anchoredPosition = OfflineTitlePosition;
        title.rectTransform.sizeDelta = OfflineTitleSize;
        title.alignment = TextAlignmentOptions.Center;
        title.enableAutoSizing = true;
        title.fontSizeMin = 36;
        title.fontSizeMax = 50;
    }

    private void EnsureSelection()
    {
        if (EventSystem.current == null) return;
        var navigation = GetComponent<ButtonSelectController>().tagetButtonList;
        var selected = EventSystem.current.currentSelectedGameObject;
        if (navigation.Any(item => item.gameObject == selected && item.IsInteractable())) return;
        var first = navigation.FirstOrDefault(item => item.IsInteractable());
        if (first != null) UIInputModeController.RequestSelection(first.gameObject);
    }

    private void RequestRememberedSelection(GameObject preferred, Selectable fallback)
    {
        if (EventSystem.current == null) return;
        Selectable target = preferred != null ? preferred.GetComponent<Selectable>() : null;
        if (target == null || !target.gameObject.activeInHierarchy || !target.IsInteractable()) target = fallback;
        if (target != null && target.gameObject.activeInHierarchy && target.IsInteractable())
            UIInputModeController.RequestSelection(target.gameObject);
        else EnsureSelection();
    }

    private void Refresh()
    {
        if (flow == null) return;
        if (isViewTransitioning)
        {
            refreshQueued = true;
            return;
        }

        bool lobbyIsVisible = flow.Lobby != null;
        if (lobbyIsVisible != isLobbyView)
        {
            isLobbyView = lobbyIsVisible;
            TransitionView(RefreshContent);
            return;
        }

        RefreshContent();
    }

    private void RefreshContent()
    {
        var lobby = flow.Lobby;
        if (lobby != null)
        {
            isOnlineScreen = wasInLobby = true;
            bool canSelect = lobby.IsOwner && !lobby.IsLocked;
            if (officialStageButton != null) officialStageButton.interactable = canSelect;
            savedMapButton.interactable = canSelect;
            participantButton.interactable = !lobby.IsLocked && lobby.MapName != null;
            offlineButton.interactable = lobby.CanStart;
            inviteButton.interactable = !lobby.IsLocked;
            joinSavedMapButton.interactable = !string.IsNullOrEmpty(lobby.Code);
            cancelButton.interactable = flow.State != QuantumGameState.Stopping;
            SetLabel(participantButton, lobby.IsReady ? "준비 해제" : "준비");
            SetLabel(offlineButton, "시작");
            SetLabel(joinSavedMapButton, copiedUntil > Time.unscaledTime ? "복사됨" : "코드 복사");
            SetLabel(savedMapButton, "커스텀 맵");
            if (officialStageButton != null) SetLabel(officialStageButton, "공식 맵");
            SetLabel(inviteButton, "친구 초대");
            SetLabel(cancelButton, flow.State == QuantumGameState.Starting ? "취소" : "나가기");
            lobbyHeader.text = "방 코드  " + (lobby.Code ?? "발급 중…");
            if (title != null)
            {
                title.text = "친구 방";
                title.color = ControlInk;
            }
            lobbyHeader.color = ControlInk;
            status.text = $"선택 맵  {lobby.MapName ?? "선택 대기"}\n{lobby.Status}";
            lobbyNotice.text = LobbyMessage(lobby);
            SetLayout(
                (joinSavedMapButton, -230, 230, 370), (inviteButton, 230, 230, inviteSprite != null ? 224 : 370),
                (lobby.IsOwner ? officialStageButton : null, -230, -75, 370),
                (lobby.IsOwner ? savedMapButton : null, 230, -75, 370),
                (participantButton, -270, -165, 245),
                (lobby.IsOwner ? offlineButton : null, 0, -165, 245),
                (cancelButton, 270, -165, 245));
            return;
        }
        if (wasInLobby)
        {
            wasInLobby = false;
            copiedUntil = 0;
            onlineMessage = null;
        }
        if (flow.CanInviteSavedMapFriends) isOnlineScreen = true;
        if (isOnlineScreen)
        {
            RefreshOnlineEntry();
            return;
        }
        RestoreOfflineLayout();
        SetLabel(offlineButton, "오프라인 플레이 시작");
        bool isBusy = flow.State == QuantumGameState.Starting || flow.State == QuantumGameState.Stopping;
        bool isSavedMap = flow.SavedMapName != null;
        int minimum = isSavedMap ? 1 : flow.SelectedContent.MinimumPlayers;
        bool hasMinimum = flow.PlayerCount >= minimum;
        savedMapButton.interactable = !isBusy;
        joinSavedMapButton.interactable = !isBusy || flow.CanInviteSavedMapFriends;
        SetLabel(joinSavedMapButton, flow.HasSteamInvite ? "받은 초대로 참가" : "코드로 참가");
        SetLabel(savedMapButton, isSavedMap ? "저장 맵: " + flow.SavedMapName : "저장 맵 선택");
        SetLabel(onlineButton, "온라인 플레이");
        offlineButton.interactable = !isBusy && !flow.HasRejectedSavedMap && hasMinimum && flow.HasLocalDevices;
        onlineButton.interactable = !isBusy && (!isSavedMap || hasMinimum);
        participantButton.interactable = !isBusy;
        SetLabel(participantButton, $"참가 {flow.PlayerCount}명 / 키보드 {flow.KeyboardSlots}명 / 패드 {flow.PlayerCount - flow.KeyboardSlots}명");
        staticMapButton.interactable = ropeMapButton.interactable = !isBusy;
        if (interactionMapButton != null) interactionMapButton.interactable = !isBusy;
        if (respawnMapButton != null) respawnMapButton.interactable = !isBusy;
        if (deviceMapButton != null) deviceMapButton.interactable = !isBusy;
        if (tilemapButton != null) tilemapButton.interactable = !isBusy;
        bool isOfficial = !isSavedMap && flow.SelectedMap >= flow.SupportedMaps.Length;
        if (officialStageButton != null)
        {
            officialStageButton.interactable = !isBusy;
            Pixel(officialStageButton).Text = isOfficial ? "공식: " + flow.SelectedContent.DisplayName : $"공식 스테이지 {officialStages.Count}개";
        }
        cancelButton.interactable = flow.State != QuantumGameState.Stopping;
        EnsureSelection();
        status.text = flow.Message + "\n선택 맵: " + (flow.HasRejectedSavedMap ? "선택할 수 없음" : isSavedMap ? flow.SavedMapName : flow.SelectedMap == 0 ? "정적 지형" : flow.SelectedMap == 1 ? "로프" : flow.SelectedMap == 2 ? "밟기/운반/상자" : flow.SelectedMap == 3 ? "리셋/추락 복귀" : flow.SelectedMap == 4 ? "체크포인트/구간/소환" : isOfficial ? "공식 스테이지 " + flow.SelectedContent.DisplayName : "Tilemap 사용례 (전체 스테이지 아님)") +
            $"\n최소 {minimum}명 / 최대 {QuantumGameContent.MaxPlayers}명 / 연결된 게임패드 {Gamepad.all.Count}개" +
            (!hasMinimum ? "\n최소 인원에 도달해야 시작할 수 있습니다." : !flow.HasLocalDevices ? "\n오프라인 플레이에 필요한 입력 장치를 연결하세요." : "\n배정을 확인하고 플레이를 시작하세요.");
    }

    private static void SetLabel(Button button, string value) => Pixel(button).Text = value;

    private void OnDestroy()
    {
        OnHide();
        if (inviteSprite != null) Destroy(inviteSprite);
    }
}
