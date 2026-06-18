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

    // 서버와 예측값의 위치 오차가 이 값 이하면 보정 생략
    private const float RECONCILE_THRESHOLD = 0.05f;

    private MovementHandler movementHandler;
    private PlayerInputManager inputManager;

    private readonly InputPayload[] inputBuffer = new InputPayload[BUFFER_SIZE];
    private readonly StatePayload[] stateBuffer = new StatePayload[BUFFER_SIZE];

    private uint currentSequenceNumber = 0;

    private bool hasPendingServerState = false;
    private StatePayload pendingServerState;

    // LateUpdate에서 NT의 덮어쓰기를 복원하기 위한 예측 위치
    private Vector3 predictedPosition;

    private void Awake()
    {
        movementHandler = GetComponent<MovementHandler>();
        inputManager = GetComponent<PlayerInputManager>();
    }

    private void FixedUpdate()
    {
        if (!isOwned) return;

        // 캐리 상태처럼 MovementHandler가 비활성화된 경우 예측 건너뜀
        if (!movementHandler.enabled) return;

        // 서버 보정값이 있으면 이번 예측 전에 먼저 처리
        if (hasPendingServerState)
        {
            Reconcile(pendingServerState);
            hasPendingServerState = false;
        }

        InputPayload input = BuildInputPayload();

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

        // NetworkTransformUnreliable이 Update에서 서버 위치로 덮어썼을 수 있으므로 예측 위치로 복원
        // 렌더링은 LateUpdate 이후에 일어나므로 플레이어 눈에는 예측 위치만 보임
        transform.position = predictedPosition;
    }

    private InputPayload BuildInputPayload()
    {
        return new InputPayload
        {
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
        // 이미 받은 것보다 오래된 상태는 무시
        if (hasPendingServerState && serverState.sequenceNumber <= pendingServerState.sequenceNumber)
            return;

        pendingServerState = serverState;
        hasPendingServerState = true;
    }

    private void Reconcile(StatePayload serverState)
    {
        int bufferIndex = (int)(serverState.sequenceNumber % BUFFER_SIZE);
        StatePayload predictedState = stateBuffer[bufferIndex];

        float positionError = Vector2.Distance(serverState.position, predictedState.position);

        // 오차가 임계값 이하면 정상 예측 — 보정 불필요
        if (positionError < RECONCILE_THRESHOLD) return;

        // 서버 상태로 복원 후 이후 입력들을 순서대로 재시뮬레이션
        movementHandler.SetState(serverState);

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

        predictedPosition = transform.position;
    }
}
