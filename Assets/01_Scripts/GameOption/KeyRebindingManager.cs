using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;
using System.Linq;

public class KeyRebindingManager : MonoBehaviour
{
    public InputActionAsset inputActions; // Input Action Asset
    public KeyBindItem rebindUIPrefab;     

    public ButtonSelectController keyboardRebindUIContainer;  // 리바인딩 UI 컨테이너
    public GameObject keyboardContent;

    public ButtonSelectController gamepadRebindUIContainer;  // 리바인딩 게임 패드 UI 컨테이너
    public GameObject gamepadContent;

    private readonly Dictionary<Guid, KeyBindItem> bindingItems = new Dictionary<Guid, KeyBindItem>();

    private Gamepad gamepad;
    private Dictionary<string, InputAction> _actions = new Dictionary<string, InputAction>();
    private InputActionRebindingExtensions.RebindingOperation _rebindingOperation;

    private void Start()
    {
        StyleScrollView(keyboardRebindUIContainer);
        StyleScrollView(gamepadRebindUIContainer);

        // Input Actions 초기화
        foreach (var map in inputActions.actionMaps)
        {
            if(map.name == "Player")
            {
                foreach (var action in map.actions)
                {
                    _actions[action.name] = action;
                    // UI 생성
                    CreateRebindUI(action);
                }
            }
        }

        // 저장된 키 설정 불러오기
        LoadBindings();
    }

    private void StyleScrollView(ButtonSelectController container)
    {
        ScrollRect scrollRect = container.GetComponentInChildren<ScrollRect>(true);
        if (scrollRect == null) scrollRect = container.GetComponent<ScrollRect>();
        if (scrollRect == null || scrollRect.verticalScrollbar == null) return;

        Scrollbar scrollbar = scrollRect.verticalScrollbar;
        Image track = scrollbar.GetComponent<Image>();
        if (track != null)
        {
            track.sprite = null;
            track.type = Image.Type.Simple;
            track.color = new Color32(206, 234, 239, 255);
        }

        if (scrollbar.targetGraphic is Image handle)
        {
            handle.sprite = null;
            handle.type = Image.Type.Simple;
            handle.color = Color.white;
        }

        ColorBlock colors = scrollbar.colors;
        colors.normalColor = new Color32(49, 164, 186, 255);
        colors.highlightedColor = new Color32(146, 230, 101, 255);
        colors.selectedColor = colors.highlightedColor;
        colors.pressedColor = new Color32(75, 177, 198, 255);
        colors.fadeDuration = 0f;
        scrollbar.colors = colors;
        scrollbar.GetComponent<RectTransform>().SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 24f);
    }
    private void CreateRebindUI(InputAction action)
    {
        foreach (var binding in action.bindings)
        {
            ScrollViewController container = keyboardRebindUIContainer.GetComponent<ScrollViewController>();

            if (binding.groups.Contains("Gamepad") || binding.path.StartsWith("<Gamepad>/", StringComparison.Ordinal))
                container = gamepadRebindUIContainer.GetComponent<ScrollViewController>();
            else if (binding.groups.Contains("Keyboard"))
            {
                container = keyboardRebindUIContainer.GetComponent<ScrollViewController>();

            }

            if (binding.isComposite)
            {
                // Composite binding 처리
                foreach (var part in action.bindings)
                {
                    // isPartOfComposite를 확인하고, 현재 binding에 속하는지 체크
                    if (part.isPartOfComposite && part.action == binding.action)
                    {
                        CreateCompositePartUI(action, part, container);
                    }
                }
            }
            else if (!binding.isPartOfComposite)
            {
                CreateSingleBindingUI(action, binding, container);
            }
        }
    }

    private void CreateSingleBindingUI(InputAction action, InputBinding binding, ScrollViewController container)
    {
        KeyBindItem uiInstance = Instantiate(rebindUIPrefab, container.Content);
        container.GetComponent<ButtonSelectController>().tagetButtonList.Add(uiInstance.keyBindBtn);

        uiInstance.antionNameText.text = GetActionDisplayName(action.name);
        bindingItems[binding.id] = uiInstance;
        UpdateBindingText(action, uiInstance.keyBindText, binding);

        // LINQ로 인덱스 찾기
        int bindingIndex = action.bindings.ToList().FindIndex(b => b == binding);
        if (bindingIndex >= 0)
        {
            uiInstance.keyBindBtn.onClick.AddListener(() => StartRebinding(action, uiInstance.keyBindText, bindingIndex));
        }
    }

    private void CreateCompositePartUI(InputAction action, InputBinding partBinding, ScrollViewController container)
    {
        var uiInstance = Instantiate(rebindUIPrefab, container.Content);
        var keyBindItem = uiInstance.GetComponent<KeyBindItem>();
        container.GetComponent<ButtonSelectController>().tagetButtonList.Add(uiInstance.keyBindBtn);

        keyBindItem.antionNameText.text = GetActionDisplayName(action.name) + " " + GetDirectionDisplayName(partBinding.name);
        bindingItems[partBinding.id] = keyBindItem;
        UpdateBindingText(action, keyBindItem.keyBindText, partBinding);

        // LINQ로 인덱스 찾기
        int partBindingIndex = action.bindings.ToList().FindIndex(b => b == partBinding);
        if (partBindingIndex >= 0)
        {
            keyBindItem.keyBindBtn.onClick.AddListener(() => StartRebinding(action, keyBindItem.keyBindText, partBindingIndex));
        }
    }


    private void StartRebinding(InputAction action, TMP_Text bindingText, int bindingIndex)
    {
        action.Disable();

        bindingText.text = "Press a control...";
        foreach (Transform child in keyboardRebindUIContainer.transform)
        {
            var keyBindItem = child.GetComponent<KeyBindItem>();
            if (keyBindItem != null)
            {
                keyBindItem.keyBindBtn.interactable = false;
            }
        }

        foreach (Transform child in gamepadRebindUIContainer.transform)
        {
            var keyBindItem = child.GetComponent<KeyBindItem>();
            if (keyBindItem != null)
            {
                keyBindItem.keyBindBtn.interactable = false;
            }
        }

        _rebindingOperation = action.PerformInteractiveRebinding(bindingIndex)
            .WithControlsExcluding("Mouse")
            .OnComplete(operation => RebindingComplete(action, bindingText, bindingIndex))
            .OnCancel(operation => RebindingCancel(action, bindingText, bindingIndex))
            .Start();
    }

    private void RebindingComplete(InputAction action, TMP_Text bindingText, int bindingIndex)
    {
        _rebindingOperation.Dispose();
        action.Enable();

        SaveBindings();
        UpdateBindingText(action, bindingText, action.bindings[bindingIndex]);

        foreach (Transform child in keyboardRebindUIContainer.transform)
        {
            var keyBindItem = child.GetComponent<KeyBindItem>();
            if (keyBindItem != null)
            {
                keyBindItem.keyBindBtn.interactable = true;
            }
        }

        foreach (Transform child in gamepadRebindUIContainer.transform)
        {
            var keyBindItem = child.GetComponent<KeyBindItem>();
            if (keyBindItem != null)
            {
                keyBindItem.keyBindBtn.interactable = true;
            }
        }
    }

    private void RebindingCancel(InputAction action, TMP_Text bindingText, int bindingIndex)
    {
        _rebindingOperation.Dispose();
        action.Enable();

        UpdateBindingText(action, bindingText, action.bindings[bindingIndex]);

        foreach (Transform child in keyboardRebindUIContainer.transform)
        {
            var keyBindItem = child.GetComponent<KeyBindItem>();
            if (keyBindItem != null)
            {
                keyBindItem.keyBindBtn.interactable = true;
            }
        }
    }

    private void UpdateBindingText(InputAction action, TMP_Text bindingText, InputBinding binding)
    {
        if (binding.isComposite)
        {
            bindingText.text = "Composite Binding";
        }
        else
        {
            bindingText.text = InputControlPath.ToHumanReadableString(
                binding.effectivePath,
                InputControlPath.HumanReadableStringOptions.OmitDevice
            );
        }
    }
    private void UpdateBindingText(InputAction action, TMP_Text bindingText)
    {
        if (action == null || action.bindings.Count == 0)
        {
            bindingText.text = "Unbound";
            return;
        }

        string bindingDisplay = "";
        foreach (var binding in action.bindings)
        {
            if (binding.isComposite)
            {
                // Composite binding의 각 구성 요소 출력
                bindingDisplay += $"{binding.name}:\n";
                foreach (var part in action.bindings)
                {
                    if (part.isPartOfComposite && part.groups == binding.groups)
                    {
                        bindingDisplay += $"- {part.name}: {InputControlPath.ToHumanReadableString(part.effectivePath, InputControlPath.HumanReadableStringOptions.OmitDevice)}\n";
                    }
                }
            }
            else if (!binding.isPartOfComposite)
            {
                // 단일 입력
                bindingDisplay += InputControlPath.ToHumanReadableString(binding.effectivePath, InputControlPath.HumanReadableStringOptions.OmitDevice);
                break; // 단일 입력은 첫 번째만 표시
            }
        }

        bindingText.text = string.IsNullOrEmpty(bindingDisplay) ? "Unbound" : bindingDisplay.TrimEnd('\n');
    }

    public void SaveBindings()
    {
        try
        {
            string json = inputActions.SaveBindingOverridesAsJson();
            PlayerPrefs.SetString("bindings", json);
            PlayerPrefs.Save();
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to save bindings: {ex.Message}");
        }
    }

    public void LoadBindings()
    {
        if (PlayerPrefs.HasKey("bindings"))
        {
            string json = PlayerPrefs.GetString("bindings");
            try
            {
                inputActions.LoadBindingOverridesFromJson(json);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Failed to load bindings: {ex.Message}");
                ResetBindings(); // JSON 불일치 시 초기화
            }
        }
        else
        {
            Debug.Log("No saved bindings found, using default bindings.");
        }

        // 각 액션의 UI 텍스트 업데이트
        foreach (var action in _actions.Values)
        {
            foreach (var binding in action.bindings)
            {
                if (binding.isComposite)
                {
                    UpdateCompositeBindingUI(action, binding);
                }
                else
                {
                    UpdateSingleBindingUI(action, binding);
                }
            }
        }
    }
    private static string GetActionDisplayName(string actionName)
    {
        switch (actionName)
        {
            case "Submit": return "Confirm";
            case "Cancel": return "Back";
            case "Interact": return "Pick Up / Throw";
            case "Previous": return "Previous Camera";
            case "Next": return "Next Camera";
            case "Sprint": return "Run";
            case "Reset": return "Respawn";
            default: return actionName;
        }
    }

    private static string GetDirectionDisplayName(string direction)
    {
        switch (direction)
        {
            case "up": return "Up";
            case "down": return "Down";
            case "left": return "Left";
            case "right": return "Right";
            default: return direction;
        }
    }

    private void UpdateCompositeBindingUI(InputAction action, InputBinding binding)
    {
        foreach (var partBinding in action.bindings)
        {
            if (partBinding.isPartOfComposite && partBinding.action == binding.action)
                UpdateSingleBindingUI(action, partBinding);
        }
    }
    private void UpdateSingleBindingUI(InputAction action, InputBinding binding)
    {
        // 표시명이 같아도 각 바인딩의 저장값을 해당 행에 반영한다.
        if (bindingItems.TryGetValue(binding.id, out var item))
            UpdateBindingText(action, item.keyBindText, binding);
    }
    public void ResetBindings()
    {
        inputActions.RemoveAllBindingOverrides(); // 모든 바인딩 초기화
        PlayerPrefs.DeleteKey("bindings");        // 저장된 키 삭제
        PlayerPrefs.Save();

        foreach (var action in _actions.Values)
        {
            foreach (var binding in action.bindings)
            {
                if (binding.isComposite)
                {
                    UpdateCompositeBindingUI(action, binding);
                }
                else
                {
                    UpdateSingleBindingUI(action, binding);
                }
            }
        }
    }

}
