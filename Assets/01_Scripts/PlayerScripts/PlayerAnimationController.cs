using UnityEngine;

public class PlayerAnimationController : MonoBehaviour
{
    [SerializeField]
    private Animator animator;

    // 애니메이션 파라미터 이름 정의
    private static readonly int IsWalking = Animator.StringToHash("isWalking");
    private static readonly int JumpTrigger = Animator.StringToHash("jumpTrigger");
    private static readonly int AttackTrigger = Animator.StringToHash("attackTrigger");
    private static readonly int IsLifting = Animator.StringToHash("isLifting");
    private static readonly int ThrowTrigger = Animator.StringToHash("throwTrigger");
    private static readonly int DamagedTrigger = Animator.StringToHash("damagedTrigger");
    private static readonly int IsFalling = Animator.StringToHash("isFalling");
    private static readonly int IsGround = Animator.StringToHash("isGround");

    public void PlayIdleAnimation()
    {
        animator.SetBool(IsWalking, false);
        animator.SetBool(IsFalling, false);
        animator.SetBool(IsLifting, false);
    }

    public void PlayWalkAnimation()
    {
        animator.SetBool(IsWalking, true);
    }

    public void PlayJumpAnimation()
    {
        animator.SetTrigger(JumpTrigger);
        animator.SetBool(IsGround, false);
    }

    public void PlayAttackAnimation()
    {
        animator.SetTrigger(AttackTrigger);
    }

    public void PlayLiftingAnimation(bool isLifting)
    {
        animator.SetBool(IsLifting, isLifting);
    }

    public void PlayThrowAnimation()
    {
        animator.SetTrigger(ThrowTrigger);
    }

    public void PlayDamagedAnimation()
    {
        animator.SetTrigger(DamagedTrigger);
    }

    public void PlayFallAnimation()
    {
        animator.SetBool(IsFalling, true);
    }

    public void StopFallAnimation()
    {
        animator.SetBool(IsFalling, false);
    }

    public void GroundState()
    {
        if (!animator.GetBool(IsGround))
            animator.SetBool(IsGround, true);
    }
}
