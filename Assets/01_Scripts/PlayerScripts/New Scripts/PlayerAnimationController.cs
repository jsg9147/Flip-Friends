using UnityEngine;
using Mirror;

public class PlayerAnimationController : NetworkBehaviour
{
    [SerializeField]
    private Animator animator;

    // Cached animation parameters
    private static readonly int IsWalking = Animator.StringToHash("isWalking");
    private static readonly int JumpTrigger = Animator.StringToHash("jumpTrigger");
    private static readonly int AttackTrigger = Animator.StringToHash("attackTrigger");
    private static readonly int IsLifting = Animator.StringToHash("isLifting");
    private static readonly int ThrowTrigger = Animator.StringToHash("throwTrigger");
    private static readonly int DamagedTrigger = Animator.StringToHash("damagedTrigger");
    private static readonly int IsFalling = Animator.StringToHash("isFalling");
    private static readonly int IsGround = Animator.StringToHash("isGround");
    private static readonly int IsClimb = Animator.StringToHash("isClimb");
    private static readonly int IsShrinkTrigger = Animator.StringToHash("isShrink");

    private void Awake()
    {
        // Avoid redundant GetComponent calls
        if (animator == null) animator = GetComponent<Animator>();
    }

    [ClientRpc]
    public void RpcChangeAnimation(PlayerState state)
    {
        if (animator.speed != 1) animator.speed = 1; // Ensure speed reset

        ResetAllAnimations(); // Reset before playing a new one

        switch (state)
        {
            case PlayerState.Idle:
                animator.SetBool(IsWalking, false);
                break;
            case PlayerState.Walk:
                animator.SetBool(IsWalking, true);
                break;
            case PlayerState.Jump:
                animator.SetTrigger(JumpTrigger);
                break;
            case PlayerState.Shrink:
                animator.SetTrigger(IsShrinkTrigger);
                break;
            case PlayerState.Damaged:
                animator.SetTrigger(DamagedTrigger);
                break;
            case PlayerState.Climb:
                animator.SetBool(IsClimb, true);
                break;
            case PlayerState.ClimbIdle:
                animator.SetBool(IsClimb, true);
                animator.speed = 0; // Pause animation
                break;
            default:
                Debug.LogWarning($"Animation state '{state}' not implemented.");
                break;
        }
    }

    private void ResetAllAnimations()
    {
        animator.ResetTrigger(JumpTrigger);
        animator.ResetTrigger(AttackTrigger);
        animator.ResetTrigger(DamagedTrigger);
        animator.ResetTrigger(IsShrinkTrigger);

        animator.SetBool(IsWalking, false);
        animator.SetBool(IsLifting, false);
        animator.SetBool(IsFalling, false);
        animator.SetBool(IsGround, true); // Default state as ground
        animator.SetBool(IsClimb, false);
    }

    [ClientRpc]
    public void RpcGroundState(bool isGround)
    {
        animator.SetBool(IsGround, isGround);
    }

    public void PlayAttackAnimation() => animator.SetTrigger(AttackTrigger);
    public void PlayLiftingAnimation(bool isLifting) => animator.SetBool(IsLifting, isLifting);
    public void PlayThrowAnimation() => animator.SetTrigger(ThrowTrigger);
    public void PlayFallAnimation(bool isFalling) => animator.SetBool(IsFalling, isFalling);
}
