using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInputManager : NetworkBehaviour
{
    public PlayerController2D controller;
    public Vector2 MovementInput { get; private set; }

    // 이벤트성 플래그 — 누른 순간 / 뗀 순간만 true, LateUpdate에서 자동 리셋
    public bool IsJumpPressed { get; private set; }
    public bool IsJumpUp { get; private set; }

    // 상태성 플래그 — 누르는 동안 계속 true
    public bool IsJumpHold { get; private set; }
    public bool IsRunPressed { get; private set; }
    public bool IsPickUpPressed { get; private set; }
    public bool ResetPressed { get; private set; }
    public bool IsNextPressed { get; private set; }
    public bool IsPreviousPressed { get; private set; }

    // ClientMover.FixedUpdate가 읽은 직후 호출 — LateUpdate 리셋은 FixedUpdate보다 늦게 실행되므로
    // 입력을 항상 놓치는 문제를 방지하기 위해 소비 시점에 직접 클리어
    public void ClearJumpOneShots()
    {
        IsJumpPressed = false;
        IsJumpUp = false;
    }

    public void OnMove(InputAction.CallbackContext context)
    {
        if (!isOwned) return;
        MovementInput = context.ReadValue<Vector2>();
    }

    public void OnJump(InputAction.CallbackContext context)
    {
        if (!isOwned) return;

        if (context.started)
        {
            IsJumpPressed = true;
        }
        else if (context.performed)
        {
            IsJumpHold = true;
        }
        else if (context.canceled)
        {
            IsJumpHold = false;
            IsJumpUp = true;
        }
    }

    public void OnInteract(InputAction.CallbackContext context)
    {
        if (!isOwned) return;

        if (context.started)
            IsPickUpPressed = true;
        else if (context.canceled)
            IsPickUpPressed = false;
    }

    public void OnRun(InputAction.CallbackContext context)
    {
        if (!isOwned) return;

        if (context.started)
            IsRunPressed = true;
        else if (context.canceled)
            IsRunPressed = false;
    }

    public void OnReset(InputAction.CallbackContext context)
    {
        if (!isOwned) return;

        if (context.started)
            ResetPressed = true;
        else if (context.canceled)
            ResetPressed = false;
    }

    public void OnNext(InputAction.CallbackContext context)
    {
        if (context.started)
            IsNextPressed = true;
        else if (context.canceled)
            IsNextPressed = false;
    }

    public void OnPrevious(InputAction.CallbackContext context)
    {
        if (context.started)
            IsPreviousPressed = true;
        else if (context.canceled)
            IsPreviousPressed = false;
    }
}
