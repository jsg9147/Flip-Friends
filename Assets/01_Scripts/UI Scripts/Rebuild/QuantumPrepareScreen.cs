using System;
using FlipFriends.Network;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class QuantumPrepareScreen : NavigableScreen
{
    [SerializeField] private Button staticMapButton;
    [SerializeField] private Button ropeMapButton;
    [SerializeField] private Button offlineButton;
    [SerializeField] private Button onlineButton;
    [SerializeField] private Button cancelButton;
    [SerializeField] private Button legacyButton;
    [SerializeField] private TMP_Text status;
    private QuantumGameFlow flow;
    private Button interactionMapButton;
    private Button respawnMapButton;
    private Button deviceMapButton;
    private Button tilemapButton;
    private Button officialStageButton;
    private System.Collections.Generic.IReadOnlyList<FlipFriends.Maps.QuantumOfficialStageAsset> officialStages = Array.Empty<FlipFriends.Maps.QuantumOfficialStageAsset>();

    protected override void Awake()
    {
        base.Awake();
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
        staticMapButton.onClick.AddListener(() => flow.SelectMap(0));
        ropeMapButton.onClick.AddListener(() => flow.SelectMap(1));
        offlineButton.onClick.AddListener(() => StartGame(false));
        onlineButton.onClick.AddListener(() => StartGame(true));
        cancelButton.onClick.AddListener(OnCancel);
        legacyButton.onClick.AddListener(() => Navigator.Open("mode-select"));
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
        flow.Changed += Refresh;
        Refresh();
    }

    protected override void OnHide()
    {
        if (flow != null) flow.Changed -= Refresh;
    }

    private async void StartGame(bool isOnline)
    {
        try { await flow.StartAsync(isOnline); }
        catch (Exception error) { Debug.LogException(error); }
    }

    protected override async void OnCancel()
    {
        if (flow == null) return;
        try
        {
            await flow.ReturnAsync("시작을 취소했습니다. 다시 시작할 수 있습니다.");
            if (this != null && IsVisible) NavigateBack();
        }
        catch (Exception error) { Debug.LogException(error); }
    }

    private void Refresh()
    {
        bool isBusy = flow.State == QuantumGameState.Starting || flow.State == QuantumGameState.Stopping;
        offlineButton.interactable = onlineButton.interactable = legacyButton.interactable = !isBusy;
        staticMapButton.interactable = ropeMapButton.interactable = !isBusy;
        if (interactionMapButton != null) interactionMapButton.interactable = !isBusy;
        if (respawnMapButton != null) respawnMapButton.interactable = !isBusy;
        if (deviceMapButton != null) deviceMapButton.interactable = !isBusy;
        if (tilemapButton != null) tilemapButton.interactable = !isBusy;
        bool isOfficial = flow.SelectedMap >= flow.SupportedMaps.Length;
        if (officialStageButton != null)
        {
            officialStageButton.interactable = !isBusy;
            officialStageButton.GetComponentInChildren<TMP_Text>().text = "공식 스테이지\n" + (isOfficial ? flow.SelectedContent.DisplayName : $"{officialStages.Count}개");
        }
        cancelButton.interactable = flow.State != QuantumGameState.Stopping;
        status.text = flow.Message + "\n선택 맵: " + (flow.SelectedMap == 0 ? "정적 지형" : flow.SelectedMap == 1 ? "로프" : flow.SelectedMap == 2 ? "밟기/운반/상자" : flow.SelectedMap == 3 ? "리셋/추락 복귀" : flow.SelectedMap == 4 ? "체크포인트/구간/소환" : isOfficial ? "공식 스테이지 " + flow.SelectedContent.DisplayName : "Tilemap 사용례 (전체 스테이지 아님)") +
            "\n2인 / 이동/로프/운반/상자/체크포인트/구간/소환/도착 지원\n리셋 1P R / 2P Backspace | 전체 스테이지는 미지원";
    }

    private void OnDestroy() { if (flow != null) flow.Changed -= Refresh; }
}
