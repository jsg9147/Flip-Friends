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

    private MovementHandler movementHandler;
    private ClientMover clientMover;

    private readonly Queue<InputPayload> inputQueue = new Queue<InputPayload>();

    // 입력이 없는 프레임에 방향/달리기 상태를 유지하기 위한 마지막 입력
    private InputPayload lastInput;

    private uint lastProcessedSeq = 0;
    private int sendCounter = 0;

    private void Awake()
    {
        movementHandler = GetComponent<MovementHandler>();
        clientMover = GetComponent<ClientMover>();
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        // ServerMover가 Simulate를 직접 호출하므로 MovementHandler.FixedUpdate 자동 실행 비활성화
        movementHandler.managedExternally = true;
    }

    // 포지션 리셋 등 순간 이동 시 큐에 쌓인 오래된 입력 전부 제거
    public void ClearInputQueue()
    {
        inputQueue.Clear();
    }

    // ClientMover.CmdSendInput에서 서버 측으로 호출됨
    public void ReceiveInput(InputPayload input)
    {
        // 호스트 자신의 입력은 ClientMover가 로컬에서 처리 — 서버에서 중복 처리 방지
        if (isOwned) return;

        if (inputQueue.Count >= MAX_QUEUE_SIZE)
            inputQueue.Dequeue();

        inputQueue.Enqueue(input);
    }

    private void FixedUpdate()
    {
        if (!isServer || isOwned) return;

        ProcessInput();
        SendStateIfNeeded();
    }

    private void ProcessInput()
    {
        if (inputQueue.Count > 0)
        {
            lastInput = inputQueue.Dequeue();
            lastProcessedSeq = lastInput.sequenceNumber;
            movementHandler.ApplyInput(lastInput);
        }
        else
        {
            // 새 입력 없음 — 방향/달리기는 유지하되 점프 같은 순간 이벤트는 제거
            InputPayload continuation = lastInput;
            continuation.jump   = false;
            continuation.jumpUp = false;
            movementHandler.ApplyInput(continuation);
        }

        movementHandler.Simulate(Time.fixedDeltaTime);
    }

    private void SendStateIfNeeded()
    {
        sendCounter++;
        if (sendCounter < SEND_INTERVAL) return;

        sendCounter = 0;

        // connectionToClient: 이 플레이어를 소유한 클라이언트의 연결
        if (connectionToClient == null) return;

        StatePayload state = movementHandler.GetState(lastProcessedSeq);
        clientMover.TargetSendState(connectionToClient, state);
    }
}
