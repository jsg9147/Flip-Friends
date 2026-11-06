using Mirror;
using UnityEngine;

[RequireComponent(typeof(NetworkIdentity), typeof(Rigidbody2D))]
public class CannonProjectile : NetworkBehaviour
{
    [SyncVar] private Vector2 launchVelocity;
    [SyncVar] private Vector3 launchPosition;
    [SyncVar] private float maxDistance;
    private Rigidbody2D body;
    private double nextRangeCheck;
    private const double RangeCheckInterval = .1;

    public Vector2 LaunchVelocity => launchVelocity;
    public bool IsOutOfRange => Vector3.Distance(launchPosition, transform.position) > maxDistance;

    private void Awake() => body = GetComponent<Rigidbody2D>();

    // Spawn 전에 초기화하므로 아직 isServer가 설정되지 않았다.
    public void ServerInitialize(Vector2 velocity, float distance)
    {
        if (!NetworkServer.active) return;
        if (body == null) Awake();
        launchVelocity = velocity;
        launchPosition = transform.position;
        maxDistance = distance;
        body.simulated = true;
        body.linearVelocity = launchVelocity;
        nextRangeCheck = NetworkTime.time;
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        if (body == null) Awake();
        body.simulated = true;
        body.linearVelocity = launchVelocity;
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        if (isServer) return;
        if (body == null) Awake();
        body.linearVelocity = Vector2.zero;
        body.simulated = false;
    }

    private void Update()
    {
        if (!isServer || NetworkTime.time < nextRangeCheck) return;
        nextRangeCheck = NetworkTime.time + RangeCheckInterval;
        if (IsOutOfRange) NetworkServer.Destroy(gameObject);
    }
}
