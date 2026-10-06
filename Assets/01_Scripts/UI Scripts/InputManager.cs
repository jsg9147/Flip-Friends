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

    void OnCancel(InputValue value)
    {
        // OnCancelEvent에 구독 중인 함수가 있으면 모두 호출
        OnCancelEvent?.Invoke();
    }

    void OnSubmit(InputValue value)
    {
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
