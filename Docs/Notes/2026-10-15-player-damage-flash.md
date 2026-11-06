# 플레이어 피격 점멸 연출 조사

- 조사일: 2026-10-15
- 범위: 피격 시 `Damaged.png`와 플레이어 색상 점멸

## 결론

현재 구현에는 흰색 → 원래색 → 흰색 → 원래색 점멸이 없다. `Damaged.png`는 흰색 기반 스프라이트지만 `SpriteRenderer.color`에 플레이어 고유 색상이 계속 적용되므로 화면에서는 흰색이 아니라 해당 플레이어 색으로 보인다. 피격 순간에는 이 스프라이트를 최소 0.25초 표시할 뿐 색상 변경은 하지 않는다.

## 현재 호출 흐름

```text
[서버] PlayerController2D.HandleDamage
  → MovementHandler.OnDamaged
  → PlayerStateController.RpcPlayOneShot(Damaged)
[각 클라이언트] PlayerAnimationController.PlayOneShot
  → Animator.Play("Damaged")
```

- `HandleDamage`는 실제 피해가 적용된 경우에만 RPC를 보낸다.
- `Damaged.anim`은 `White/Damaged.png` 한 프레임만 사용한다.
- `PlayerAnimationController`는 최소 0.25초 동안 피격 상태를 유지하지만 `SpriteRenderer.color`는 건드리지 않는다.
- 피격 사운드, 파티클, 카메라 흔들림도 현재 없다.

## 이력에서 확인한 내용

- 초기 `Damaged.anim`은 `SlimeTakeDamage.png`의 5프레임을 12 FPS로 재생했다.
- 2025-01-03에 5프레임 클립이 흰색 기반 `White/Damaged.png` 한 프레임으로 교체됐다. 같은 시기에 플레이어 스프라이트를 흰색 기반으로 바꾸고 `SpriteRenderer.color`로 플레이어 색을 입히는 구조가 들어왔다.
- 저장소 이력에서 피격 시 `Color.white`와 원래색을 번갈아 적용하는 코드나 색상 애니메이션 키는 찾지 못했다.
- 2026-10-11 애니메이션 단순화는 Damaged 상태를 직접 재생하도록 바꿨지만 별도의 점멸 연출을 제거한 것은 아니다. 조사 가능한 이력 기준으로 점멸 자체가 구현되어 있지 않았다.

## Unity 실행 측정

열린 Main 씬에 저장되지 않은 변경이 있어 플레이 모드 대신 격리된 Preview Scene에 `Player.prefab`을 복제하고 실제 `PlayerAnimationController.PlayOneShot(Damaged)`를 호출했다.

- 호출 전: `Base`, `RGBA(0.363, 0.990, 1.000, 1.000)`
- 0ms: `Damaged_0`, `RGBA(0.363, 0.990, 1.000, 1.000)`
- 100ms: `Damaged_0`, `RGBA(0.363, 0.990, 1.000, 1.000)`
- 250ms: `Damaged_0`, `RGBA(0.363, 0.990, 1.000, 1.000)`

결과적으로 로컬 `Damaged` 상태와 피격 스프라이트 전환은 정상이다. 그러나 모든 측정 시점에 플레이어 틴트가 유지되므로 흰색 → 원래색 점멸은 화면에 나오지 않는다.

## 구현 결과

`PlayerAnimationController.PlayOneShot(PlayerState.Damaged)`에서 클라이언트 로컬 점멸을 시작하도록 구현했다.

- `Color.white → 원래색 → Color.white → 원래색` 순서로 전환하며 기본 구간은 0.08초다.
- 다시 피격되면 기존 점멸을 취소하고 처음부터 재생한다.
- `PlayerController2D`의 색상 SyncVar 훅은 `SetPlayerColor`를 거쳐 점멸 중에도 최신 플레이어 색상을 보존한다.
- 컴포넌트가 비활성화되면 점멸을 취소하고 최신 플레이어 색상으로 복원한다.

기존 RPC가 모든 클라이언트에서 같은 피격 이벤트를 실행하므로 점멸 전용 네트워크 상태는 추가하지 않는다. 애니메이션 클립에 색상 키를 넣으면 플레이어별 원래색을 고정값으로 복원하기 어려우므로 코드 방식이 안전하다.

격리된 Unity Preview Scene에서 점멸 이터레이터를 실행한 결과, 초기색에서 첫 흰색으로 바뀌고 점멸 도중 갱신한 새 플레이어 색상으로 복원된 뒤 새 알파값의 흰색과 새 색상으로 다시 전환됐다. 완료와 비활성화 뒤에도 새 색상이 유지됐다.

## 검증 항목

- 서로 다른 플레이어 색상에서 흰색과 정확한 원래색이 두 번 교차하는지 확인한다.
- 피격 표정과 점멸이 함께 보이고 종료 후 현재 이동 애니메이션으로 돌아오는지 확인한다.
- 무적 종료 후 연속 피격, 도착 지점 진입, 비활성화 중 색상이 남지 않는지 확인한다.
- 호스트와 원격 클라이언트 모두 같은 플레이어의 점멸을 보는지 2인 환경에서 확인한다.
