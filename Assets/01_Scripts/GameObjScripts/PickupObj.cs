using UnityEngine;
using Mirror;

public class PickupObj : RaycastController
{
    [SyncVar] private bool isCarried = false;
    public bool IsCarried => isCarried;

    private Transform playerTransform;
    public Rigidbody2D rb { get; private set; }

    public override void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        base.Start(); // RaycastController의 Start 호출
    }

    public void SetPickupState(Transform playerTransform, bool isCarried)
    {
        this.playerTransform = playerTransform;
        this.isCarried = isCarried;

        if (isCarried)
        {
            rb.linearVelocity = Vector2.zero; // 속도 초기화
            rb.gravityScale = 0;
        }
    }

    public void StateReset()
    {
        this.playerTransform = null;
        this.isCarried = false;
        rb.gravityScale = 1;
    }
}
