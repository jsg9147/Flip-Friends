using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class PrivateJoinScreen : UIScreen
{
    [Header("Navigation")]
    [SerializeField] private ScreenNavigator navigator;

    [Header("UI References")]
    [SerializeField] private TMP_InputField joinCodeInput;
    [SerializeField] private Button joinButton;
    [SerializeField] private Button backButton;

    [Header("Legacy Fallback")]
    [SerializeField] private GameObject legacyDigitInputRoot;
    [SerializeField] private GameObject legacyInputButton;
    [SerializeField] private DigitControl[] legacyDigitControls;
    [SerializeField] private float legacyInputDelay = 0.2f;

    [Header("Input Settings")]
    [SerializeField] private int requiredCodeLength = 8;

    private ButtonSelectController selectController;
    private bool legacyInputMode;
    private float lastLegacyInputTime;

    protected override void Awake()
    {
        base.Awake();

        if (navigator == null)
        {
            navigator = FindFirstObjectByType<ScreenNavigator>(FindObjectsInactive.Include);
        }

        if (joinCodeInput != null)
        {
            joinCodeInput.characterLimit = requiredCodeLength;
            joinCodeInput.onValueChanged.AddListener(HandleJoinCodeChanged);
        }

        if (joinButton != null)
        {
            joinButton.onClick.AddListener(JoinLobby);
        }

        if (backButton != null)
        {
            backButton.onClick.AddListener(HandleBack);
        }

        selectController = GetComponent<ButtonSelectController>();

        ApplyInputModeVisibility();
        RefreshJoinButtonState();
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
        if (!IsVisible || joinCodeInput != null)
        {
            return;
        }

        HandleLegacyInput();
        EnsureLegacySelection();
    }

    protected override void OnShow()
    {
        RefreshJoinButtonState();

        if (joinCodeInput != null)
        {
            joinCodeInput.text = string.Empty;
            joinCodeInput.Select();
            joinCodeInput.ActivateInputField();
        }

        legacyInputMode = false;
    }

    public void JoinLobby()
    {
        if (SteamRoomManager.Instance == null)
        {
            Debug.LogWarning("PrivateJoinScreen could not find SteamRoomManager.");
            return;
        }

        string joinCode = GetJoinCode();
        if (string.IsNullOrWhiteSpace(joinCode))
        {
            return;
        }

        SteamRoomManager.Instance.JoinPrivateLobby(joinCode);
    }

    public void JoinSteamLobby()
    {
        JoinLobby();
    }

    public void HandleBack()
    {
        if (legacyInputMode)
        {
            InputComplete();
            return;
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

    public void CodeInputBtn()
    {
        if (joinCodeInput != null)
        {
            joinCodeInput.Select();
            joinCodeInput.ActivateInputField();
            return;
        }

        if (legacyDigitControls == null || legacyDigitControls.Length == 0)
        {
            return;
        }

        if (Time.unscaledTime - lastLegacyInputTime < legacyInputDelay)
        {
            return;
        }

        legacyInputMode = true;

        if (selectController != null)
        {
            selectController.enabled = false;
        }

        legacyDigitControls[0].SelectEvent();
        lastLegacyInputTime = Time.unscaledTime;
    }

    public void InputComplete()
    {
        if (!legacyInputMode)
        {
            return;
        }

        legacyInputMode = false;
        lastLegacyInputTime = Time.unscaledTime;

        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }

        if (selectController != null)
        {
            selectController.enabled = true;
            selectController.SelectFirstBtn();
        }

        RefreshJoinButtonState();
    }

    private void HandleJoinCodeChanged(string value)
    {
        if (joinCodeInput == null)
        {
            return;
        }

        string digitsOnly = new string(value.Where(char.IsDigit).ToArray());
        if (digitsOnly != value)
        {
            joinCodeInput.SetTextWithoutNotify(digitsOnly);
        }

        RefreshJoinButtonState();
    }

    private string GetJoinCode()
    {
        if (joinCodeInput != null)
        {
            string sanitized = new string(joinCodeInput.text.Where(char.IsDigit).ToArray());
            return sanitized;
        }

        if (legacyDigitControls == null || legacyDigitControls.Length == 0)
        {
            return string.Empty;
        }

        return string.Concat(legacyDigitControls.Select(control => control != null ? control.CurrentDigit.ToString() : "0"));
    }

    private void EnsureLegacySelection()
    {
        if (!legacyInputMode || EventSystem.current == null || legacyDigitControls == null || legacyDigitControls.Length == 0)
        {
            return;
        }

        if (EventSystem.current.currentSelectedGameObject == null)
        {
            legacyDigitControls[0].SelectEvent();
        }
    }

    private void HandleLegacyInput()
    {
        if (!legacyInputMode || InputManager.instance == null || legacyDigitControls == null)
        {
            return;
        }

        GameObject currentSelected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        if (currentSelected == null || Time.unscaledTime - lastLegacyInputTime < legacyInputDelay)
        {
            return;
        }

        for (int index = 0; index < legacyDigitControls.Length; index++)
        {
            DigitControl control = legacyDigitControls[index];
            if (control == null || control.boder == null || control.boder.gameObject != currentSelected)
            {
                continue;
            }

            if (InputManager.instance.dir.y > 0f)
            {
                SetLegacyDigit(index, control.CurrentDigit + 1);
                lastLegacyInputTime = Time.unscaledTime;
                break;
            }

            if (InputManager.instance.dir.y < 0f)
            {
                SetLegacyDigit(index, control.CurrentDigit - 1);
                lastLegacyInputTime = Time.unscaledTime;
                break;
            }

            if (InputManager.instance.dir.x > 0f)
            {
                SelectLegacyDigit(index + 1);
                lastLegacyInputTime = Time.unscaledTime;
                break;
            }

            if (InputManager.instance.dir.x < 0f)
            {
                SelectLegacyDigit(index - 1);
                lastLegacyInputTime = Time.unscaledTime;
                break;
            }
        }
    }

    private void SetLegacyDigit(int index, int value)
    {
        if (legacyDigitControls == null || index < 0 || index >= legacyDigitControls.Length || legacyDigitControls[index] == null)
        {
            return;
        }

        int wrappedValue = (value % 10 + 10) % 10;
        legacyDigitControls[index].SetDigit(wrappedValue);
        RefreshJoinButtonState();
    }

    private void SelectLegacyDigit(int index)
    {
        if (legacyDigitControls == null || legacyDigitControls.Length == 0)
        {
            return;
        }

        int wrappedIndex = (index % legacyDigitControls.Length + legacyDigitControls.Length) % legacyDigitControls.Length;
        DigitControl control = legacyDigitControls[wrappedIndex];
        if (control != null)
        {
            control.SelectEvent();
        }
    }

    private void RefreshJoinButtonState()
    {
        if (joinButton == null)
        {
            return;
        }

        string joinCode = GetJoinCode();
        joinButton.interactable = !string.IsNullOrWhiteSpace(joinCode) && joinCode.Length == requiredCodeLength;
    }

    private void ApplyInputModeVisibility()
    {
        bool hasModernInput = joinCodeInput != null;

        if (legacyDigitInputRoot != null)
        {
            legacyDigitInputRoot.SetActive(!hasModernInput);
        }

        if (legacyInputButton != null)
        {
            bool shouldShowLegacyInputButton = !hasModernInput ||
                (joinCodeInput != null && legacyInputButton == joinCodeInput.gameObject);

            legacyInputButton.SetActive(shouldShowLegacyInputButton);
        }
    }
}
