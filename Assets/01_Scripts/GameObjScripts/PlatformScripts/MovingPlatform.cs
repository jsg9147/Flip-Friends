using UnityEngine;
using Mirror;

public class MovingPlatform : NetworkBehaviour
{
    public Vector2 endPosition;
    public float speed = 2f;
    public bool isRotate = false;
    public float rotationSpeed = 180f;

    [SyncVar] private Vector2 syncedPosition;

    private Vector2 startPosition;
    private bool movingToEnd = true;
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
        syncedPosition = transform.position;
    }

    private void Update()
    {
        if (!isServer)
            transform.position = syncedPosition;

        if (isRotate)
            transform.Rotate(Vector3.forward, rotationSpeed * Time.deltaTime);
    }

    [Server]
    void MovePlatform()
    {
        Vector2 targetPosition = movingToEnd ? endPositionToWorld : startPosition;
        transform.position = Vector2.MoveTowards(transform.position, targetPosition, speed * Time.deltaTime);

        if (Vector2.Distance(transform.position, targetPosition) < 0.1f)
            movingToEnd = !movingToEnd;
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
