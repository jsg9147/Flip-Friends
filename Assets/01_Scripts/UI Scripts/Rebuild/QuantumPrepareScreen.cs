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
    [SerializeField] private Button legacyButton;
    [SerializeField] private TMP_Text status;
    [SerializeField] private Sprite controlFrame;
    [SerializeField] private Sprite panelBackground;
    [SerializeField] private Sprite primaryFrame;
    [SerializeField] private Sprite secondaryFrame;
    [SerializeField] private Sprite codeFieldFrame;
    [SerializeField] private Sprite hostIcon;
    [SerializeField] private Sprite backIcon;
    private static readonly Color32 ControlInk = new(7, 53, 56, 255);
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
    private Image inviteFrameFill;
    private TMP_InputField codeInput;
    private TMP_Text lobbyHeader;
    private TMP_Text lobbyNotice;
    private TMP_Text title;
    private string offlineTitle;
    private Selectable[] offlineControls;
    private (Vector2 Position, Vector2 Size, Vector2 LabelSize)[] offlineLayout;
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
        if (Resources.Load<FlipFriends.Maps.QuantumCustomMapAsset>("FlipFriendsInteractionMapManifest") != null)
        {
            interactionMapButton = Instantiate(ropeMapButton, ropeMapButton.transform.parent);
            interactionMapButton.name = "상호작용 맵 선택";
            interactionMapButton.GetComponentInChildren<TMP_Text>().text = "밟기·운반\n상자";
            var choices = new[] { staticMapButton, ropeMapButton, interactionMapButton };
            for (int i = 0; i < choices.Length; i++)
            {
                var rect = (RectTransform)choices[i].transform;
                rect.anchoredPosition = new Vector2((i - 1) * 410, rect.anchoredPosition.y);
                rect.sizeDelta = new Vector2(390, rect.sizeDelta.y);
                ((RectTransform)choices[i].GetComponentInChildren<TMP_Text>().transform).sizeDelta = new Vector2(370, 70);
            }
            interactionMapButton.onClick.AddListener(() => flow.SelectMap(2));
            GetComponent<ButtonSelectController>().tagetButtonList.Insert(2, interactionMapButton);
        }
        if (Resources.Load<FlipFriends.Maps.QuantumCustomMapAsset>("FlipFriendsRespawnMapManifest") != null)
        {
            respawnMapButton = Instantiate(ropeMapButton, ropeMapButton.transform.parent);
            respawnMapButton.name = "복귀 맵 선택";
            respawnMapButton.GetComponentInChildren<TMP_Text>().text = "리셋·추락\n복귀";
            respawnMapButton.onClick.AddListener(() => flow.SelectMap(3));
            GetComponent<ButtonSelectController>().tagetButtonList.Insert(3, respawnMapButton);
            var choices = new[] { staticMapButton, ropeMapButton, interactionMapButton, respawnMapButton };
            for (int i = 0; i < choices.Length; i++)
            {
                var rect = (RectTransform)choices[i].transform;
                rect.anchoredPosition = new Vector2((i - 1.5f) * 310, rect.anchoredPosition.y);
                rect.sizeDelta = new Vector2(290, rect.sizeDelta.y);
                ((RectTransform)choices[i].GetComponentInChildren<TMP_Text>().transform).sizeDelta = new Vector2(270, 70);
            }
        }
        if (Resources.Load<FlipFriends.Maps.QuantumCustomMapAsset>("FlipFriendsDeviceMapManifest") != null)
        {
            deviceMapButton = Instantiate(ropeMapButton, ropeMapButton.transform.parent);
            deviceMapButton.name = "장치 맵 선택";
            deviceMapButton.GetComponentInChildren<TMP_Text>().text = "체크포인트\n구간·소환";
            deviceMapButton.onClick.AddListener(() => flow.SelectMap(4));
            GetComponent<ButtonSelectController>().tagetButtonList.Insert(4, deviceMapButton);
            var choices = new[] { staticMapButton, ropeMapButton, interactionMapButton, respawnMapButton, deviceMapButton };
            for (int i = 0; i < choices.Length; i++)
            {
                var rect = (RectTransform)choices[i].transform;
                rect.anchoredPosition = new Vector2((i - 2f) * 260, rect.anchoredPosition.y);
                rect.sizeDelta = new Vector2(250, rect.sizeDelta.y);
                var label = choices[i].GetComponentInChildren<TMP_Text>();
                ((RectTransform)label.transform).sizeDelta = new Vector2(240, 70);
                label.enableAutoSizing = true;
            }
        }
        if (Resources.Load<FlipFriends.Maps.QuantumCustomMapAsset>("FlipFriendsTilemapMapManifest") != null)
        {
            tilemapButton = Instantiate(ropeMapButton, ropeMapButton.transform.parent);
            tilemapButton.name = "Tilemap 사용례 선택";
            tilemapButton.GetComponentInChildren<TMP_Text>().text = "타일 지형\n구간 Reset";
            tilemapButton.onClick.AddListener(() => flow.SelectMap(5));
            GetComponent<ButtonSelectController>().tagetButtonList.Insert(5, tilemapButton);
            var choices = new[] { staticMapButton, ropeMapButton, interactionMapButton, respawnMapButton, deviceMapButton, tilemapButton };
            for (int i = 0; i < choices.Length; i++)
            {
                var rect = (RectTransform)choices[i].transform;
                rect.anchoredPosition = new Vector2((i - 2.5f) * 215, rect.anchoredPosition.y);
                rect.sizeDelta = new Vector2(205, rect.sizeDelta.y);
                var label = choices[i].GetComponentInChildren<TMP_Text>();
                ((RectTransform)label.transform).sizeDelta = new Vector2(195, 70); label.enableAutoSizing = true;
            }
        }
        AddOfficialStageButton();
        participantButton = Instantiate(offlineButton, offlineButton.transform.parent);
        participantButton.name = "참가 인원 및 장치 배정";
        var participantRect = (RectTransform)participantButton.transform;
        participantRect.anchoredPosition = new Vector2(0, -45);
        participantRect.sizeDelta = new Vector2(440, 54);
        savedMapButton = AddSavedMapControl("저장 맵 선택", -420, () => flow.SelectNextSavedMap());
        joinSavedMapButton = AddSavedMapControl("코드로 참가", 420, () =>
        {
            if (flow.Lobby != null) JoinSavedMap();
            else OpenCodeEntry();
        });
        status.richText = false;
        status.color = ControlInk;
        status.rectTransform.anchoredPosition = new Vector2(0, 100);
        status.rectTransform.sizeDelta = new Vector2(1150, 145);
        offlineButton.GetComponentInChildren<TMP_Text>().text = "오프라인 플레이 시작";
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
        legacyButton.onClick.AddListener(() =>
        {
            if (flow.Lobby != null) flow.Lobby.Leave("Steam 로비를 나갔습니다.");
            else Navigator.Open("mode-select");
        });
        offlineControls = GetComponent<ButtonSelectController>().tagetButtonList.ToArray();
        title = GetComponentsInChildren<TMP_Text>(true).FirstOrDefault(label =>
            label.transform.parent == status.transform.parent && label != status);
        offlineTitle = title != null ? title.text : null;
        if (title != null)
        {
            offlineTitle = "플레이 준비";
            title.text = offlineTitle;
            title.enableAutoSizing = true;
            title.fontSizeMin = 36;
            title.fontSizeMax = 54;
        }
        offlineLayout = offlineControls.Select(control =>
        {
            var rect = (RectTransform)control.transform;
            return (rect.anchoredPosition, rect.sizeDelta, control.GetComponentInChildren<TMP_Text>().rectTransform.sizeDelta);
        }).ToArray();
        hostLobbyButton = AddLobbyButton("방 만들기", 0, 175, HostLobby);
        submitCodeButton = AddLobbyButton("참가", 370, 65, JoinSavedMap);
        inviteButton = AddLobbyButton("친구 초대", 230, 230, InviteFriend);
        joinInviteButton = AddLobbyButton("받은 초대로 참가", 0, -30, InviteFriend);
        foreach (var control in offlineControls.Concat(new[] { hostLobbyButton, submitCodeButton, inviteButton, joinInviteButton }))
        {
            var fill = ApplyControlFrame(control, ((Button)control).image, secondaryFrame != null ? secondaryFrame : controlFrame);
            if (control == inviteButton) inviteFrameFill = fill;
            var label = control.GetComponentInChildren<TMP_Text>();
            label.color = ControlInk;
            label.enableAutoSizing = true;
            label.fontSizeMax = control == hostLobbyButton ? 46 : 28;
        }
        ApplyControlFrame(hostLobbyButton, hostLobbyButton.image, primaryFrame != null ? primaryFrame : secondaryFrame);
        AddButtonIcon(hostLobbyButton, hostIcon, 48);
        AddButtonIcon(cancelButton, backIcon, 34);
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

    private Image ApplyControlFrame(Selectable control, Image image, Sprite frame)
    {
        if (frame == null) return null;
        image.sprite = frame;
        image.type = Image.Type.Sliced;
        image.pixelsPerUnitMultiplier = 1f;
        image.color = Color.white;
        control.transition = Selectable.Transition.ColorTint;
        var colors = control.colors;
        colors.normalColor = new Color32(168, 194, 180, 255);
        colors.highlightedColor = Color.white;
        colors.selectedColor = colors.highlightedColor;
        colors.pressedColor = new Color32(135, 190, 160, 255);
        colors.disabledColor = new Color32(95, 110, 110, 180);
        control.colors = colors;
        return null;
    }

    private void AddButtonIcon(Button button, Sprite sprite, float size)
    {
        if (button == null || sprite == null) return;
        var iconObject = new GameObject("도트 아이콘", typeof(RectTransform), typeof(Image));
        iconObject.transform.SetParent(button.transform, false);
        var icon = iconObject.GetComponent<Image>();
        icon.sprite = sprite;
        icon.preserveAspect = true;
        icon.raycastTarget = false;
        var iconRect = icon.rectTransform;
        iconRect.anchorMin = iconRect.anchorMax = new Vector2(0.5f, 0.5f);
        iconRect.anchoredPosition = new Vector2(-((RectTransform)button.transform).sizeDelta.x * 0.5f + size * 0.8f, 0);
        iconRect.sizeDelta = new Vector2(size, size);
        TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
        {
            label.rectTransform.anchoredPosition += new Vector2(size * 0.35f, 0);
            label.rectTransform.sizeDelta -= new Vector2(size * 0.7f, 0);
        }
    }

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
        if (inviteFrameFill != null) inviteFrameFill.gameObject.SetActive(false);
        var label = inviteButton.GetComponentInChildren<TMP_Text>();
        label.color = ControlInk;
        label.fontSizeMax = 26;
    }

    private Button AddLobbyButton(string label, float x, float y, UnityEngine.Events.UnityAction click)
    {
        var button = AddSavedMapControl(label, x, click);
        ((RectTransform)button.transform).anchoredPosition = new Vector2(x, y);
        return button;
    }

    private TMP_InputField CreateCodeInput()
    {
        var root = new GameObject("방 코드 입력", typeof(RectTransform), typeof(Image), typeof(TMP_InputField));
        root.transform.SetParent(offlineButton.transform.parent, false);
        var rect = (RectTransform)root.transform;
        rect.anchoredPosition = new Vector2(-137, -25);
        rect.sizeDelta = new Vector2(590, 88);
        var image = root.GetComponent<Image>();
        image.sprite = codeFieldFrame != null ? codeFieldFrame : offlineButton.image.sprite;
        image.type = codeFieldFrame != null ? Image.Type.Sliced : offlineButton.image.type;
        var input = root.GetComponent<TMP_InputField>();
        input.targetGraphic = image;
        ApplyControlFrame(input, image, codeFieldFrame);
        input.characterLimit = 64;
        input.lineType = TMP_InputField.LineType.SingleLine;
        var label = Instantiate(offlineButton.GetComponentInChildren<TextMeshProUGUI>(), root.transform);
        label.text = string.Empty;
        label.richText = false;
        label.raycastTarget = false;
        label.enableAutoSizing = false;
        label.fontSize = 32;
        label.alignment = TextAlignmentOptions.MidlineLeft;
        var labelRect = label.rectTransform;
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(28, 8);
        labelRect.offsetMax = new Vector2(-28, -8);
        var placeholder = Instantiate(label, root.transform);
        placeholder.text = "방 코드 입력";
        placeholder.alpha = 0.65f;
        input.textComponent = label;
        input.textViewport = labelRect;
        input.placeholder = placeholder;
        input.onValueChanged.AddListener(HandleCodeChanged);
        return input;
    }

    private void HandleCodeChanged(string value)
    {
        if (submitCodeButton == null) return;
        bool pending = flow != null && flow.IsBusy || SteamLobbyService.Current?.IsLobbyRequestPending == true;
        submitCodeButton.interactable = !pending && !string.IsNullOrWhiteSpace(value);
    }

    private Button AddSavedMapControl(string label, float x, UnityEngine.Events.UnityAction click)
    {
        var button = Instantiate(offlineButton, offlineButton.transform.parent);
        button.name = label;
        var rect = (RectTransform)button.transform;
        rect.anchoredPosition = new Vector2(x, -45);
        rect.sizeDelta = new Vector2(370, 54);
        var text = button.GetComponentInChildren<TMP_Text>();
        text.rectTransform.sizeDelta = new Vector2(350, 54);
        text.enableAutoSizing = true;
        text.richText = false;
        text.text = label;
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
        officialStageButton = Instantiate(ropeMapButton, ropeMapButton.transform.parent);
        officialStageButton.name = "공식 스테이지 선택";
        officialStageButton.onClick.AddListener(SelectNextOfficialStage);
        GetComponent<ButtonSelectController>().tagetButtonList.Insert(6, officialStageButton);
        var choices = new[] { staticMapButton, ropeMapButton, interactionMapButton, respawnMapButton, deviceMapButton, tilemapButton, officialStageButton };
        for (int i = 0; i < choices.Length; i++)
        {
            var rect = (RectTransform)choices[i].transform;
            rect.anchoredPosition = new Vector2((i - 3f) * OfficialRowSpacing, rect.anchoredPosition.y);
            rect.sizeDelta = new Vector2(OfficialRowSpacing - 10, rect.sizeDelta.y);
            var label = choices[i].GetComponentInChildren<TMP_Text>();
            ((RectTransform)label.transform).sizeDelta = new Vector2(OfficialRowSpacing - 20, 70); label.enableAutoSizing = true;
        }
    }

    private const float OfficialRowSpacing = 182;

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
            var label = button.GetComponentInChildren<TMP_Text>();
            label.enableAutoSizing = true;
            label.rectTransform.sizeDelta = new Vector2(width - (button == hostLobbyButton ? 112 : 20), height - 12);
            label.rectTransform.anchoredPosition = button == hostLobbyButton ? new Vector2(38, 0) : Vector2.zero;
            if (button == cancelButton) label.rectTransform.anchoredPosition = new Vector2(12, 0);
            if (button == inviteButton && inviteSprite != null)
            {
                label.rectTransform.anchoredPosition = new Vector2(width * 0.15f, 0);
                label.rectTransform.sizeDelta = new Vector2(width * 0.62f, height - 12);
            }
            UpdateButtonIcon(button, width);
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
            control.GetComponentInChildren<TMP_Text>().rectTransform.sizeDelta = layout.LabelSize;
            navigation.Add(control);
        }
        foreach (var button in new[] { hostLobbyButton, submitCodeButton, inviteButton, joinInviteButton })
            button.gameObject.SetActive(false);
        codeInput.gameObject.SetActive(false);
        lobbyHeader.gameObject.SetActive(false);
        lobbyNotice.gameObject.SetActive(false);
        status.rectTransform.anchoredPosition = new Vector2(0, 100);
        status.rectTransform.sizeDelta = new Vector2(1150, 145);
        status.enableAutoSizing = false;
        status.fontSize = 28;
        if (title != null) title.text = offlineTitle;
        SetLabel(cancelButton, "뒤로");
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

    private void UpdateButtonIcon(Button button, float width)
    {
        Transform iconTransform = button.transform.Find("도트 아이콘");
        if (iconTransform == null) return;
        RectTransform rect = (RectTransform)iconTransform;
        float size = rect.sizeDelta.x;
        rect.anchoredPosition = new Vector2(-width * 0.5f + size * 0.8f, 0);
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
        SetLabel(legacyButton, "기존 Steam 게임 (Mirror)");
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
        legacyButton.interactable = participantButton.interactable = !isBusy;
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
            officialStageButton.GetComponentInChildren<TMP_Text>().text = "공식 스테이지\n" + (isOfficial ? flow.SelectedContent.DisplayName : $"{officialStages.Count}개");
        }
        cancelButton.interactable = flow.State != QuantumGameState.Stopping;
        EnsureSelection();
        status.text = flow.Message + "\n선택 맵: " + (flow.HasRejectedSavedMap ? "선택할 수 없음" : isSavedMap ? flow.SavedMapName : flow.SelectedMap == 0 ? "정적 지형" : flow.SelectedMap == 1 ? "로프" : flow.SelectedMap == 2 ? "밟기/운반/상자" : flow.SelectedMap == 3 ? "리셋/추락 복귀" : flow.SelectedMap == 4 ? "체크포인트/구간/소환" : isOfficial ? "공식 스테이지 " + flow.SelectedContent.DisplayName : "Tilemap 사용례 (전체 스테이지 아님)") +
            $"\n최소 {minimum}명 / 최대 {QuantumGameContent.MaxPlayers}명 / 연결된 게임패드 {Gamepad.all.Count}개" +
            (!hasMinimum ? "\n최소 인원에 도달해야 시작할 수 있습니다." : !flow.HasLocalDevices ? "\n오프라인 플레이에 필요한 입력 장치를 연결하세요." : "\n배정을 확인하고 플레이를 시작하세요.");
    }

    private static void SetLabel(Button button, string value) => button.GetComponentInChildren<TMP_Text>().text = value;

    private void OnDestroy()
    {
        OnHide();
        if (inviteSprite != null) Destroy(inviteSprite);
    }
}
