using DG.Tweening;
using Mirror;
using Steamworks;
using UnityEngine;

public class PlayerBounce : NetworkBehaviour
{
    public float springForce = 10f;
    public float springMaxForce = 16f;

    private Rigidbody2D rb;
    private BoxCollider2D boxCollider;

    public bool isShrinked { get; private set; }

    private void Start()
    {
        boxCollider = GetComponent<BoxCollider2D>();
        rb = GetComponent<Rigidbody2D>();
        isShrinked = false;
    }

    public void PlayerApplySpringEffect(bool isJumpHold)
    {
        if (!IsExsitUnderPlayer()) return;

        rb.linearVelocity = Vector2.zero;

        Vector2 springVec = new(rb.linearVelocityX * 1.1f, springForce);
        float maxY = isJumpHold ? springMaxForce : springForce;
        if (isJumpHold)
        {
            springVec.y = springVec.y + springForce;
            if (springVec.y > springMaxForce)
                springVec.y = springMaxForce;
        }
        else
            springVec.y = maxY;

        if (springVec.y < springForce)
        {
            springVec.y = springForce;
        }

        rb.linearVelocity = springVec;
    }

    private bool IsExsitUnderPlayer()
    {
        PlayerController otherPlayer = SearchPlayer(Vector2.down);
        return otherPlayer != null && rb.linearVelocity.y < 0;
    }

    private bool IsExsitOnPlayer()
    {
        PlayerController otherPlayer = SearchPlayer(Vector2.up);
        return otherPlayer != null;
    }

    public void StartShrinking(float duration = 0.1f)
    {
        if (isShrinked || !IsExsitOnPlayer())
            return;

        Vector3 targetScale = new(transform.localScale.x, transform.localScale.y * 0.5f, transform.localScale.z);
        isShrinked = true;
        transform.DOScale(targetScale, duration).SetEase(Ease.Linear).OnComplete(() =>
        {
            // 원래 크기로 돌아가기
            ExpandWithDOTween(duration);
        });
    }
    private void ExpandWithDOTween(float duration = 0.1f)
    {
        transform.DOScale(Vector3.one, duration).SetEase(Ease.Linear).OnComplete(() =>
        {
            isShrinked = false;
        });
    }

    private PlayerController SearchPlayer(Vector2 dir)
    {
        Vector2 boxSize = boxCollider.size;
        Vector2 boxCenter = (Vector2)transform.position + boxCollider.offset;
        float raySpacing = boxSize.x / 4f; // 박스의 가로 크기를 기준으로 Ray를 여러 개 생성
        int rayCount = 5; // 총 5개의 Raycast 사용
        float yPos = (dir.y > 0) ? boxCollider.bounds.max.y : boxCollider.bounds.min.y;

        for (int i = 0; i < rayCount; i++)
        {
            // Ray의 시작 위치를 좌측부터 일정 간격으로 설정
            Vector2 rayOrigin = new Vector2(boxCollider.bounds.min.x + (i * raySpacing), yPos);

            RaycastHit2D[] hits = Physics2D.RaycastAll(rayOrigin, dir, 0.15f, LayerMask.GetMask("Player"));
            Debug.DrawRay(rayOrigin, dir * 0.15f, Color.red, 0.1f);

            foreach (RaycastHit2D hit in hits)
            {
                if (hit.collider != null && hit.collider.gameObject != gameObject)
                {
                    PlayerController otherPlayerController = hit.collider.GetComponent<PlayerController>();
                    return otherPlayerController;
                }
            }
        }
        return null;
    }
}
