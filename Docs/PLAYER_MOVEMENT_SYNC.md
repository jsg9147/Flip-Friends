# 플레이어 이동·상호작용 동기화

- 상태: Active (Quantum 개발2인 검증 완료, 실환경 다인 검증 미완료)
- 문서 정리: 2026-11-08 (실행 검증 날짜는 검증 절 참조)
- 범위: `Controller2D`, `MovementHandler`, `ClientMover`, `ServerMover`, `PlayerLagCompensation`, `PlayerInteraction`, `PlayerController2D`, `PickupObj`, `LocalNetworkTest`

## 현재 상태

- Quantum 제품8에 실제 Player.prefab의30도·상자45도 경사를 이식했다. 접선 속도·양방향 오르내림·정지·점프·급경사 미끄럼/법선 점프를 FP로 처리한다. 천장/운반물/몸체 검사 뒤에만 경사 상승을 반영하며 기존8raw 미세 단차와 역할을 분리했다. [지원 범위·검증](Notes/2026-11-08-quantum-slopes.md).

- Quantum 제품6은 실제 FP 윤곽 지형을 이동·놓기/복귀/소환·Shrink 복원 점유에서 검사한다. 축소 운반자의 크기를 들린 대상 대신 사용하던 천장 충돌 누락을 실제 대상 HalfSize/Offset으로 수정했다. 이동 수치·점프·로프18틱·운반 규칙은 유지한다. [재현·검증](Notes/2026-11-08-quantum-tilemap.md).

- Quantum 제품5의 구간 Reset은 플레이어/상자 운반 관계를 유지하고 전체 목적지가 막히면 두 대상을 함께 대기·복귀시킨다. 대기 입력/탈출/직접Reset·그룹상자 소환은 소비/거부한다. 수동Reset/추락은 DropAll 후 플레이어별 체크포인트/최초 위치로 복귀한다. 소환 성공만 관계·속도·쿨다운을 원자적으로 갱신한다. [장치 구현·검증](Notes/2026-11-08-quantum-devices.md)을 따른다.
- Quantum 제품5에 수동 Reset·플레이어/상자 추락 복귀와 프레임 초기 위치/입력엣지·원자적 관계/속도/타이머 정리를 추가했다. 들린 플레이어 직접 복귀는 소비·무시하며, 놓기 불가/목적지 점유에는 안전 복귀 대기를 쓴다. 고정 Reset은1P R·2P Backspace, 재설정/패드는 미연결이다. [복귀 구현·검증](Notes/2026-11-08-quantum-respawn.md)을 따른다.
- Quantum 제품 경로에 밟기·운반/탈출·던지기/놓기·상자 충돌/중력을 이식했다. 관계/타이머/Shrink 판정은 프레임에 두고 PlayerRef 순서로 처리한다. 제품5/ff-product-5, 기존 상자 콘텐츠3:3이며 기존 정적/로프2:2와 이동/로프18틱을 보존한다. 기본 Quantum 판정과 Mirror 루트 판정 차이·일반 점프에 미연결인 BlockJump·안전한 놓기 정책은 [상세 기록](Notes/2026-11-07-quantum-interactions.md)을 따른다. 일반 장치·전체 스테이지는 미이식이다.
- 현재 키는 1P A/D·W/S·Space·왼쪽 Shift·E, 2P 방향키·오른쪽 Ctrl·오른쪽 Shift·Enter다. 기존 단일 Player 액션과 `bindings` 저장은 로컬 2슬롯에 미연결이며 키 재설정·게임패드를 지원한다고 표시하지 않는다. Jump/Run/Interact·아래 조합과 이동·상호작용 상태를 기록/재생한다.
- Mirror 발판은 시작 시각 기반으로 위치를 평가한다. 입력의 platformTime과 상태의 탑승 부품/세대/시각으로 운반 이동을 Simulate에서 처리하며 재시뮬레이션도 같은 발판 위치·충돌을 사용한다. 기존 발판의 외부 Controller2D.Move 호출은 제거했다.
- Mirror 리셋: Reset 태그 접촉은 서버 RespawnHandler가 구간 목적지로 Teleport하며 운반 상태를 유지한다. 리셋 키·추락은 기존처럼 DropAll 후 체크포인트/시작점으로 복귀한다. 체크포인트는 서버에서 더 큰 ID만 받는다.
- 상자 재소환은 PickupObj.ServerReset으로 운반자 참조·표시·충돌 무시·속도·동기화 위치를 초기화한다. 기본 스테이지 자식 상자 참조는 루트 Identity의 부품 참조로 전송한다.
- **이동**:
  - 소유 클라이언트가 `ClientMover`로 예측한다. 서버는 `ServerMover`가 입력 하나당 `Simulate`를 한 번 돌린다.
  - 밀린 입력은 한 틱에 둘까지 처리한다. 입력이 약 0.2초 끊기면 마지막 입력으로 계속 움직인다.
  - `StatePayload`에는 다음 틱에 영향을 주는 값이 모두 들어 있다: 위치, 속도, 외부 속도, 스무딩, 접지, 등반, 방향, 코요테 시간, 각 타이머.
- **순간이동**:
  - 서버가 위치를 정할 때(운반 해제, 리셋, `RespawnHandler`)는 `ServerMover.Teleport`를 쓴다.
  - 이때 에포크가 바뀌어 이전 입력과 보정값을 버리고, 소유자는 `TargetResync`로 받은 상태에서 예측을 새로 시작한다.
- **관통 방지**:
  - 레이 원점을 `transform` 기준으로 계산하고, 가장 가까운 충돌로 레이를 줄인다.
  - `Move` 전에 `ResolvePenetration`으로 겹침을 푼다.
  - 플레이어 `Rigidbody2D`는 Kinematic이다.
- **플레이어 간 지연 보상**:
  - 소유 클라이언트는 마지막으로 그린 화면의 원격 플레이어 시각을 `InputPayload.viewTime`에 담는다. 식은 `NetworkTime.time - (timeStampAdjustment + offset)`이다. NT가 스냅샷을 `서버 시각 + timeStampAdjustment + offset`에 넣고 `NetworkTime.time`으로 보간하기 때문이다.
  - 클라이언트는 예측 전에 `Physics2D.SyncTransforms()`로 원격 콜라이더를 화면 위치에 맞춘다.
  - 서버는 `LateUpdate`마다 플레이어 위치를 `NetworkTime.localTime`과 함께 기록한다(`PlayerPositionHistory`). NT가 보내는 시각·위치와 같다.
  - 원격 클라이언트 입력 하나를 `Simulate`하는 동안만 다른 플레이어를 `viewTime` 위치로 옮기고 끝나면 되돌린다(`PlayerLagCompensation`). 최대 0.5초이고, 운반 중이거나 도착 지점에 들어간 플레이어는 옮기지 않는다. 호스트 자신의 입력은 되돌리지 않는다.
  - 밟기 튕김과 밟힌 쪽 연출은 되돌린 위치 기준으로 판정된다.
- **머리 밟기**: 머리에 닿으면 항상 튕긴다. 서 있는 상태는 두지 않는다. 튕기면서 상대 머리 위에 계속 머무르게 조작하는 것이 재미 요소다(사용자 결정, 2026-10-11).
- **서버만 판정하는 것**: 피격, 들기·던지기. 피격은 `SendStateNow`로 소유자에게 바로 보정값을 보낸다.
- **양쪽이 같이 적용하는 것**:
  - 로프 진입, 스프링, 바운스, 밟기 튕김은 서버와 소유 클라이언트가 같이 적용한다.
  - 밟힌 쪽 연출(Shrink, 점프 차단)은 서버만 낸다. 판정이 갈리면 서버 보정이 덮어쓴다.
- **보정 표시**:
  - 위치 오차 0.15 이상이거나 속도 오차 1 이상이면 재시뮬레이션한다.
  - 그때 생긴 위치 차이와 운반 해제 시의 차이는 보이는 위치만 약 0.1초에 걸쳐 따라간다(`ClientMover.visualOffset`). 2 이상 떨어지면 바로 옮긴다.
  - 운반에서 풀려난 **원격** 플레이어는 `ThrownPlayerSmoother`가 화면에 보이던 자리에서 던진 궤적을 직접 그리고, 0.3초 동안 NetworkTransform 보간 위치로 넘긴다. 원격 플레이어가 보간 때문에 과거 시점에 그려져 "뒤에서 날아오는" 것처럼 보이는 문제를 막는다.
- **운반**:
  - 들린 플레이어는 모든 화면에서 운반자의 자식(`heldPos`)으로 붙는다. 서버 시뮬레이션과 클라이언트 예측은 멈춘다.
  - `carryLockDuration`(기본 1.5초)이 지난 뒤 이동이나 점프 입력이 들어오면 운반자가 보는 방향 앞 대각선으로 던져진다(`playerThrowVelocity`).
- **놓기**:
  - 들기 키를 누르면 던지고, 아래 방향과 함께 누르면 내려놓는다.
  - 놓을 위치는 `CanOccupy`로 고른다: 앞 → 머리 위 → 운반자 자리 순서다.
  - 피격, 리셋, 도착 지점 진입, 운반자 퇴장 시에는 들고 있던 것을 내려놓는다.
- **박스**: 서버가 위치·속도를 정한다(`PickupObj.Release`). 클라이언트는 위치 SyncVar를 보간해 따라간다.

## 핵심 흐름

```text
[소유 클라] ClientMover → Simulate(예측) → CmdSendInput(epoch, seq)
[서버] ServerMover.ReceiveInput(에포크 검사) → Rewind(viewTime) → Simulate → Restore → 3틱마다 TargetSendState
[서버] PlayerInteraction.Release → PlayerController2D.EndCarried → RpcEndCarried → ServerMover.Teleport → TargetResync
```

## 검증

- Quantum 제품5의 이동·로프·밟기/운반·복귀·장치 재생과 Main/Photon 검증은 [공통 검증 요약](QUANTUM_MIGRATION.md#검증)을 따른다. 세부 수치·실패 분석은 [장치 기록](Notes/2026-11-08-quantum-devices.md)과 해당 이동/상호작용 Notes에 있다.
- Mirror: 2026-10-26 KCP 지연0/60ms의 Stage1~21 생성·API 완료·씬 왕복과 상자 복귀/소환을 확인했다. 실조작 전체 클리어·연속 발판/운반은 별도 미실행이다. [실행 범위](Notes/2026-10-26-stage-live-validation.md).
- Mirror 상태/발판/Weaver 격리 검사는 [과거 상세 검증](Notes/2026-11-08-documentation-reference-history.md)에 보존했다. 일반 EditMode asmdef는 Assembly-CSharp를 직접 참조하지 않으며 에디터 검사 메뉴를 사용한다.
- Mirror 로컬2인: Tools/Flip Friends/Local Network Test/Host, 빌드는 -localclient -latency 60. 양쪽 송신 지연으로 RTT 약120ms다. Steam 실계정 검증과 구분한다.

## 남은 작업

- Quantum: 최대4인·키 재설정/게임패드·피격/스프링/바운스/발판·실제 스테이지 연결은 [전환 순서](NEXT_SESSION.md)를 따른다.
- Mirror 미완료 실조작: 연속 발판 탑승/점프 분리·머리 위 장애물/리셋·운반/밟기·피격·운반자 이탈과 지연 조건을 확인한다. 수치 변경은 재현 근거를 확보한 뒤 판단한다.
- Mirror의 원격 과거 위치와 트랩 predictedTime 차이, 던진 상자 표시 보정, 해제 후0.3초 보간/지연 보상 차이는 관찰할 위험이다. 현재 결함으로 단정하지 않는다.
- Ground 콜라이더 전면 교체와 클라이언트 권한 이동 대안은 보류된 과거 검토다. Quantum 전환과 분리하며 필요할 때만 [당시 대안](Notes/2026-10-11-client-authority-alternative.md)을 확인한다.

## 다음 작업

[최신 착수 순서](NEXT_SESSION.md#다음-작업)에 따라 기존 스테이지의 Tilemap/장치 판정을 작은 묶음부터 이식하고 해당 영역의 동작 문제와 회귀를 함께 확인한다.
