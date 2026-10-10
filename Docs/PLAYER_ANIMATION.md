# 플레이어 애니메이션 상태 동기화

- 상태: Active
- 최종 갱신: 2026-10-11
- 범위: `PlayerStateController`, `PlayerAnimationController`, `PlayerAnimator.controller`

## 현재 상태

- 지속 상태(Idle, Walk, Jump, Climb, ClimbIdle, Carried)는 서버가 `PlayerController2D.UpdatePlayerState`에서 매 틱 이동 결과로 정하고 `PlayerStateController`의 SyncVar로 보낸다. 값이 바뀔 때만 전송되고 늦게 들어온 클라이언트도 현재 값을 받는다.
- 순간 연출(Damaged, Shrink)은 `RpcPlayOneShot`으로 보낸다. SyncVar에 실으면 같은 틱의 다음 상태에 덮여 클라이언트가 놓칠 수 있다. 피격은 `MovementHandler.OnDamaged`가 실제로 피해를 적용했을 때만 보낸다.
- 소유 클라이언트는 이동 계열 상태(Idle, Walk, Jump)를 예측 이동 결과로 직접 계산해 서버 왕복 지연 없이 보여 준다. 등반·운반은 서버 값을 따른다.
- `PlayerAnimator.controller`는 상태 6개(Idle, Walk, Jump, Climb, Shrink, Damaged)만 있고 전이·파라미터가 없다. `PlayerAnimationController`가 상태가 바뀔 때만 `Animator.Play`로 재생한다. ClimbIdle은 Climb 클립을 `animator.speed = 0`으로 멈춘다.
- 1회성 클립은 끝까지(최소 `minOneShotDuration` 0.25초) 재생한 뒤 지속 상태로 돌아간다.
- `NetworkAnimator`는 쓰지 않는다. Shrink 클립이 BoxCollider2D 크기·오프셋을 바꾸므로 Write Defaults는 켜 둔다.
- 공중 상태는 모두 Jump 클립이다. `Falling.anim`은 남아 있지만 컨트롤러에 연결되어 있지 않다.

## 핵심 흐름

```text
[서버] PlayerController2D.UpdatePlayerState → PlayerStateController.ChangeState (SyncVar)
[서버] HandleDamage / OnSteppedByOtherPlayer → PlayerStateController.RpcPlayOneShot
[클라] PlayerAnimationController.Update → PlayerStateController.GetDisplayState → Animator.Play
```

## 검증

- 자동: `dotnet build Assembly-CSharp.csproj` 통과.
- 수동(1인, 확인 완료): 대기·걷기·점프·등반(줄에서 멈춤 포함), 트랩 피격, 밟힘 후 콜라이더 복원, 운반.
- 수동(미확인, 환경 제약): 2인 이상에서 다른 플레이어 애니메이션 동기화와 늦게 들어온 클라이언트의 초기 상태. Steam 2계정이 필요하다.

## 남은 작업

- 2인 이상 동기화 수동 검증.

## 다음 작업

- 2인 이상 환경에서 위 수동 검증을 진행한다.
