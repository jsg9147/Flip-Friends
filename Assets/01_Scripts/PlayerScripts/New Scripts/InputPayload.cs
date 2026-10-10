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
    // 이 입력을 만들 때 화면의 원격 플레이어가 서 있던 서버 시각. 서버가 지연 보상으로 그 위치에 두고 판정한다.
    public double viewTime;
}
