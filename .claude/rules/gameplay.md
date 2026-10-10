---
paths:
  - "Assets/01_Scripts/ObstacleScripts/**/*.cs"
  - "Assets/01_Scripts/PlayingScripts/**/*.cs"
  - "Assets/01_Scripts/GameObjScripts/**/*.cs"
  - "Assets/01_Scripts/PlayerScripts/**/*.cs"
---

# 게임플레이 규칙

## 충돌

Rigidbody 물리가 아니라 레이캐스트(`Controller2D`, `RaycastController`)로 판정한다. `Controller2D.collisions`가 이동 판단의 기준이다. 충돌 문제를 고칠 때 Rigidbody 설정을 먼저 의심하지 않는다.

## 기능 추가 패턴

기존 코드를 고치지 않고 확장한다. 자식 클래스는 부모를 그대로 대체할 수 있어야 한다.

- **장애물**: `BasicTrap`(`ObstacleScripts/`)을 상속한다. 방사형이 아닌 넉백은 Inspector의 `knockbackDir`을 설정한다. 콜라이더 태그를 `"Trap"` 또는 `"Enemy"`로 두어 `PlayerController2D`의 피해 처리가 잡게 한다.
- **스위치**: `Switch` 또는 `LayerBasedSwitch`(`PlayingScripts/SwitchScripts/`)를 상속하고 `OnSwitchStateChanged`를 재정의한다. `isActivated`는 `SyncVar`로 동기화된다.
- **플레이어 상태**: `PlayerController2D`의 `PlayerState`에 값을 추가하고 `PlayerStateController`, `PlayerAnimationController`를 함께 고친다. 지속 상태는 서버가 `ChangeState`(SyncVar)로 정하고, 피격처럼 순간적인 연출은 `RpcPlayOneShot`으로 보낸다. `PlayerAnimator.controller`는 전이·파라미터 없이 상태만 두고 코드가 `Animator.Play`로 재생한다. `Shrink` 클립이 콜라이더 크기를 바꾸므로 Write Defaults는 켜 둔다.
- **들 수 있는 오브젝트**: `PickupObj`를 붙이고 레이어를 `"Pickable"`로 둔다. `PlayerInteraction`의 탐색이 이 레이어를 찾는다.

## 레이어와 태그

- 태그: `"Trap"`·`"Enemy"`는 피해, `"Rope"`는 등반, `"Finish"`는 스테이지 종료, `"Reset"`은 리스폰, `"Bounce"`·`"Spring"`은 속도 변경
- 레이어: `"Player"`는 플레이어 감지 레이캐스트, `"Pickable"`은 들 수 있는 오브젝트 감지

새 태그나 레이어를 만들기 전에 위 목록으로 해결되는지 본다. 추가하면 이 파일에 적는다.
