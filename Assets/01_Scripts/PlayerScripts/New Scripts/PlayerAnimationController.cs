using UnityEngine;

// 전이 그래프 없이 PlayerState를 Animator 상태로 직접 재생한다. 네트워크 동기화는 PlayerStateController가 맡는다
public class PlayerAnimationController : MonoBehaviour
{
    private static readonly int IdleHash = Animator.StringToHash("Idle");
    private static readonly int WalkHash = Animator.StringToHash("Walk");
    private static readonly int JumpHash = Animator.StringToHash("Jump");
    private static readonly int ClimbHash = Animator.StringToHash("Climb");
    private static readonly int ShrinkHash = Animator.StringToHash("Shrink");
    private static readonly int DamagedHash = Animator.StringToHash("Damaged");

    [SerializeField] private Animator animator;

    // 피격 클립은 1프레임이라 클립 길이만큼만 보이면 거의 안 보인다. 1회성 클립의 최소 표시 시간
    [SerializeField, Min(0.1f)] private float minOneShotDuration = 0.25f;

    private PlayerStateController stateController;
    private int currentStateHash;
    private int oneShotHash;
    private float oneShotMinEndTime;

    private void Awake()
    {
        if (animator == null) animator = GetComponent<Animator>();
        stateController = GetComponent<PlayerStateController>();
    }

    private void OnEnable()
    {
        // 비활성화 동안 Animator가 기본 상태로 돌아갔을 수 있어 다음 Update에서 다시 재생하게 한다
        currentStateHash = 0;
    }

    private void Update()
    {
        if (IsOneShotPlaying()) return;

        PlayerState state = stateController.GetDisplayState();
        int stateHash = ToStateHash(state);

        if (stateHash != currentStateHash)
        {
            animator.Play(stateHash, 0, 0f);
            currentStateHash = stateHash;
        }

        // 줄에 매달려 멈춰 있으면 등반 클립을 현재 프레임에서 멈춘다
        animator.speed = state == PlayerState.ClimbIdle ? 0f : 1f;
    }

    public void PlayOneShot(PlayerState state)
    {
        int stateHash = state switch
        {
            PlayerState.Damaged => DamagedHash,
            PlayerState.Shrink => ShrinkHash,
            _ => 0
        };

        if (stateHash == 0)
        {
            Debug.LogWarning($"1회성 애니메이션이 없는 상태입니다: {state}", this);
            return;
        }

        animator.speed = 1f;
        animator.Play(stateHash, 0, 0f);
        oneShotHash = stateHash;
        oneShotMinEndTime = Time.time + minOneShotDuration;
        // 1회성 클립이 끝나면 지속 상태를 다시 재생하도록 캐시를 비운다
        currentStateHash = 0;
    }

    private bool IsOneShotPlaying()
    {
        if (oneShotHash == 0) return false;
        if (Time.time < oneShotMinEndTime) return true;

        AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(0);
        if (info.shortNameHash == oneShotHash && info.normalizedTime < 1f) return true;

        oneShotHash = 0;
        return false;
    }

    private static int ToStateHash(PlayerState state)
    {
        return state switch
        {
            PlayerState.Walk => WalkHash,
            PlayerState.Jump => JumpHash,
            PlayerState.Climb => ClimbHash,
            PlayerState.ClimbIdle => ClimbHash,
            _ => IdleHash
        };
    }
}
