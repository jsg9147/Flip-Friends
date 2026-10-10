using Mirror;
using UnityEngine;

public class PlayerStateController : NetworkBehaviour
{
    private const float WALK_VELOCITY_THRESHOLD = 0.05f;

    // 지속 상태는 SyncVar로 둬서 바뀔 때만 전송되고, 늦게 들어온 클라이언트도 현재 값을 받는다
    [SyncVar] private PlayerState playerState;

    private MovementHandler movementHandler;
    private PlayerAnimationController animationController;

    public PlayerState CurrentState => playerState;

    private void Awake()
    {
        movementHandler = GetComponent<MovementHandler>();
        animationController = GetComponent<PlayerAnimationController>();
    }

    [Server]
    public void ChangeState(PlayerState newState)
    {
        playerState = newState;
    }

    // 피격·밟힘은 지속 상태가 아니라 순간 이벤트다. SyncVar에 실으면 같은 틱의 다음 상태에 덮여 클라이언트가 놓칠 수 있다
    [ClientRpc]
    public void RpcPlayOneShot(PlayerState state)
    {
        animationController.PlayOneShot(state);
    }

    // 내 캐릭터의 이동 상태는 예측 이동 결과로 바로 정해 서버 왕복만큼 애니메이션이 늦지 않게 한다.
    // 등반·운반처럼 서버만 아는 상태는 서버 값을 그대로 쓴다
    public PlayerState GetDisplayState()
    {
        if (isOwned && IsLocomotion(playerState) && movementHandler.enabled)
            return ResolveLocomotion(movementHandler.isGrounded, movementHandler.CurrentVelocity.x);

        return playerState;
    }

    public static bool IsLocomotion(PlayerState state)
    {
        return state == PlayerState.Idle || state == PlayerState.Walk || state == PlayerState.Jump;
    }

    // 서버 판정과 소유 클라이언트 예측이 같은 규칙을 쓰도록 한 곳에 둔다
    public static PlayerState ResolveLocomotion(bool isGrounded, float velocityX)
    {
        if (!isGrounded) return PlayerState.Jump;
        return IsWalking(velocityX) ? PlayerState.Walk : PlayerState.Idle;
    }

    public static PlayerState ResolveClimb(Vector2 velocity)
    {
        return velocity.magnitude > WALK_VELOCITY_THRESHOLD ? PlayerState.Climb : PlayerState.ClimbIdle;
    }

    public static bool IsWalking(float velocityX)
    {
        return Mathf.Abs(velocityX) > WALK_VELOCITY_THRESHOLD;
    }
}
