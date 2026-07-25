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

    [Header("Host Settings")]
    [SerializeField] private RoomType roomType = RoomType.Public;
    [SerializeField] private int minPlayerCount = 2;
    [SerializeField] private int maxPlayerLimit = 4;
    [SerializeField] private int maxPlayerCount = 4;
    [SerializeField] private float horizontalInputCooldown = 0.2f;

    private float lastHorizontalInputTime;
    private bool canReadHorizontalInput = true;

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
        if (createButton != null)
        {
            createButton.interactable = true;
        }

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

        if (createButton != null)
        {
            createButton.interactable = false;
        }

        roomManager.HostLobby(roomType, maxPlayerCount);
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
