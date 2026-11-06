using Mirror;
using UnityEngine;

public abstract class Switch : RaycastController
{
    [SyncVar(hook = nameof(OnActivatedChanged))]
    private bool isActivated = false;

    public Sprite onSwitchSprite;
    public Sprite offSwitchSprite;

    private SpriteRenderer switchRenderer;

    public bool IsActivated => isActivated;

    public override void OnStartServer()
    {
        base.OnStartServer();
        ApplySwitchVisual(isActivated);
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        ApplySwitchVisual(isActivated);
    }

    [Server]
    public virtual void ToggleSwitch()
    {
        if (!isServer) return;
        SetSwitchState(!isActivated);
    }

    [Server]
    public void SetSwitchState(bool newState)
    {
        if (!isServer) return;
        if (isActivated == newState) return;
        isActivated = newState;
        ApplySwitchVisual(newState);
        OnSwitchStateChanged(newState);
    }

    // 서버 효과와 클라이언트 표시를 분리해 hook에서 상태 변경을 재통보하지 않는다.
    protected virtual void OnSwitchStateChanged(bool newState) { }

    private void OnActivatedChanged(bool oldState, bool newState) => ApplySwitchVisual(newState);

    protected virtual void ApplySwitchVisual(bool newState)
    {
        if (switchRenderer == null) switchRenderer = GetComponent<SpriteRenderer>();
        if (switchRenderer != null) switchRenderer.sprite = newState ? onSwitchSprite : offSwitchSprite;
    }

    [Server]
    public void DetectPlayer()
    {
        if (HasPlayerBelow()) ToggleSwitch();
    }

    protected bool HasPlayerBelow()
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
                            return true;
                        }
                    }
                }
            }
        }
        return false;
    }
}
