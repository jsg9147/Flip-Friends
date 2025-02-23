using UnityEngine;
using TMPro;
using UnityEngine.InputSystem;

public class GameRoomUI : MonoBehaviour
{
    private const string lobbyKeyHideStr = "**********";

    public InputActionAsset inputActions; // Input Action Asset

    public TMP_Text lobbyKeyText;
    public TMP_Text lobbyKeyShowInfoText;
    public TMP_Text notReadyTextText;

    private bool hideKey = true;

    private void Start()
    {
        // 기본적으로 키 숨기기
        lobbyKeyText.text = lobbyKeyHideStr;

        // Submit 버튼의 키 이름 업데이트
        UpdateSubmitKeyName();

        // BGM 재생
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlayBGM(1);
        }


    }

    private void ReadyTextUpdate()
    {
        var submitAction = inputActions.FindAction("Submit");

        notReadyTextText.text = $"Press to ";

        if (submitAction != null)
        {
            foreach (var binding in submitAction.bindings)
            {
                // 키보드 또는 컨트롤러 바인딩인지 확인
                if (binding.isComposite || string.IsNullOrEmpty(binding.effectivePath))
                    continue;

                // 바인딩된 키 이름 가져오기
                var keyName = InputControlPath.ToHumanReadableString(
                    binding.effectivePath,
                    InputControlPath.HumanReadableStringOptions.OmitDevice
                );

                // 현재 입력 장치 확인
                var lastUsedDevice = submitAction.activeControl?.device;

                if (lastUsedDevice != null)
                {
                    // 장치 유형에 따라 UI 텍스트 업데이트
                    if (lastUsedDevice is Keyboard)
                    {
                        notReadyTextText.text = $"Press to {keyName}";
                    }
                    else if (lastUsedDevice is Gamepad)
                    {
                        notReadyTextText.text = $"Press to {keyName}";
                    }
                    else
                    {
                        notReadyTextText.text = $"Press to {keyName}";
                    }
                }
                else
                {
                    notReadyTextText.text = $"Press to {keyName}";
                }

                return;
            }
        }
        else
        {
            lobbyKeyShowInfoText.text = "Submit key not found";
        }
    }

    private void UpdateSubmitKeyName()
    {
        var submitAction = inputActions.FindAction("Interact");

        if (submitAction != null)
        {
            foreach (var binding in submitAction.bindings)
            {
                // 키보드 또는 컨트롤러 바인딩인지 확인
                if (binding.isComposite || string.IsNullOrEmpty(binding.effectivePath))
                    continue;

                // 바인딩된 키 이름 가져오기
                var keyName = InputControlPath.ToHumanReadableString(
                    binding.effectivePath,
                    InputControlPath.HumanReadableStringOptions.OmitDevice
                );

                // 현재 입력 장치 확인
                var lastUsedDevice = submitAction.activeControl?.device;

                if (lastUsedDevice != null)
                {
                    // 장치 유형에 따라 UI 텍스트 업데이트
                    if (lastUsedDevice is Keyboard)
                    {
                        lobbyKeyShowInfoText.text = $"Show : {keyName}";
                    }
                    else if (lastUsedDevice is Gamepad)
                    {
                        lobbyKeyShowInfoText.text = $"Show : {keyName}";
                    }
                    else
                    {
                        lobbyKeyShowInfoText.text = $"Show : {keyName}";
                    }
                }
                else
                {
                    lobbyKeyShowInfoText.text = $"Press {keyName} to show the lobby key";
                }

                return;
            }
        }
        else
        {
            lobbyKeyShowInfoText.text = "Interact key not found";
        }
    }



    private void OnEnable()
    {
        if (InputManager.instance != null)
        {
            InputManager.instance.OnInteractEvent += ShowLobbyKey;
            InputManager.instance.OnCancelEvent += ExitRoom;
        }
    }

    private void OnDisable()
    {
        if (InputManager.instance != null)
        {
            InputManager.instance.OnInteractEvent -= ShowLobbyKey;
            InputManager.instance.OnCancelEvent -= ExitRoom;
        }
    }

    public void ShowLobbyKey()
    {
        hideKey = !hideKey;

        if (hideKey)
        {
            lobbyKeyText.text = lobbyKeyHideStr;
        }
        else
        {
            lobbyKeyText.text = SteamRoomManager.Instance?.lobbyKeyStr;
        }
    }

    public void CopyLobbyKey()
    {
        GUIUtility.systemCopyBuffer = SteamRoomManager.Instance?.lobbyKeyStr; // 텍스트를 클립보드에 복사
    }

    public void ExitRoom()
    {
        SteamRoomManager.Instance.LeaveLobby();
    }
}
