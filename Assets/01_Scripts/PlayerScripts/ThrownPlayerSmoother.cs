using UnityEngine;

// 운반에서 풀려난 원격 플레이어를 다른 화면에서 자연스럽게 보이게 하는 표시 보정.
// 원격 플레이어는 NetworkTransform 보간이라 과거 시점에 그려진다. 운반 중에는 운반자 머리에 붙어 그 화면의 현재 시점에 보이다가,
// 놓이는 순간 과거 시점으로 돌아가 "뒤에서 출발해 날아오는" 것처럼 보인다.
// 놓인 순간 보이던 자리에서 던진 궤적을 직접 그리고 blendDuration 동안 보간 위치로 넘겨준다. 판정은 서버 위치 그대로다.
public class ThrownPlayerSmoother : MonoBehaviour
{
    [Tooltip("직접 그린 궤적에서 네트워크 보간 위치로 넘어가는 시간(초)")]
    [SerializeField] private float blendDuration = 0.3f;

    private MovementHandler movementHandler;
    private Controller2D controller;

    private bool isActive;
    private float elapsed;
    private Vector2 predictedPosition;
    private Vector2 predictedVelocity;
    // NetworkTransform이 마지막으로 정한 위치. 스냅샷이 아직 없으면 서버가 정한 놓은 위치다.
    private Vector2 networkPosition;
    private Vector3 lastOutput;

    private void Awake()
    {
        movementHandler = GetComponent<MovementHandler>();
        controller = GetComponent<Controller2D>();
    }

    public void Begin(Vector2 shownPosition, Vector2 serverPosition, Vector2 launchVelocity)
    {
        isActive = true;
        elapsed = 0f;
        predictedPosition = shownPosition;
        predictedVelocity = launchVelocity;
        networkPosition = serverPosition;
        transform.position = shownPosition;
        lastOutput = transform.position;
    }

    public void Cancel() => isActive = false;

    // NetworkTransform은 Update에서 보간 위치를 적용하므로 그 뒤에 덮어쓴다.
    private void LateUpdate()
    {
        if (!isActive) return;

        // 이번 프레임에 NetworkTransform이 위치를 정하지 않았으면 transform에는 지난 출력이 그대로 남아 있다.
        if (transform.position != lastOutput)
            networkPosition = transform.position;

        elapsed += Time.deltaTime;
        AdvancePrediction(Time.deltaTime);

        if (elapsed >= blendDuration)
        {
            isActive = false;
            transform.position = networkPosition;
            return;
        }

        float weight = Mathf.SmoothStep(0f, 1f, elapsed / blendDuration);
        transform.position = Vector2.Lerp(predictedPosition, networkPosition, weight);
        lastOutput = transform.position;
    }

    private void AdvancePrediction(float deltaTime)
    {
        predictedVelocity.y = Mathf.Max(predictedVelocity.y + movementHandler.Gravity * deltaTime, -movementHandler.MaxFallSpeed);
        Vector2 next = predictedPosition + predictedVelocity * deltaTime;

        // 벽·바닥을 뚫고 그리지 않도록 막히면 그 자리에 멈춘다. 이후는 보간 위치가 이어받는다.
        if (controller.FindBlockingCollider(next, RaycastController.overlapTolerance) != null)
        {
            predictedVelocity = Vector2.zero;
            return;
        }
        predictedPosition = next;
    }
}
