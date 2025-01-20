using Mirror;
using UnityEngine;

public abstract class Switch : RaycastController
{
    [SyncVar] // 네트워크 싱크를 위한 상태 변수
    private bool isActivated = false;

    public Sprite onSwitchSprite;
    public Sprite offSwitchSprite;

    // 스위치 활성화 상태 접근자
    public bool IsActivated => isActivated;

    // 스위치를 활성화 또는 비활성화하는 메서드
    public virtual void ToggleSwitch()
    {
        isActivated = !isActivated;
        OnSwitchStateChanged(isActivated);
    }

    // 스위치 상태 변경 이벤트 (상속받아 재정의 필요)
    protected abstract void OnSwitchStateChanged(bool newState);

    public void DetectPlayer()
    {
        UpdateRaycastOrigins();
        float rayLength = skinWidth * 2f;

        for (int i = 0; i < verticalRayCount; i++)
        {
            Vector2 rayOrigin = raycastOrigins.bottomLeft + Vector2.right * (verticalRaySpacing * i);
            RaycastHit2D hit = Physics2D.Raycast(rayOrigin, Vector2.down, rayLength, collisionMask);

            Debug.DrawRay(rayOrigin, Vector2.down * rayLength, Color.blue);

            if (hit)
            {
                if (hit.transform.position.y + (hit.collider.bounds.size.y * 0.4f) + hit.collider.offset.y < transform.position.y + boxCollider.offset.y - (boxCollider.size.y * 0.5f))
                {
                    if (hit.transform.GetComponent<Controller2D>() != null)
                    {
                        Controller2D player = hit.transform.GetComponent<Controller2D>();
                        if (player.collisions.above)
                        {
                            ToggleSwitch();
                            break;
                        }
                    }
                }
            }
        }
    }
}
