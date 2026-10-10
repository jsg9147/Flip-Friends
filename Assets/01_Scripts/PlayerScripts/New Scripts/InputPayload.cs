using UnityEngine;

/// <summary>
/// 한 프레임의 입력을 캡슐화하는 구조체.
/// 시퀀스 번호로 서버가 어느 입력에 대한 결과인지 클라이언트에게 알려준다.
/// </summary>
public struct InputPayload
{
    // 서버가 위치를 강제로 정할 때마다 바뀐다. 그 전에 보낸 입력은 서버가 버린다.
    public ushort epoch;
    public uint sequenceNumber;
    public Vector2 movement;
    public bool jump;
    public bool jumpHeld;
    public bool jumpUp;
    public bool run;
    public float deltaTime;
}
