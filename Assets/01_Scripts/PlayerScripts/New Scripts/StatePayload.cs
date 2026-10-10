using UnityEngine;

/// <summary>
/// 특정 시점의 시뮬레이션 상태를 담는 구조체.
/// 서버가 클라이언트에게 보정값을 전송할 때 사용한다.
/// 재시뮬레이션 결과가 서버와 같으려면 다음 틱에 영향을 주는 값을 빠짐없이 담아야 한다.
/// </summary>
public struct StatePayload
{
    public ushort epoch;
    public uint sequenceNumber;
    public Vector2 position;
    public Vector2 velocity;
    public Vector2 externalVelocity;
    public float velocityXSmoothing;
    public bool isGrounded;
    public bool isClimbed;
    public sbyte faceDir;
    public float coyoteTime;
    public float jumpBlockTime;
    public float climbBlockTime;
    public float invincibleTime;
    public float uncontrollableTime;
}
