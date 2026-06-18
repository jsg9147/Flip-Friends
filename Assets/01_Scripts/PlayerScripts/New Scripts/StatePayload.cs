using UnityEngine;

/// <summary>
/// 특정 시점의 물리 상태를 담는 구조체.
/// 서버가 클라이언트에게 보정값을 전송할 때 사용한다.
/// </summary>
public struct StatePayload
{
    public uint sequenceNumber;
    public Vector2 position;
    public Vector2 velocity;
    public bool isGrounded;
}
