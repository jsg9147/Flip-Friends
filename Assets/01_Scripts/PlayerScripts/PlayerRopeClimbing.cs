using Mirror;
using UnityEngine;

public class PlayerRopeClimbing : NetworkBehaviour
{
    public float climbSpeed = 3.0f;
    public bool isClimbing { get; private set; }
    private Rigidbody2D rb;
    private float originalGravity;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        originalGravity = rb.gravityScale;
        isClimbing = false;
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.gameObject.CompareTag("Rope"))
        {

        }
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        if (collision.gameObject.CompareTag("Rope"))
        {

        }
    }

    public void ClimbingMovement(Vector2 dir)
    {
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, dir.y * climbSpeed);
    }

    public void Climbing(Transform ropeTrans)
    {
        isClimbing = true;
        rb.gravityScale = 0; // 중력을 없애 플레이어가 줄에 매달려 있을 때 떨어지지 않도록 합니다.
        rb.linearVelocity = new Vector2(0, 0); // 줄에 붙을 때의 속도를 초기화합니다.

        transform.position = new(ropeTrans.position.x, transform.position.y);
    }

    public void CancelClimbing()
    {
        isClimbing = false;
        rb.gravityScale = originalGravity; // 원래 중력 값으로 되돌립니다.
    }
}
