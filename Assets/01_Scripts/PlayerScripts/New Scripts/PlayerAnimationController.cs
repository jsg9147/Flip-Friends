using System.Collections;
using UnityEngine;
using Mirror;

public class PlayerAnimationController : NetworkBehaviour
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
    private static readonly int IsClimb = Animator.StringToHash("isClimb");

    private void Start()
    {
        animator = GetComponent<Animator>();
    }

    [Command] // 클라이언트에서 서버로 명령을 전송
    public void CmdChangeAnimation(PlayerState state)
    {
        RpcChangeAnimation(state); // 서버에서 모든 클라이언트에게 애니메이션 변경을 요청
    }

    [ClientRpc] // 서버에서 모든 클라이언트로 애니메이션 변경을 브로드캐스트
    private void RpcChangeAnimation(PlayerState state)
    {
        if (animator.speed != 1)
            animator.speed = 1;

        switch (state)
        {
            case PlayerState.Idle:
                PlayIdleAnimation();
                break;
            case PlayerState.Walk:
                PlayWalkAnimation();
                break;
            case PlayerState.Jump:
                PlayJumpAnimation();
                break;
            case PlayerState.Shrink:
                ShrinkAnimation();
                break;
            case PlayerState.Damaged:
                PlayDamagedAnimation();
                break;
            case PlayerState.Climb:
                ClimbAnimation();
                break;
            case PlayerState.ClimbIdle:
                ClimbIdleAnimation();
                break;
            default:
                print($"Not yet {state} motion");
                break;
        }
    }

    public void PlayIdleAnimation()
    {
        animator.SetBool(IsWalking, false);
        animator.SetBool(IsFalling, false);
        animator.SetBool(IsLifting, false);
        animator.SetBool(IsClimb, false);
    }

    public void PlayWalkAnimation()
    {
        animator.SetBool(IsWalking, true);
    }

    public void PlayJumpAnimation()
    {
        animator.SetBool(IsClimb, false);
        animator.SetBool(IsGround, false);
        animator.SetBool(IsWalking, false);
        animator.SetTrigger(JumpTrigger);
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

    public void ClimbAnimation()
    {
        animator.SetBool(IsClimb, true);
        animator.speed = 1;
    }

    public void ClimbIdleAnimation()
    {
        animator.SetBool(IsClimb, true);
        animator.speed = 0;
    }

    public void ShrinkAnimation()
    {
        animator.SetTrigger("isShrink");
    }
    public void GroundState(bool isGround)
    {
        animator.SetBool(IsGround, isGround);
    }
}
