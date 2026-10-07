---
paths:
  - "Assets/01_Scripts/NetworkScripts/**/*.cs"
  - "Assets/01_Scripts/PlayerScripts/**/*.cs"
  - "Assets/01_Scripts/MapEditorScripts/Network/**/*.cs"
---

# 네트워크 규칙 (Mirror)

- **서버 권한**: 이동, 물리, 충돌은 서버에서만 실행한다. `MovementHandler.FixedUpdate`는 `if (!isServer) return`으로 막는다.
- 클라이언트 → 서버는 `[Command]`, 서버 → 클라이언트는 `[ClientRpc]`를 쓴다. 예: `CmdJumpInputDown`, `CmdObjectInteraction` / `RpcFlipChanged`, `RpcVelocityReset`.
- 상태 동기화는 `[SyncVar(hook = nameof(...))]`로 해서 모든 클라이언트에서 hook이 불리게 한다.
- `isServer`, `isOwned`, `isLocalPlayer` 분기는 메서드 진입부에서 조기 반환한다.
- `[Command]`는 `Cmd`, `[ClientRpc]`는 `Rpc`, `[SyncVar]` hook은 `On` + 변수명 + `Changed` 접두사를 붙인다.
- 새 네트워크 상태를 추가하면 호스트와 늦게 들어온 클라이언트 양쪽에서 값이 맞는지 확인한다. `SyncVar`는 접속 시점에 초기값이 전달된다.

## 씬 흐름

`Main.unity`(메인 메뉴·로비 선택) → `GameRoom.unity`(준비·스테이지 선택) → `GamePlay.unity`(서버 시작 시 `StageManager`가 스테이지 프리팹 생성). 모든 플레이어가 도착 트리거에 들어가면 `GameManager`가 `SlimeRoomManager.ReturnRoomScene()`을 호출해 `GameRoom.unity`로 돌아간다.

맵 에디터 테스트 플레이(`SlimeRoomManager.IsTestPlaying`) 중에는 `ReturnRoomScene()`과 `GameManager.ExitGame()`이 호스트를 끄고 에디터로 돌아간다. 이 동안 `offlineScene`을 비워 둔다. 값이 있으면 Mirror가 종료 시 매니저를 DDOL에서 꺼내 파괴한다. 서버 종료 흐름을 바꿀 때 이 분기를 함께 확인한다.

`NetworkRoomManager.OnServerAddPlayer`는 `OnRoomServerAddPlayer`를 호출하지 않는다. 방 플레이어 생성 직후에 할 일은 `OnServerAddPlayer`를 오버라이드한다.

## 입력

New Input System만 쓴다. `PlayerInputManager`가 콜백으로 입력을 받아 서버에 Command로 넘기고, 서버가 물리를 처리한 뒤 ClientRpc로 반영한다. 클라이언트에서 직접 물리를 건드리지 않는다.

## 검증

Steam 클라이언트 로그인이 필요하다. 2인 이상이 필요한 항목은 수동 검증으로 남기고 `Docs/CUSTOM_MAP_NETWORK.md`의 `검증`에 환경 제약으로 적는다.
