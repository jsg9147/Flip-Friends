using System.Collections;
using UnityEngine;
using DG.Tweening;
using Mirror;

public class PlayerDetectPlatform : PlatformController
{
    [Header("Platform Settings")]
    public float initialWaitTime;
    public bool disappearAfterComplete;
    public bool disableColliderWhenFalling;

    [Header("References")]
    public SpriteRenderer spriteRenderer;

    private bool isFirstWait = true;
    private bool isPlayerDetected = false;
    private bool hasDisappeared = false;
    private bool hasStartedMoving = false;

    private float delayStartTime;
    private bool delayStarted = false;

    public override void Start()
    {
        base.Start();
        ResetPlatformState();
    }

    private void Update()
    {
        if (!isServer) return;

        if (!delayStarted)
        {
            delayStartTime = Time.time;
            delayStarted = true;
        }

        if (Time.time >= delayStartTime + 0.2f)
        {
            DetectPlayer();
        }
    }

    public override void FixedUpdate()
    {
        if (!isServer) return;
        UpdateRaycastOrigins();

        if (isPlayerDetected && !isCompleted)
        {
            HandleInitialWait();

            if (!hasStartedMoving && Time.time >= nextMoveTime)
            {
                StartPlatformMovement();
            }
            base.FixedUpdate();
        }

        if (isCompleted)
        {
            HandleCompletion();
        }

        if (isWaitingAtStart && !isFirstWait)
        {
            ResetPlatformState();
        }
    }

    private void HandleInitialWait()
    {
        if (isFirstWait)
        {
            nextMoveTime = Time.time + initialWaitTime;
            isFirstWait = false;
        }
    }

    private void StartPlatformMovement()
    {
        hasStartedMoving = true;
        if(disableColliderWhenFalling)
            RpcFadeOutSpriteAlpha(0.5f);
    }

    private void HandleCompletion()
    {
        if (disableColliderWhenFalling)
        {
            RpcToggleCollider(false);
        }

        if (disappearAfterComplete && !hasDisappeared)
        {
            HandleDisappearance();
        }
    }

    private void HandleDisappearance()
    {
        hasDisappeared = true;
    }

    private void DetectPlayer()
    {
        float rayLength = skinWidth;

        for (int i = 0; i < verticalRayCount; i++)
        {
            Vector2 rayOrigin = raycastOrigins.topLeft + Vector2.right * (verticalRaySpacing * i);
            RaycastHit2D hit = Physics2D.Raycast(rayOrigin, Vector2.up, rayLength, passengerMask);

            if (hit)
            {
                if (hit.transform.position.y - (hit.collider.bounds.size.y * 0.5f) - hit.collider.offset.y > transform.position.y + boxCollider.offset.y + (boxCollider.size.y * 0.5f))
                {
                    if (hit.transform.GetComponent<Controller2D>() != null)
                    {
                        Controller2D player = hit.transform.GetComponent<Controller2D>();
                        if (player.collisions.below)
                        {
                            isPlayerDetected = true;

                            if (disableColliderWhenFalling)
                                RpcPlayerFallAnimation(true);
                            break;
                        }
                    }
                }
            }
        }
    }

    [ClientRpc]
    private void RpcToggleCollider(bool enable)
    {
        var collider = GetComponent<BoxCollider2D>();
        if (collider != null)
        {
            collider.enabled = enable;
        }
    }

    [ClientRpc]
    private void RpcFadeOutSpriteAlpha(float duration)
    {
        spriteRenderer?.DOFade(0f, duration).SetEase(Ease.Linear);
    }

    [ClientRpc]
    private void RpcSpriteRecovery()
    {
        if (spriteRenderer != null)
        {
            spriteRenderer.color = Color.white;
        }
    }

    [ClientRpc]
    private void RpcPlayerFallAnimation(bool isFall)
    {
        var animator = GetComponent<Animator>();
        if (animator != null)
        {
            animator.SetBool("isFall", isFall);
        }
    }

    public void ResetPlatformState()
    {
        isFirstWait = true;
        isPlayerDetected = false;
        hasDisappeared = false;
        hasStartedMoving = false;
        isCompleted = false;
        isWaitingAtStart = false;

        if (isServer)
        {
            RpcSyncPosition(transform.position);
            RpcSpriteRecovery();
            RpcToggleCollider(true);
            RpcPlayerFallAnimation(false);
        }
    }
}
