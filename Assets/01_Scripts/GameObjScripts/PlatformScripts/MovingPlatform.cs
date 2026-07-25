using UnityEngine;
using Mirror;

public class MovingPlatform : NetworkBehaviour
{
    [SerializeField] private Vector2 endPosition;
    [SerializeField] private float speed = 2f;
    [SerializeField] private bool isRotate = false;
    [SerializeField] private float rotationSpeed = 180f;
    [SerializeField] private float interpolationSpeed = 15f;
    [SerializeField] private bool carriesPlayers = true;

    [SyncVar] private Vector2 syncedPosition;
    [SyncVar] private float syncedRotation;

    private Vector2 startPosition;
    private bool movingToEnd = true;
    private Vector2 endPositionToWorld;
    private Vector2 platformDelta;
    private Vector2 clientPlatformDelta;

    // Controller2D가 읽는 이동 델타 — carriesPlayers가 꺼져 있으면 Vector2.zero 반환
    public Vector2 PlatformDelta => carriesPlayers
        ? (isServer ? platformDelta : clientPlatformDelta)
        : Vector2.zero;

    private void Start()
    {
        startPosition = transform.position;
        endPositionToWorld = (Vector2)transform.position + endPosition;
    }

    public override void OnStartClient()
    {
        // 게임 도중 접속 시 현재 위치로 즉시 이동 — Lerp 시작점 오류 방지
        if (!isServer)
            transform.position = new Vector3(syncedPosition.x, syncedPosition.y, transform.position.z);
    }

    [ServerCallback]
    private void FixedUpdate()
    {
        Vector2 positionBefore = transform.position;

        MovePlatform();
        if (isRotate)
            RotatePlatform();

        platformDelta = (Vector2)transform.position - positionBefore;
        syncedPosition = transform.position;
        syncedRotation = transform.eulerAngles.z;
    }

    private void Update()
    {
        if (isServer) return;

        ApplyNetworkPosition();
        if (isRotate)
            ApplyNetworkRotation();
    }

    private void ApplyNetworkPosition()
    {
        // Z 좌표 유지하면서 보간 적용
        Vector3 previousPos = transform.position;
        Vector3 target = new Vector3(syncedPosition.x, syncedPosition.y, transform.position.z);
        transform.position = Vector3.Lerp(transform.position, target, interpolationSpeed * Time.deltaTime);
        clientPlatformDelta = (Vector2)transform.position - (Vector2)previousPos;
    }

    private void ApplyNetworkRotation()
    {
        float smoothedZ = Mathf.LerpAngle(transform.eulerAngles.z, syncedRotation, interpolationSpeed * Time.deltaTime);
        transform.rotation = Quaternion.Euler(0f, 0f, smoothedZ);
    }

    [Server]
    private void MovePlatform()
    {
        Vector2 targetPosition = movingToEnd ? endPositionToWorld : startPosition;
        transform.position = Vector2.MoveTowards(transform.position, targetPosition, speed * Time.deltaTime);

        if (Vector2.Distance(transform.position, targetPosition) < 0.1f)
            movingToEnd = !movingToEnd;
    }

    [Server]
    private void RotatePlatform()
    {
        transform.Rotate(Vector3.forward, rotationSpeed * Time.deltaTime);
    }

    private void OnDrawGizmos()
    {
        Vector2 globalStartPosition = Application.isPlaying ? startPosition : (Vector2)transform.position;
        Vector2 globalEndPosition = Application.isPlaying ? endPositionToWorld : (Vector2)transform.position + endPosition;

        Gizmos.color = Color.green;
        Gizmos.DrawLine(globalStartPosition, globalEndPosition);

        Gizmos.color = Color.red;
        Gizmos.DrawSphere(globalEndPosition, 0.1f);
    }
}
