# 다음 세션: 플레이어 간 지연 보상 + 올라탄 플레이어 따라가기

- 작성: 2026-10-11
- 기능 문서: `Docs/PLAYER_MOVEMENT_SYNC.md` (먼저 읽는다)
- 규칙: `.claude/rules/network.md`, `.claude/rules/gameplay.md`

## 배경

- 이동은 소유 클라이언트 예측(`ClientMover`) + 서버 권한(`ServerMover`)이다. 원격 플레이어는 `NetworkTransformUnreliable` 보간으로 과거 시점에 그려진다.
- 그래서 움직이는 상대의 머리를 밟거나 그 위에 올라타 이동하면, 내 화면과 서버의 상대 머리 위치가 "상대 속도 × (편도 지연 + 보간 버퍼)"만큼 어긋난다. `-latency 60`에서 약 0.4칸이다. 착지 판정이 갈려 보정이 반복된다.
- 밟기 튕김은 이미 소유 클라이언트도 예측한다(`MovementHandler.PlayerMovementInteract`). 밟힌 쪽 연출은 서버만 낸다.

## 할 일

### 1. 플레이어 간 충돌 지연 보상 (서버)

- 클라이언트가 입력을 만들 때 원격 플레이어를 보고 있던 시점을 `InputPayload`에 담는다.
  - 원격 NT는 `NetworkTime.time`(= `NetworkClient.localTimeline`)으로 보간한다.
  - 스냅샷 타임스탬프에는 `timeStampAdjustment + offset`이 더해진다(`NetworkTransformBase`).
  - 서버 기준 시각으로 환산하는 식을 Mirror 코드로 확인한 뒤 쓴다. 추측으로 쓰지 않는다.
- 서버는 플레이어마다 최근 위치 기록(약 0.5초)을 링 버퍼로 둔다. 기록하는 쪽은 서버 시각(`NetworkTime.localTime`)과 위치다.
- `ServerMover`가 원격 클라이언트의 입력 하나를 `Simulate`하기 직전에:
  - 다른 플레이어들의 transform을 그 입력의 보던 시점 위치로 잠깐 옮기고 `Physics2D.SyncTransforms()`를 부른다.
  - 시뮬레이션이 끝나면 원래 위치로 되돌리고 다시 동기화한다.
- 되돌리는 대상은 플레이어만이다. 지형·발판·박스는 건드리지 않는다.
- 운반 중인 플레이어(부모가 있는 경우)는 부모를 따라가므로 따로 옮기지 않는다.
- 호스트 자신의 플레이어는 서버 현재 시점을 보고 있으므로 되돌리지 않는다.
- 밟힌 쪽 판정(`OnSteppedByOtherPlayer`)도 되돌린 위치 기준으로 일어나야 한다.

### 2. 밟고 있는 플레이어를 움직이는 발판처럼 따라가기 (예측·서버 공통)

- `Controller2D`가 아래로 쏜 레이에 플레이어가 걸려 서 있는 상태(밟기 튕김이 아닌 상태)를 구분한다.
- `MovementHandler.ApplyMovement`에서 그 플레이어가 지난 틱 이후 움직인 만큼(`platformDelta`처럼) 더해 같이 움직인다.
  - 서버는 1번의 되돌린 위치 기준 이동량을 쓴다.
  - 클라이언트는 화면에 보이는 원격 플레이어의 이동량을 쓴다.
- 현재 밟기 튕김 조건(`underPlayer` + `isJumpBlocked`)과 "서 있기"가 충돌하지 않게 정리한다. 지금은 머리에 닿으면 바로 튕긴다. 서 있게 할 조건(예: 점프 입력 없이 착지, 상대 머리 위에서 정지)을 코드에서 확인하고 정한다. 바뀌는 게임플레이가 있으면 사용자에게 먼저 묻는다.

## 대안: 클라이언트 권한 이동 (피코파크식 추정)

1·2를 적용해도 머리 밟기 감각이 만족스럽지 않을 때 검토한다. 피코파크의 공개 정보는 Unity, Steam 네트워크, 호스트 방식뿐이다. 아래 구조는 추정이다.

| | 현재 + 1·2 (서버 권한 + 예측 + 지연 보상) | 클라이언트 권한 이동 |
|---|---|---|
| 내 캐릭터 | 즉각 반응, 드물게 보정 | 항상 즉각, 보정 없음 |
| 머리 밟기·올라타기 | 서버가 내 화면 시점으로 되돌려 판정 | 각자 자기 화면 기준으로 판정 |
| 화면 일치 | 결과는 서버가 통일 | 상대 화면에서는 어긋나 보일 수 있음 |
| 치트 방지 | 있음 | 없음 (친구 협동이라 영향 작음) |
| 유지 비용 | 높음. 결정적 시뮬레이션, `StatePayload` 완전성, 에포크 관리 | 낮음 |

전환 비용(코드 기준 추정):

- 없앨 것: `ClientMover`의 재시뮬레이션·보정, `ServerMover`의 입력 큐·에포크, `StatePayload`의 대부분.
- 바꿀 것: 소유자가 `Simulate` 결과 위치·속도·상태를 보내고, 서버는 범위 검사 후 그대로 퍼뜨린다. `NetworkTransform`을 ClientToServer로 바꾸는 것도 방법이다.
- 다시 설계할 것:
  - 들기·던지기. 서버가 정하는 대신 소유권이 바뀐다. 운반 중에는 운반자가 위치를 정하고, 던질 때 던져지는 쪽 소유자에게 시작 위치·속도를 넘긴다.
  - 피격·리셋. 서버가 판정해 소유자에게 이벤트로 보낸다.
  - 밟힌 쪽 연출. 밟은 쪽이 판정해 서버로 알린다.
- 그대로 쓸 것: `Controller2D` 충돌·겹침 해소, `MovementHandler.Simulate`, 로컬 2인 테스트, `ThrownPlayerSmoother`.

판단 기준: 1·2 적용 후 로컬 2인(`-latency 60`)에서 걷는 상대 위 착지·올라타기가 자주 어긋나면 전환을 사용자와 상의한다.

## 지켜야 할 것

- 판정 위치는 서버가 정한다. 표시 보정(`ClientMover.visualOffset`, `ThrownPlayerSmoother`)과 섞지 않는다.
- 시뮬레이션에 영향을 주는 새 값은 `StatePayload`에 넣는다. 재시뮬레이션 결과가 서버와 같아야 한다.
- 플레이어 `transform.position`을 서버에서 직접 바꾸는 건 1번의 임시 되돌리기만 허용한다. 반드시 같은 틱 안에서 복원한다.
- 프리팹을 에디터 API로 저장하면 Mirror `_assetId`가 0이 될 수 있다. `LoadAssetAtPath` + `SavePrefabAsset`을 쓰고 diff를 확인한다.

## 검증

- 컴파일: `dotnet build Assembly-CSharp.csproj`, 에디터 콘솔 오류 0
- 1인: 에디터 Host로 Idle·이동·점프가 그대로인지(UnityMCP `execute_code`로 상태 확인 가능)
- 2인: `Tools/Flip Friends/Local Network Test/Host` + 빌드 `-localclient -latency 60`. 사용자에게 확인을 요청한다.
  - 걷는 상대 머리 가장자리에 착지하기
  - 걷는 상대 위에 올라타 같이 이동하기
  - 밟기 튕김
  - 밟힌 쪽 화면이 어떻게 보이는지
- 끝나면 `Docs/PLAYER_MOVEMENT_SYNC.md`의 현재 상태·검증·남은 작업을 갱신하고 이 파일을 지운다.
