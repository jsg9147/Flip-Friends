---
paths:
  - "Assets/01_Scripts/NetworkScripts/**/*.cs"
  - "Assets/QuantumUser/**/*.cs"
  - "Assets/QuantumUser/**/*.qtn"
  - "Assets/01_Scripts/MapEditorScripts/Network/**/*.cs"
---

# 네트워크 규칙 (Quantum)

Mirror·NGO는 2026-11-15에 제거했다. 네트워크는 Photon Quantum 3 결정론 시뮬레이션 하나다. 현재 범위와 검증은 `Docs/QUANTUM_MIGRATION.md`를 따른다.

## 시뮬레이션

- 게임 규칙은 `Assets/QuantumUser/Simulation`의 `FlipFriends*System`이 FP·`DeltaTime`·프레임 상태로만 처리한다. 타이머·관계·접촉 이력처럼 다음 틱에 영향을 주는 값은 모두 프레임(`FlipFriends.qtn` 컴포넌트·전역)에 둔다. `UnityEngine` 타입, `float`, 실시간, 코루틴, `System.Random`을 시뮬레이션에 넣지 않는다.
- 같은 틱의 여러 플레이어는 `PlayerRef` 순서로 처리한다. 순서가 결과를 바꾸는 판정(운반 탐색, 동시 피격, 스위치 반전)은 정렬 기준을 코드에 명시한다.
- Unity 쪽은 입력 수집(`QuantumPlayerInput`)과 표시(`QuantumGameView`, `Quantum*Views`)만 맡는다. 스프라이트·카메라는 예측 프레임, 효과음처럼 중복되면 안 되는 연출은 확정 틱에서 계산한다. 표시 코드가 시뮬레이션 상태를 바꾸지 않는다.
- 규칙이나 수치를 바꾸면 결정론 합의 문자열이 달라져야 한다. 제품 서명(`QuantumGameContent.NetworkVersion`, 현재 `ff-product-15`)을 올리고 기존 공식·매니페스트 해시가 바뀌는지 확인한다.

## 콘텐츠

- 맵 콘텐츠는 `IQuantumMapContent.ValidateAndApply`로 `RuntimeConfig`에 들어간다. 공식 스테이지는 `QuantumOfficialStageAsset`(작성 컴포넌트 → `QuantumOfficialStageConverter`), 사용례·저장 맵은 `QuantumCustomMapAsset`/`QuantumMapConverter`(MapData + `QuantumMapEditorPalette`)다.
- 저장된 콘텐츠 해시와 재변환 해시가 다르면 시작하지 않는다. 변환 규칙을 바꾸면 변환 버전을 올리고 기존 에셋을 재변환한다.
- 온라인 시작 전 참가자 전원이 콘텐츠 합의(`QuantumMapAgreement`)를 마친 뒤 Photon 방을 닫고 러너를 시작한다. 저장 맵은 `QuantumSavedMapTransfer`가 보유 확인 후 필요한 참가자에게만 보낸다. 받은 맵은 자동 저장하지 않는다.

## 세션과 씬

- `QuantumGameFlow`가 세션과 씬 수명을 소유한다(Main ↔ QuantumPlay, MapEditor 테스트 플레이 복귀). 제품 SimulationConfig의 자동 씬 로딩은 Disabled로 유지한다.
- 세션을 끝낼 때 러너·Photon 연결·입력 구독·동적 맵을 모두 정리한다. 검사는 `QuantumRunner.ActiveRunners`와 `QuantumGameSession.ActiveInputSubscriptions`가 0인지 본다.
- Steam 로비는 `SteamLobbyService`(Main 씬 오브젝트, DDOL)만 호출한다. `SteamAPI.Init`/`Shutdown`은 Steamworks.NET `SteamManager`에 맡긴다. `quantum-v1` 표시가 없는 로비는 받지 않는다.
- 진행 중 입장은 받지 않는다. 방장 시작 시 Steam 신규 입장과 Photon 방을 닫는다.

## 인증

출시 경로는 `SteamPhotonTicket`의 Steam 인증만 쓴다. 개발 익명 연결은 `UNITY_EDITOR` 검사 코드에만 둔다. 기준은 `Docs/QUANTUM_SECURITY.md`다.

## 검증

- 새 Play에서 Main을 열고 `Tools/Flip Friends/Quantum/Check Product Offline And Main`(`QuantumProductChecks.RunOffline`)을 다른 Main 검사보다 먼저 돌린다. 이어서 `Check Steam Lobby With Development Photon`으로 모의 Steam + 개발 Photon 두 연결을 확인한다.
- 같은 PC의 개발 Photon 연결은 출시 인증 검증이 아니다. Steam 실계정·다른 PC·실제 다인 조작은 실행 여부를 따로 적는다.
