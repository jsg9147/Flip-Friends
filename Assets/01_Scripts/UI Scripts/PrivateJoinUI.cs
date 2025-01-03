using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Mirror.BouncyCastle.Asn1.Crmf;

public class PrivateJoinUI : MonoBehaviour
{
    [Header("UI References")] 
    public DigitControl[] digitControls;       // 숫자 제어 UI 배열
    public Button joinButton;                  // 참여 버튼

    [Header("Input Settings")]
    public float inputDelay = 0.2f;            // 입력 딜레이 (초 단위)

    private int[] joinCodeDigits = new int[8]; // 입력된 코드 저장 배열
    private float lastInputTime = 0f;          // 마지막 입력 시간

    private ButtonSelectController selectController; // 선택 제어기
    public bool inputMode = false;            // 입력 모드 활성화 상태

    private void OnEnable()
    {
        // 입력 이벤트 구독
        if (InputManager.instance != null)
        {
            InputManager.instance.OnSubmitEvent += InputComplete;
            InputManager.instance.OnCancelEvent += InputComplete;
        }
    }

    private void OnDisable()
    {
        // 입력 이벤트 구독 해제
        if (InputManager.instance != null)
        {
            InputManager.instance.OnSubmitEvent -= InputComplete;
            InputManager.instance.OnCancelEvent -= InputComplete;
        }
    }

    private void Start()
    {
        selectController = GetComponent<ButtonSelectController>();
        InitializeDigitControls();    // 숫자 컨트롤 초기화
        UpdateJoinButtonState();     // 초기 버튼 상태 설정
    }

    private void Update()
    {
        HandleInput();
        EnsureSelection();
    }

    // ============================
    // 초기화 및 UI 업데이트 관련 메서드
    // ============================

    private void InitializeDigitControls()
    {
        for (int i = 0; i < digitControls.Length; i++)
        {
            int index = i; // 람다 캡처 문제 해결용 로컬 변수
            joinCodeDigits[i] = 0;
            digitControls[i].SetDigit(0);

            if (digitControls[i].upButton != null)
                digitControls[i].upButton.onClick.AddListener(() => IncrementDigit(index));

            if (digitControls[i].downButton != null)
                digitControls[i].downButton.onClick.AddListener(() => DecrementDigit(index));
        }
    }

    private void UpdateJoinButtonState()
    {
        // 모든 숫자가 유효한 경우 버튼 활성화
        joinButton.interactable = true;
    }

    private void EnsureSelection()
    {
        // 입력 모드에서 선택이 해제된 경우 첫 번째 자리 선택
        if (inputMode && EventSystem.current.currentSelectedGameObject == null)
        {
            digitControls[0].SelectEvent();
        }
        if(!inputMode && EventSystem.current.currentSelectedGameObject == null)
        {
            for(int i = 0; i < digitControls.Length;i++)
            {
                if (digitControls[i].boder == EventSystem.current.currentSelectedGameObject)
                {
                    EventSystem.current.SetSelectedGameObject(null);
                    print("Chcek");
                }
            }
        }
    }

    // ============================
    // 입력 처리 관련 메서드
    // ============================

    private void HandleInput()
    {
        GameObject currentSelected = EventSystem.current.currentSelectedGameObject;

        if (currentSelected != null)
        {
            for (int i = 0; i < digitControls.Length; i++)
            {
                if (digitControls[i].boder.gameObject == currentSelected)
                {
                    HandleVerticalInput(i);
                    HandleHorizontalInput(i);
                }
            }
        }
    }

    private void HandleVerticalInput(int index)
    {
        if (Time.time - lastInputTime > inputDelay)
        {
            if (InputManager.instance.dir.y > 0)
            {
                IncrementDigit(index);
                lastInputTime = Time.time;
            }
            else if (InputManager.instance.dir.y < 0)
            {
                DecrementDigit(index);
                lastInputTime = Time.time;
            }
        }
    }

    private void HandleHorizontalInput(int index)
    {
        if (Time.time - lastInputTime > inputDelay)
        {
            if (InputManager.instance.dir.x < 0)
            {
                SelectPreviousDigit(index);
                lastInputTime = Time.time;
            }
            else if (InputManager.instance.dir.x > 0)
            {
                SelectNextDigit(index);
                lastInputTime = Time.time;
            }
        }
    }

    private void SelectPreviousDigit(int index)
    {
        if (index > 0)
            digitControls[index - 1].SelectEvent();
        else
            digitControls[digitControls.Length - 1].SelectEvent();
    }

    private void SelectNextDigit(int index)
    {
        if (index < digitControls.Length - 1)
            digitControls[index + 1].SelectEvent();
        else
            digitControls[0].SelectEvent();
    }

    private void IncrementDigit(int index)
    {
        joinCodeDigits[index] = (joinCodeDigits[index] + 1) % 10; // 0-9 순환
        digitControls[index].SetDigit(joinCodeDigits[index]);
        UpdateJoinButtonState();
    }

    private void DecrementDigit(int index)
    {
        joinCodeDigits[index] = (joinCodeDigits[index] - 1 + 10) % 10; // 0-9 순환
        digitControls[index].SetDigit(joinCodeDigits[index]);
        UpdateJoinButtonState();
    }

    // ============================
    // 버튼 동작 관련 메서드
    // ============================

    public void JoinSteamLobby()
    {
        string joinCode = string.Join("", joinCodeDigits);
        SteamRoomManager.Instance.JoinPrivateLobby(joinCode);
    }

    public void CodeInputBtn()
    {
        if (Time.time - lastInputTime > inputDelay)
        {
            selectController.enabled = false;
            inputMode = true;
            digitControls[0].SelectEvent();
        }
    }

    public void InputComplete()
    {
        if (inputMode)
        {
            inputMode = false;
            EventSystem.current.SetSelectedGameObject(null);
            selectController.enabled = true;
            selectController.SelectFirstBtn();

            lastInputTime = Time.time;
        }
    }
}
