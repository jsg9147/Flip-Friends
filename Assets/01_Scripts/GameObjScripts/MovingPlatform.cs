using System.Collections;
using UnityEngine;
using Mirror;

public class MovingPlatform : NetworkBehaviour
{
    public Vector2 startPosition;  // 시작 위치
    public Vector2 endPosition;    // 끝 위치
    public float speed = 2f;       // 이동 속도
    public bool isVertical = false; // true일 경우 위아래로, false일 경우 좌우로 이동

    private bool movingToEnd = true;  // 끝 위치로 이동 중인지 여부

    public override void OnStartServer()
    {
        // 서버에서 시작 위치 설정
        transform.position = startPosition;
    }

    [ServerCallback]
    void Update()
    {
        MovePlatform();
    }

    [Server]
    void MovePlatform()
    {
        Vector2 targetPosition = movingToEnd ? endPosition : startPosition;
        transform.position = Vector2.MoveTowards(transform.position, targetPosition, speed * Time.deltaTime);

        if (Vector2.Distance(transform.position, targetPosition) < 0.1f)
        {
            movingToEnd = !movingToEnd;  // 방향 전환
        }
    }
}
