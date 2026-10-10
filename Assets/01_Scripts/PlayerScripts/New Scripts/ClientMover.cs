using Mirror;
using UnityEngine;

/// <summary>
/// 로컬 플레이어의 클라이언트 사이드 예측을 담당한다.
/// 입력을 즉시 로컬 시뮬레이션에 반영하고, 서버 보정값이 오면 해당 시점부터 재시뮬레이션한다.
/// </summary>
[DefaultExecutionOrder(-10)] // PlayerController2D보다 먼저 실행해 같은 프레임 충돌 방지
public class ClientMover : NetworkBehaviour
{
    // 핑 300ms 환경에서 최대 약 128프레임 분량 보관
    private const int BUFFER_SIZE = 128;

    // 서버와 예측값의 위치 오차가 이 값 이하면 보정 생략 (부동소수점 드리프트 허용치)
    private const float RECONCILE_THRESHOLD = 0.15f;

    // 밟기 튕김처럼 위치는 같은데 속도만 다른 순간을 바로 잡는다. 위치 오차만 보면 몇 틱 뒤 크게 벌어진 다음에야 보정된다.
    private const float VELOCITY_RECONCILE_THRESHOLD = 1f;

    // 보정·재동기화로 예측 위치가 갑자기 바뀌면, 화면에는 이전 위치에서 이 시간 상수로 따라가게 그린다.
    // 판정은 항상 보정된 위치로 하므로 서버와 같고, 보이는 위치만 부드러워진다.
    private const float VISUAL_SMOOTHING_TIME = 0.08f;
    // 리스폰처럼 멀리 옮겨진 경우는 따라가지 않고 바로 보여 준다.
    private const float VISUAL_SNAP_DISTANCE = 2f;

    private MovementHandler movementHandler;
    private PlayerInputManager inputManager;

    private readonly InputPayload[] inputBuffer = new InputPayload[BUFFER_SIZE];
    private readonly StatePayload[] stateBuffer = new StatePayload[BUFFER_SIZE];

    private uint currentSequenceNumber = 0;

    // 서버가 순간이동시킬 때마다 바뀐다. 다른 에포크의 보정값은 순간이동 전 기준이라 버린다.
    private ushort epoch;

    private bool hasPendingServerState = false;
    private StatePayload pendingServerState;

    // LateUpdate에서 NT의 덮어쓰기를 복원하기 위한 예측 위치
    private Vector3 predictedPosition;

    private Vector2 visualOffset;
    // 운반 중처럼 예측하지 않는 동안 화면에 보이던 위치. 풀려날 때 여기서부터 따라가게 한다.
    private Vector3 lastDisplayedPosition;

    private void Awake()
    {
        movementHandler = GetComponent<MovementHandler>();
        inputManager = GetComponent<PlayerInputManager>();
        // Awake 시점 위치로 초기화 — 첫 LateUpdate에서 원점으로 순간이동하는 버그 방지
        predictedPosition = transform.position;
        lastDisplayedPosition = transform.position;
    }

    private void FixedUpdate()
    {
        if (!isOwned) return;

        // 활성 클라이언트가 없으면 Command를 보낼 수 없음 — 예측 자체가 무의미하므로 건너뜀
        // (연결 종료·씬 전환 시점에 isOwned가 아직 true인 채 FixedUpdate가 실행되는 경우 방지)
        if (!NetworkClient.active) return;

        // 캐리 상태처럼 MovementHandler가 비활성화된 경우 예측 건너뜀.
        // 이때 누른 점프가 남아 있다가 풀려난 직후 튀어 나가지 않도록 비운다.
        if (!movementHandler.enabled)
        {
            inputManager.ClearJumpOneShots();
            return;
        }

        // LateUpdate가 보정 오프셋을 더해 그려 두었으므로 시뮬레이션 전에 실제 예측 위치로 되돌린다.
        transform.position = predictedPosition;

        // 서버 보정값이 있으면 이번 예측 전에 먼저 처리
        if (hasPendingServerState)
        {
            Reconcile(pendingServerState);
            hasPendingServerState = false;
        }

        InputPayload input = BuildInputPayload();
        // 읽은 즉시 클리어 — FixedUpdate는 Input 콜백보다 먼저 실행되므로 LateUpdate 리셋에 의존하면 항상 누락됨
        inputManager.ClearJumpOneShots();

        int bufferIndex = (int)(currentSequenceNumber % BUFFER_SIZE);
        inputBuffer[bufferIndex] = input;

        // 로컬 즉시 실행 — 네트워크 왕복 없이 화면에 바로 반영
        movementHandler.ApplyInput(input);
        movementHandler.Simulate(Time.fixedDeltaTime);

        stateBuffer[bufferIndex] = movementHandler.GetState(currentSequenceNumber);
        predictedPosition = transform.position;

        CmdSendInput(input);
        currentSequenceNumber++;
    }

    private void LateUpdate()
    {
        if (!isOwned) return;

        // 운반 중이거나 도착 지점 안에서는 예측하지 않으므로 부모·NetworkTransform이 정한 위치를 그대로 둔다.
        if (!movementHandler.enabled)
        {
            lastDisplayedPosition = transform.position;
            return;
        }

        visualOffset = Vector2.Lerp(visualOffset, Vector2.zero, 1f - Mathf.Exp(-Time.deltaTime / VISUAL_SMOOTHING_TIME));

        // NetworkTransformUnreliable이 Update에서 서버 위치로 덮어썼을 수 있으므로 예측 위치로 복원
        // 렌더링은 LateUpdate 이후에 일어나므로 플레이어 눈에는 예측 위치(+보정 오프셋)만 보임
        transform.position = predictedPosition + (Vector3)visualOffset;
        lastDisplayedPosition = transform.position;
    }

    private void AddVisualCorrection(Vector2 correction)
    {
        visualOffset += correction;
        if (visualOffset.magnitude > VISUAL_SNAP_DISTANCE)
            visualOffset = Vector2.zero;
    }

    private InputPayload BuildInputPayload()
    {
        return new InputPayload
        {
            epoch         = epoch,
            sequenceNumber = currentSequenceNumber,
            movement      = inputManager.MovementInput,
            jump          = inputManager.IsJumpPressed,
            jumpHeld      = inputManager.IsJumpHold,
            jumpUp        = inputManager.IsJumpUp,
            run           = inputManager.IsRunPressed,
            deltaTime     = Time.fixedDeltaTime,
        };
    }

    // 서버에 입력 전달 — ServerMover가 물리를 실행하고 주기적으로 보정값을 돌려보냄
    [Command]
    private void CmdSendInput(InputPayload input)
    {
        GetComponent<ServerMover>().ReceiveInput(input);
    }

    // 3단계(ServerMover)에서 서버 보정값을 전송할 때 호출
    [TargetRpc]
    public void TargetSendState(NetworkConnection conn, StatePayload serverState)
    {
        if (serverState.epoch != epoch) return;

        // 이미 받은 것보다 오래된 상태는 무시
        if (hasPendingServerState && serverState.sequenceNumber <= pendingServerState.sequenceNumber)
            return;

        // 버퍼 범위를 벗어난 너무 오래된 상태는 재시뮬레이션 불가 — 무시
        if (currentSequenceNumber > BUFFER_SIZE && serverState.sequenceNumber < currentSequenceNumber - BUFFER_SIZE)
            return;

        pendingServerState = serverState;
        hasPendingServerState = true;
    }

    // 운반 해제·리스폰처럼 서버가 위치를 정했을 때 그 상태에서 예측을 새로 시작한다.
    [TargetRpc]
    public void TargetResync(NetworkConnection conn, StatePayload state)
    {
        ApplyResync(state);
    }

    // 호스트 자신의 플레이어는 같은 프레임에 바로 적용해야 LateUpdate가 이전 예측 위치로 되돌리지 않는다.
    public void ApplyResync(StatePayload state)
    {
        epoch = state.epoch;
        hasPendingServerState = false;
        movementHandler.SetState(state);
        predictedPosition = transform.position;

        // 운반에서 풀려날 때 화면의 운반자는 보간 때문에 과거 위치에 있다. 그 머리 위에서 서버 위치로 바로 옮기면 튀므로 따라가게 한다.
        visualOffset = Vector2.zero;
        AddVisualCorrection(lastDisplayedPosition - predictedPosition);
    }

    private void Reconcile(StatePayload serverState)
    {
        int bufferIndex = (int)(serverState.sequenceNumber % BUFFER_SIZE);
        StatePayload predictedState = stateBuffer[bufferIndex];

        float positionError = Vector2.Distance(serverState.position, predictedState.position);
        float velocityError = Vector2.Distance(serverState.velocity, predictedState.velocity);

        // 오차가 임계값 이하면 정상 예측 — 보정 불필요
        if (positionError < RECONCILE_THRESHOLD && velocityError < VELOCITY_RECONCILE_THRESHOLD) return;

        Vector3 positionBeforeReconcile = predictedPosition;

        // 서버 상태로 복원 후 이후 입력들을 순서대로 재시뮬레이션
        movementHandler.SetState(serverState);

        // 재시뮬레이션 중임을 표시 — 점프 사운드 등 부작용 있는 이벤트 억제
        movementHandler.isReconciling = true;

        uint replaySeq = serverState.sequenceNumber + 1;
        while (replaySeq < currentSequenceNumber)
        {
            int replayIndex = (int)(replaySeq % BUFFER_SIZE);
            InputPayload replayInput = inputBuffer[replayIndex];

            movementHandler.ApplyInput(replayInput);
            movementHandler.Simulate(replayInput.deltaTime);

            stateBuffer[replayIndex] = movementHandler.GetState(replaySeq);
            replaySeq++;
        }

        movementHandler.isReconciling = false;
        predictedPosition = transform.position;
        AddVisualCorrection(positionBeforeReconcile - predictedPosition);
    }
}
