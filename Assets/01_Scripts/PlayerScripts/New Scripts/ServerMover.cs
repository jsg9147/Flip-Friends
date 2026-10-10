using System.Collections.Generic;
using Mirror;
using UnityEngine;

/// <summary>
/// 서버에서 클라이언트 입력을 받아 물리를 실행하고 보정값을 전송한다.
/// isOwned 플레이어(호스트 자신)는 ClientMover가 담당하므로 건너뛴다.
/// </summary>
[DefaultExecutionOrder(-5)] // ClientMover(-10)보다 늦게, PlayerController2D(0)보다 먼저
public class ServerMover : NetworkBehaviour
{
    // 입력이 한 프레임에 몰릴 때 보관할 최대 수 — 초과 시 가장 오래된 것부터 버림
    private const int MAX_QUEUE_SIZE = 32;

    // N 프레임마다 한 번 서버 상태를 클라이언트에 전송
    private const int SEND_INTERVAL = 3;

    private const int MaxInputsPerTick = 2;
    // 이 틱 수(약 0.2초) 동안 입력이 없으면 연결이 밀린 것으로 보고 마지막 입력으로 계속 움직인다.
    private const int StarvationTicks = 10;
    private int ticksWithoutInput;

    // 서버 프레임이 빨라도 PlayerLagCompensation.MaxRewindTime(0.5초)을 덮도록 넉넉히 둔다.
    private const int HistoryCapacity = 256;

    private MovementHandler movementHandler;
    private ClientMover clientMover;
    private BoxCollider2D boxCollider;
    private readonly PlayerPositionHistory positionHistory = new PlayerPositionHistory(HistoryCapacity);

    private readonly Queue<InputPayload> inputQueue = new Queue<InputPayload>();

    // 입력이 없는 프레임에 방향/달리기 상태를 유지하기 위한 마지막 입력
    private InputPayload lastInput;

    private uint lastProcessedSeq = 0;
    private int sendCounter = 0;

    // 순간이동할 때마다 올린다. 이전 에포크 입력은 순간이동 전 위치를 기준으로 만든 것이라 버린다.
    private ushort epoch;
    // 새 에포크의 입력을 하나도 처리하지 않았으면 lastProcessedSeq가 옛 값이라 보정값을 보내면 안 된다.
    private bool hasInputInEpoch;

    private void Awake()
    {
        movementHandler = GetComponent<MovementHandler>();
        clientMover = GetComponent<ClientMover>();
        boxCollider = GetComponent<BoxCollider2D>();
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        // ServerMover가 Simulate를 직접 호출하므로 MovementHandler.FixedUpdate 자동 실행 비활성화
        movementHandler.managedExternally = true;
        PlayerLagCompensation.Register(this);
    }

    public override void OnStopServer()
    {
        PlayerLagCompensation.Unregister(this);
        base.OnStopServer();
    }

    // 지연 보상에서 viewTime 시점의 위치로 옮길 수 있으면 true.
    // 운반 중이면 운반자를 따라가고, 도착 지점에 들어가 콜라이더가 꺼져 있으면 판정에 끼지 않으므로 옮기지 않는다.
    public bool TryGetRewindPosition(double viewTime, out Vector2 position)
    {
        position = default;
        if (transform.parent != null || !boxCollider.enabled) return false;
        return positionHistory.TrySample(viewTime, out position);
    }

    // ClientMover.CmdSendInput에서 서버 측으로 호출됨
    public void ReceiveInput(InputPayload input)
    {
        // 호스트 자신의 입력은 ClientMover가 로컬에서 처리 — 서버에서 중복 처리 방지
        if (isOwned) return;
        if (input.epoch != epoch) return;

        if (inputQueue.Count >= MAX_QUEUE_SIZE)
            inputQueue.Dequeue();

        inputQueue.Enqueue(input);
    }

    // 다음 FixedUpdate에서 바로 보정값을 보낸다. 피격·밟기처럼 서버만 아는 사건을 소유자에게 빨리 알리기 위함이다.
    public void SendStateNow() => sendCounter = SEND_INTERVAL;

    // 운반 해제·리스폰처럼 서버가 위치를 정하는 경우. 소유 클라이언트는 이 상태에서 예측을 새로 시작한다.
    [Server]
    public void Teleport(Vector2 position, Vector2 velocity, float uncontrollableDuration = 0f)
    {
        transform.position = position;
        movementHandler.Launch(velocity, uncontrollableDuration);
        // 순간이동 위치의 발밑 정보를 다시 구해 첫 틱이 이전 위치의 접지 상태를 쓰지 않게 한다.
        StatePayload state = movementHandler.GetState();
        movementHandler.SetState(state);

        epoch++;
        inputQueue.Clear();
        lastInput = default;
        hasInputInEpoch = false;
        ticksWithoutInput = 0;

        state.epoch = epoch;
        if (isOwned)
            clientMover.ApplyResync(state);
        else if (connectionToClient != null)
            clientMover.TargetResync(connectionToClient, state);
    }

    private void FixedUpdate()
    {
        if (!isServer || isOwned) return;

        // 운반되거나 도착 지점에 들어가 이동이 꺼진 동안은 시뮬레이션하지 않는다.
        if (!movementHandler.enabled)
        {
            inputQueue.Clear();
            return;
        }

        ProcessInput();
        SendStateIfNeeded();
    }

    // 입력 하나에 시뮬레이션 한 번을 맞춘다. 입력 없이 서버 시계로 돌리면 서버가 예측보다 틱을 더 밟아 보정이 계속 생긴다.
    private void ProcessInput()
    {
        if (inputQueue.Count == 0)
        {
            ticksWithoutInput++;
            if (ticksWithoutInput < StarvationTicks) return;

            // 입력이 오래 끊기면 마지막 입력으로 계속 움직여 다른 화면에서 멈춰 보이지 않게 한다.
            // 방향/달리기는 유지하되 점프 같은 순간 이벤트는 제거
            // 보던 시점도 틱만큼 흘려 다른 플레이어가 멈춘 것처럼 판정되지 않게 한다.
            lastInput.viewTime += Time.fixedDeltaTime;
            InputPayload continuation = lastInput;
            continuation.jump   = false;
            continuation.jumpUp = false;
            SimulateInput(continuation);
            return;
        }

        ticksWithoutInput = 0;
        // 지연이 흔들려 입력이 몰려 오면 한 틱에 둘까지 처리해 밀린 만큼 따라잡는다.
        int inputsThisTick = inputQueue.Count > 1 ? MaxInputsPerTick : 1;
        for (int i = 0; i < inputsThisTick; i++)
        {
            lastInput = inputQueue.Dequeue();
            lastProcessedSeq = lastInput.sequenceNumber;
            hasInputInEpoch = true;
            SimulateInput(lastInput);
        }
    }

    // 이 클라이언트가 입력을 만들 때 보던 위치에 다른 플레이어를 두고 시뮬레이션한다. 밟기 판정·밟힌 쪽 연출도 그 위치 기준이다.
    private void SimulateInput(InputPayload input)
    {
        movementHandler.ApplyInput(input);
        PlayerLagCompensation.Rewind(this, input.viewTime);
        try
        {
            movementHandler.Simulate(Time.fixedDeltaTime);
        }
        finally
        {
            PlayerLagCompensation.Restore();
        }
    }

    // NetworkTransform이 LateUpdate에서 보내는 것과 같은 시각·위치를 기록한다. 원격 화면은 이 값을 보간해 그린다.
    // 실행 순서가 NetworkTransform보다 빠르지만 그 사이에 위치를 바꾸는 코드가 없어 같은 값이다.
    private void LateUpdate()
    {
        if (!isServer) return;
        positionHistory.Record(NetworkTime.localTime, transform.position);
    }

    private void SendStateIfNeeded()
    {
        sendCounter++;
        if (sendCounter < SEND_INTERVAL) return;
        if (!hasInputInEpoch) return;

        sendCounter = 0;

        // connectionToClient: 이 플레이어를 소유한 클라이언트의 연결
        if (connectionToClient == null) return;

        StatePayload state = movementHandler.GetState(lastProcessedSeq);
        state.epoch = epoch;
        clientMover.TargetSendState(connectionToClient, state);
    }
}
