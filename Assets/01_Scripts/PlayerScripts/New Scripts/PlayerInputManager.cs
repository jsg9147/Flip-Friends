using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInputManager : NetworkBehaviour
{
    public PlayerController2D controller;
    public Vector2 MovementInput { get; private set; }
    public bool IsJumpPressed { get; private set; }
    public bool IsJumpHold { get; private set; }
    public bool IsJumpUp { get; private set; }
    public bool IsRunPressed { get; private set; }
    public bool IsPickUpPressed { get; private set; }
    public bool MovementInputPressed { get; private set; }
    public bool ResetPressed { get; private set; }
    public bool IsNextPressed { get; private set; }
    public bool IsPreviousPressed { get; private set; }

    public void OnMove(InputAction.CallbackContext context)
    {
        if (!isOwned) return;

        MovementInput = context.ReadValue<Vector2>();
        if (context.started)
        {
            MovementInputPressed = true;
        }
        else if (context.performed)
        {
            MovementInputPressed = false;
        }
        else if (context.canceled)
        {
            MovementInputPressed = false;
        }
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
            IsJumpPressed = false;
            IsJumpHold = false;
        }
    }

    public void OnInteract(InputAction.CallbackContext context)
    {
        if (!isOwned) return;

        if (context.started)
        {
            IsPickUpPressed = true;
        }
        else if (context.performed)
        {

        }
        else if (context.canceled)
        {
            IsPickUpPressed = false;
        }
    }

    public void OnRun(InputAction.CallbackContext context)
    {
        if (!isOwned) return;

        if (context.started)
        {
            IsRunPressed = true;
        }
        else if (context.performed)
        {

        }
        else if (context.canceled)
        {
            IsRunPressed = false;
        }
    }

    public void OnReset(InputAction.CallbackContext context)
    {
        if (!isOwned) return;

        if (context.started)
        {
            ResetPressed = true;
        }
        else if (context.performed)
        {
            //ResetPressed = false;
        }
        else if (context.canceled)
        {
            ResetPressed = false;
        }
    }

    public void OnNext(InputAction.CallbackContext context)
    {
        if (context.started)
        {
            IsNextPressed = true;
        }
        else if (context.canceled)
        {
            IsNextPressed = false;
        }
    }

    public void OnPrevious(InputAction.CallbackContext context)
    {
        if (context.started)
        {
            IsPreviousPressed = true;
        }
        else if (context.canceled)
        {
            IsPreviousPressed = false;
        }
    }
}
