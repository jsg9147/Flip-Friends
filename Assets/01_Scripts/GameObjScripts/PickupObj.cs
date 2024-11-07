using UnityEngine;
using Mirror;

public class PickupObj : NetworkBehaviour
{
    [SyncVar] private bool isCarried = false;
    public bool IsCarried => isCarried;

    private Transform playerTransform;
    public Rigidbody2D rb { get; private set; }

    private void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
    }

    private void Update()
    {
        if (isServer && !isCarried)
        {
            RpcUpdatePosition(transform.position);
        }
    }

    public void SetPickupState(Transform playerTransform, bool isCarried)
    {
        this.playerTransform = playerTransform;
        this.isCarried = isCarried;

        if (isCarried)
        {
            rb.linearVelocity = Vector2.zero; // 속도 초기화
        }
    }

    public void StateReset()
    {
        this.playerTransform = null;
        this.isCarried = false;
        rb.bodyType = RigidbodyType2D.Dynamic;
    }

    [ClientRpc]
    private void RpcUpdatePosition(Vector3 newPosition)
    {
        transform.position = newPosition;
    }
}
