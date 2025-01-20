using System.Collections;
using UnityEngine;
using Mirror;

public class MovingPlatform : NetworkBehaviour
{
    public Vector2 endPosition;    // 끝 위치
    public float speed = 2f;       // 이동 속도
    public bool isRotate = false;  // 회전 여부
    public float rotationSpeed = 180f; // 회전 속도 (도/초)

    private Vector2 startPosition;  // 시작 위치
    private bool movingToEnd = true;  // 끝 위치로 이동 중인지 여부
    private Vector2 endPositionToWorld;

    private void Start()
    {
        startPosition = transform.position;
        endPositionToWorld = (Vector2)transform.position + endPosition;
    }

    [ServerCallback]
    void FixedUpdate()
    {
        MovePlatform();
        RpcRotatePlatform();
        RpcUpdatePosition(transform.position);
    }
    [ClientRpc] // 모든 클라이언트에서 실행되는 함수
    void RpcUpdatePosition(Vector3 newPosition)
    {
        transform.position = newPosition;
    }

    [Server]
    void MovePlatform()
    {
        Vector2 targetPosition = movingToEnd ? endPositionToWorld : startPosition;
        transform.position = Vector2.MoveTowards(transform.position, targetPosition, speed * Time.deltaTime);

        if (Vector2.Distance(transform.position, targetPosition) < 0.1f)
        {
            movingToEnd = !movingToEnd;  // 방향 전환
        }
    }

    [ClientRpc]
    void RpcRotatePlatform()
    {
        if(isRotate)
            transform.Rotate(Vector3.forward, rotationSpeed * Time.deltaTime);
    }

    // 씬 화면에 끝 지점을 나타내기 위해 Gizmos 사용
    private void OnDrawGizmos()
    {
        // 시작 위치 설정
        Vector2 globalStartPosition = Application.isPlaying ? startPosition : (Vector2)transform.position;
        Vector2 globalEndPosition = Application.isPlaying ? endPositionToWorld : (Vector2)transform.position + endPosition;

        // 시작 지점과 끝 지점을 연결하는 선을 그림
        Gizmos.color = Color.green;
        Gizmos.DrawLine(globalStartPosition, globalEndPosition);

        // 끝 위치를 작은 구체로 표시
        Gizmos.color = Color.red;
        Gizmos.DrawSphere(globalEndPosition, 0.1f);
    }
}
