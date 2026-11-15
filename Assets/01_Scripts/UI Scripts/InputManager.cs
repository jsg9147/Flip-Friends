using UnityEngine;
using UnityEngine.InputSystem;
using System;

public class InputManager : MonoBehaviour
{
    public static InputManager instance;
    [SerializeField] private InputActionReference cancelActionReference;

    public Vector2 dir { get; private set; }

    public event Action OnCancelEvent; // OnCancel 이벤트를 구독할 수 있도록 정의
    public event Action OnSubmitEvent; // OnCancel 이벤트를 구독할 수 있도록 정의
    public event Action OnInteractEvent; // OnCancel 이벤트를 구독할 수 있도록 정의
    public event Action OnMenuEvent; // OnCancel 이벤트를 구독할 수 있도록 정의

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
            if (GetComponent<UIInputModeController>() == null)
                gameObject.AddComponent<UIInputModeController>();
        }
        else
        {
            Destroy(gameObject); // 자기 자신을 삭제
            return;
        }
    }

    void OnMove(InputValue value)
    {
        dir = value.Get<Vector2>();
    }

    // PlayerInput(SendMessages)은 켜진 Player·UI 맵의 같은 이름 액션(Cancel·Submit)을 모두 같은 메서드로 보낸다.
    // 한 번 누름에 두 번 처리되면 화면 히스토리가 두 단계 풀리므로 프레임당 한 번만 통과시킨다.
    private int lastCancelFrame = -1;
    private int lastSubmitFrame = -1;

    private static bool IsFirstThisFrame(ref int lastFrame)
    {
        if (lastFrame == Time.frameCount) return false;
        lastFrame = Time.frameCount;
        return true;
    }

    void OnCancel(InputValue value)
    {
        if (!IsFirstThisFrame(ref lastCancelFrame)) return;
        OnCancelEvent?.Invoke();
    }

    void OnSubmit(InputValue value)
    {
        if (!IsFirstThisFrame(ref lastSubmitFrame)) return;
        if (SoundManager.Instance != null)
            SoundManager.Instance.PlayClickSound();

        OnSubmitEvent?.Invoke();
    }

    void OnInteract(InputValue value)
    {
        OnInteractEvent?.Invoke();
    }

    void OnMenu(InputValue value)
    {
        OnMenuEvent?.Invoke();
    }
}
