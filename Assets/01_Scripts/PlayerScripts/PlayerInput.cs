using Mirror;
using UnityEngine;

public class PlayerInput : NetworkBehaviour
{
    public Vector2 MovementInput { get; private set; }
    public bool IsJumpPressed { get; private set; }
    public bool IsJumpHold { get; private set; }
    public bool IsRunPressed { get; private set; }
    public bool IsPickUpPressed { get; private set; }
    public bool IsThrowPressed { get; private set; }

    void Update()
    {
        if (!isLocalPlayer) return;

        // 기본 이동 입력 (좌우 화살표 또는 A, D 키)
        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");

        MovementInput = new Vector2(horizontal, vertical);

        // 점프 입력 (스페이스바)
        IsJumpPressed = Input.GetButtonDown("Jump");

        // 점프 홀드 (스페이스바)
        IsJumpHold = Input.GetButton("Jump");

        // 달리기 입력 (Left Shift)
        IsRunPressed = Input.GetKey(KeyCode.LeftShift);

        // 물건 집기/버리기 입력 (E 키)
        IsPickUpPressed = Input.GetKeyDown(KeyCode.E);

        // 던지기 입력 (Q 키)
        IsThrowPressed = Input.GetKeyDown(KeyCode.Q);
    }
}
