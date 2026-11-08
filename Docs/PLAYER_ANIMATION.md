# 플레이어 애니메이션 상태 동기화

- 상태: Active
- 문서 정리: 2026-11-08 (실행 검증 날짜는 검증 절 참조)
- 범위: `PlayerStateController`, `PlayerAnimationController`, `PlayerAnimator.controller`

## 현재 상태

- Quantum 구간 운반 대기는 두 대상을 숨기고 프레임 관계를 유지한다. 복귀 시 운동/접촉/Shrink 타이머를 정리하며 버튼 Sprite/남은 쿨다운도 프레임 상태만 표시한다. [장치 구현·검증](Notes/2026-11-08-quantum-devices.md)을 따른다.

- Quantum 복귀 시 Shrink/운반/등반/접촉을 프레임에서 정리하고 복귀 대기 중 표시를 숨긴다. Animator/Unity 콜라이더는 복귀 판정에 사용하지 않는다. [복귀 구현·검증](Notes/2026-11-08-quantum-respawn.md)을 따른다.

- Quantum 제품의 Shrink는30틱과 크기(0.8,0.5)/오프셋(0,-0.25)을 프레임에 저장하고 눌린 Sprite를 표시한다. 기존 Quantum 기본 판정으로 복원하며 Animator/Unity 콜라이더를 판정에 쓰지 않는다. Mirror 애니메이션 경로는 아래와 같이 보존한다. [실제 차이·검증](Notes/2026-11-07-quantum-interactions.md).
- 아래 서버/SyncVar/Animator 설명은 기존 Mirror 경로다. Quantum의 전체 애니메이션·피격 연출 연결은 후속 작업이다.
- 바라보는 방향은 지상·공중 모두 서버가 받은 좌우 입력으로 정하며, 입력이 없으면 유지한다. 피격 시에는 넉백의 X 방향을 바라보고 조작 제한 중에는 입력으로 바뀌지 않는다. 수직 피격은 기존 방향을 유지한다.
- 지속 상태(Idle, Walk, Jump, Climb, ClimbIdle, Carried)는 서버가 `PlayerController2D.UpdatePlayerState`에서 매 틱 이동 결과로 정하고 `PlayerStateController`의 SyncVar로 보낸다. 값이 바뀔 때만 전송되고 늦게 들어온 클라이언트도 현재 값을 받는다.
- 순간 연출(Damaged, Shrink)은 `RpcPlayOneShot`으로 보낸다. SyncVar에 실으면 같은 틱의 다음 상태에 덮여 클라이언트가 놓칠 수 있다. 피격은 `MovementHandler.OnDamaged`가 실제로 피해를 적용했을 때만 보낸다.
- 소유 클라이언트는 이동 계열 상태(Idle, Walk, Jump)를 예측 이동 결과로 직접 계산해 서버 왕복 지연 없이 보여 준다. 등반·운반은 서버 값을 따른다.
- `PlayerAnimator.controller`는 상태 6개(Idle, Walk, Jump, Climb, Shrink, Damaged)만 있고 전이·파라미터가 없다. `PlayerAnimationController`가 상태가 바뀔 때만 `Animator.Play`로 재생한다. ClimbIdle은 Climb 클립을 `animator.speed = 0`으로 멈춘다.
- 1회성 클립은 끝까지(최소 `minOneShotDuration` 0.25초) 재생한 뒤 지속 상태로 돌아간다.
- 피격 시 `Damaged.png` 한 프레임과 함께 로컬 색상을 `흰색 → 원래색 → 흰색 → 원래색`으로 0.08초 간격 점멸한다. 점멸 중 플레이어 색상이 동기화되면 다음 복원 단계부터 최신 색상을 사용하며, 연속 피격·비활성화 시에도 최신 색상으로 복원한다. 조사와 구현 결과는 [`Notes/2026-10-15-player-damage-flash.md`](Notes/2026-10-15-player-damage-flash.md)에 정리했다.
- `NetworkAnimator`는 쓰지 않는다. Shrink 클립이 BoxCollider2D 크기·오프셋을 바꾸므로 Write Defaults는 켜 둔다.
- 공중 상태는 모두 Jump 클립이다. `Falling.anim`은 남아 있지만 컨트롤러에 연결되어 있지 않다.

## 핵심 흐름

```text
[서버] PlayerController2D.UpdatePlayerState → PlayerStateController.ChangeState (SyncVar)
[서버] HandleDamage / OnSteppedByOtherPlayer → PlayerStateController.RpcPlayOneShot
[클라] PlayerAnimationController.Update → PlayerStateController.GetDisplayState → Animator.Play
```

## 검증

- Quantum Shrink 판정·운반/복귀 대기 표시와 재생/Photon 결과는 [공통 검증](QUANTUM_MIGRATION.md#검증)을 따른다. Sprite 축소 표시이며 기존 Shrink 클립 전체 재생 이식은 아니다.
- Mirror: 피격 점멸의 최신 색상 복원·비활성화 정리·1인 이동/등반/피격/밟힘/운반을 확인했다. [측정·재현](Notes/2026-10-15-player-damage-flash.md).
- 실제 다인 애니메이션·피격 색상/방향 동기화는 미검증이다. 이전 세부 측정과 빌드 기록은 [과거 상세 검증](Notes/2026-11-08-documentation-reference-history.md)에 보존한다.

## 남은 작업

- 2인 이상 동기화 수동 검증.

## 다음 작업

- 실제 게임 화면에서 서로 다른 플레이어 색상과 2인 이상 환경의 피격 점멸을 수동 검증한다.
