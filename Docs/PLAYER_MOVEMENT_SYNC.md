# 플레이어 이동·상호작용 동기화

- 상태: Active (2인 플레이 검증 전)
- 최종 갱신: 2026-10-11
- 범위: `Controller2D`, `MovementHandler`, `ClientMover`, `ServerMover`, `PlayerLagCompensation`, `PlayerInteraction`, `PlayerController2D`, `PickupObj`, `LocalNetworkTest`

## 현재 상태

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

- 자동: 없음. 게임 코드가 Assembly-CSharp라 EditMode 테스트 어셈블리에서 참조할 수 없다.
- 컴파일: `dotnet build`(런타임·에디터) 오류 0.
- 수동: 아래 항목을 로컬 2인 환경에서 확인한다. 2026-10-11 기준 아직 확인하지 않았다.
  - 걷는 상대 머리 가장자리에 착지하기, 튕기며 상대 머리 위에 머무르기
  - 밟힌 쪽 화면에서 Shrink가 보이는 시점
  - 벽에 대고 달리기·점프하기
  - 벽 옆에서 들기·던지기·내려놓기
  - 들린 상태에서 1.5초 전후로 탈출하기
  - 로프 타기
  - 스프링 밟기
  - 머리 밟기
  - 함정 피격
  - 리셋
  - 운반 중 운반자 퇴장
- 로컬 2인 실행 방법:
  - 에디터에서 `Tools/Flip Friends/Local Network Test/Host`를 고르고 Main 씬에서 Play한다.
  - 빌드는 `"Slime Climb.exe" -localclient -latency 60`으로 실행한다.
  - 양쪽이 각자 송신을 늦추므로 왕복 지연은 약 120ms다. 끝나면 메뉴에서 `Off (Steam)`으로 돌린다.
- 환경 제약: Steam 경로의 최종 확인은 계정 2개가 필요해 이 환경에서 끝까지 할 수 없다.

## 남은 작업

- 수동 검증 결과를 보고 던지기 속도, 탈출 고정 시간, 보정 임계값(0.15)을 조정한다.
- 움직이는 발판이 클라이언트에서 과거 위치로 보이는 문제는 다루지 않았다. 발판 위에서 보정이 잦으면 다음에 본다.
- `Ground`를 Polygons 콜라이더로 바꾸는 것은 보류했다. 겹침 해소로 충분한지 먼저 본다.
- 던진 박스는 아직 표시 보정이 없다. 던진 사람 화면에서 박스가 뒤에서 출발하는 것처럼 보이면 같은 방식을 적용한다.
- 지연 보상 적용 후에도 머리 밟기가 자주 어긋나면 클라이언트 권한 이동을 검토한다. 비교와 전환 비용은 `Docs/Notes/2026-10-11-client-authority-alternative.md`.
- 원격 플레이어가 `ThrownPlayerSmoother`로 그려지는 0.3초 동안은 화면 위치가 NT 보간과 달라 지연 보상과 어긋날 수 있다. 문제가 보이면 그때 다룬다.
- 플레이어 NetworkTransform은 sendRate 60, 버퍼 배수 2로 이미 최소에 가깝다. 남은 어긋남은 대부분 네트워크 지연 자체다.

## 다음 작업

로컬 2인(`-latency 60`)에서 위 수동 검증 항목을 확인하고, 결과에 따라 보정 임계값이나 클라이언트 권한 전환을 정한다.
