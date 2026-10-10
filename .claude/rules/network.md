---
paths:
  - "Assets/01_Scripts/NetworkScripts/**/*.cs"
  - "Assets/01_Scripts/PlayerScripts/**/*.cs"
  - "Assets/01_Scripts/MapEditorScripts/Network/**/*.cs"
---

# 네트워크 규칙 (Mirror)

- **이동은 예측 + 서버 권한**: 소유 클라이언트는 `ClientMover`로 예측하고, 서버는 `ServerMover`가 같은 `MovementHandler.Simulate`를 입력 하나당 한 번 돌려 보정값을 보낸다. 다음 틱에 영향을 주는 값은 모두 `StatePayload`에 넣고, 시뮬레이션 안의 타이머는 코루틴이 아니라 틱 카운터로 둔다.
- 서버가 위치를 정하는 일(운반 해제, 리스폰)은 `ServerMover.Teleport`로만 한다. 에포크가 바뀌어 이전 입력·보정값이 버려진다. 플레이어 `transform.position`을 직접 바꾸지 않는다.
- 들기·던지기와 피격은 서버만 판정하고 `SendStateNow`나 RPC로 알린다. 밟기 튕김·스프링·바운스·로프는 소유 클라이언트도 예측하되, 상대에게 주는 효과(Shrink, 점프 차단)는 서버만 낸다. 결과가 갈리면 서버 보정이 덮어쓴다.
- 보정·재동기화로 생기는 위치 차이는 `ClientMover`의 보이는 위치 오프셋으로 약 0.1초에 걸쳐 따라간다. 판정용 위치(`predictedPosition`)에는 오프셋을 넣지 않는다.
- 클라이언트 → 서버는 `[Command]`, 서버 → 클라이언트는 `[ClientRpc]`를 쓴다. 서버에 없는 입력(아래 방향 등)은 Command 인자로 보낸다.
- 상태 동기화는 `[SyncVar(hook = nameof(...))]`로 해서 모든 클라이언트에서 hook이 불리게 한다.
- `isServer`, `isOwned`, `isLocalPlayer` 분기는 메서드 진입부에서 조기 반환한다.
- `[Command]`는 `Cmd`, `[ClientRpc]`는 `Rpc`, `[SyncVar]` hook은 `On` + 변수명 + `Changed` 접두사를 붙인다.
- 새 네트워크 상태를 추가하면 호스트와 늦게 들어온 클라이언트 양쪽에서 값이 맞는지 확인한다. `SyncVar`는 접속 시점에 초기값이 전달된다.

## 씬 흐름

`Main.unity`(메인 메뉴·로비 선택) → `GameRoom.unity`(준비·스테이지 선택) → `GamePlay.unity`(서버 시작 시 `StageManager`가 스테이지 프리팹 생성). 모든 플레이어가 도착 트리거에 들어가면 `GameManager`가 `SlimeRoomManager.ReturnRoomScene()`을 호출해 `GameRoom.unity`로 돌아간다.

맵 에디터 테스트 플레이(`SlimeRoomManager.IsTestPlaying`) 중에는 `ReturnRoomScene()`과 `GameManager.ExitGame()`이 호스트를 끄고 에디터로 돌아간다. 이 동안 `offlineScene`을 비워 둔다. 값이 있으면 Mirror가 종료 시 매니저를 DDOL에서 꺼내 파괴한다. 서버 종료 흐름을 바꿀 때 이 분기를 함께 확인한다.

게임 진행 중 입장·재입장은 받지 않는다(Mirror가 Room 씬 밖의 새 연결을 끊고, `SteamRoomManager`가 GameRoom 밖에서 로비를 입장 불가로 둔다). 이 정책을 바꾸면 `GameManager.FinishCheck`와 커스텀 맵 세션 캐시를 함께 확인한다.

`NetworkRoomManager.OnServerAddPlayer`는 `OnRoomServerAddPlayer`를 호출하지 않는다. 방 플레이어 생성 직후에 할 일은 `OnServerAddPlayer`를 오버라이드한다.

## 입력

New Input System만 쓴다. `PlayerInputManager`가 콜백으로 입력을 받고, 이동 입력은 `ClientMover`가 `InputPayload`로 서버에 보낸다. 들기·리셋 같은 일회성 입력은 `Consume...`으로 한 번만 읽는다.

## 검증

Steam 클라이언트 로그인이 필요하다. 한 PC에서 2인을 확인할 때는 `LocalNetworkTest`(KCP + 지연 시뮬레이션)를 쓴다: 에디터는 `Tools/Flip Friends/Local Network Test`, 빌드는 `-localhost`/`-localclient` `-latency 60`. Steam 경로가 필요한 항목은 수동 검증으로 남기고 `Docs/CUSTOM_MAP_NETWORK.md`의 `검증`에 환경 제약으로 적는다.
