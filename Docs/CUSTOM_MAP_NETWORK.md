# 커스텀 맵 네트워크 현재 상태

- 상태: Active
- 최종 갱신: 2026-08-04
- 범위: 로비 선택, 자동 공유, 세션 캐시, GamePlay 런타임 생성

## 현재 상태

- 맵 선택 시스템 1단계에서 `LobbyMapKind.Custom`과 공통 `MapKey` 기반이 도입됐다. 방 정책 연결은 후속 단계다.
- 방장 선택 맵은 정규 UTF-8 바이트, SHA-256, 최대 512 KiB 정책으로 서버가 검증한다.
- 선택 업로드와 참가자 전송은 24 KiB 청크를 사용한다.
- 서버는 불변 `MapSessionSnapshot`을 만들고 MapId, ContentHash, 길이와 청크 수 manifest를 배포한다.
- 참여자는 `Available`, `Missing`, `HashMismatch`, `InvalidLocalData`로 응답한다. 필요한 연결에만 콘텐츠를 전송한다.
- 서버 전송은 연결별 라운드 로빈 큐이며 기본 프레임 상한은 2청크·48 KiB다.
- manifest 응답 제한은 10초, 선택 업로드는 12초, 첫 실제 청크 송신 이후 완료 제한은 30초다.
- 선택 변경, 새 시작, 입장·이탈, 씬 전환과 서버 종료 시 진행 중 상태를 무효화한다.
- 서버 `ServerMapSessionStore`와 로컬 클라이언트 `MapSessionCache`는 분리되어 있다.
- `SlimeRoomManager.currentMapData`와 전체 JSON 네트워크 필드는 제거됐다. GamePlay 서버는 MapId와 ContentHash가 맞는 서버 세션 데이터만 사용한다.
- 공식맵과 커스텀맵은 `SlimeRoomManager.currentMapId`를 공통으로 사용하고 `currentMapKind`로 로딩 경로를 구분한다.
- 공식맵 전환은 커스텀 세션과 진행 중인 전송을 정리하며 기존 manifest·청크·timeout 정책은 유지한다.
- 자동 수신 데이터는 Maps 폴더에 쓰지 않는다. 영구 저장은 `MapReceivePrompt`의 명시적 승인 흐름만 사용한다.
- 커스텀맵 스키마 2.2의 `minimumPlayersToClear`도 정규 JSON과 ContentHash에 포함되며 서버 스냅샷 검증을 거친다.
- 방 생성 시 확정한 `RoomMapPolicy.CustomOnly`와 커스텀 `MapKey`가 서버·클라이언트 전송 허용 기준이다.
- 선택 업로드, manifest, 청크와 완료 응답은 `CustomOnly` 방이 아니면 각 진입점에서 거부한다.
- 게임 시작 직전 서버는 선택한 커스텀 `MapId`와 `ContentHash`, 실제 준비 참가자 연결을 완료 판정 세션으로 고정한다.
- `StageManager`가 서버 세션 저장소의 같은 `MapId + ContentHash` 데이터를 실제 로드한 경우에만 커스텀맵 완료 대상이 유효해진다.
- 모든 세션 참가자가 완료 조건을 만족하면 서버가 확정한 `MapId + ContentHash` 결과를 각 참가자에게 전달하고, 참가자 클라이언트가 자신의 로컬 완료 저장소에 기록한다.
- 완료 기록은 자동 수신 캐시나 Maps 폴더의 영구 저장 여부와 독립적이므로, 자동 수신 후 저장하지 않은 커스텀맵도 완료 이력이 유지된다.
- 커스텀맵 공개방 생성에는 목록과 네트워크 전송에 사용하는 동일한 정규 JSON의 `ContentHash` 완료 기록이 필요하다. 비공개 방에는 완료 제한이 없다.
- `HostRoomScreen`, `SteamRoomManager`, `SteamLobbyMatchmaking`이 Steam 생성 전 같은 로컬 완료 자격을 순서대로 재검사한다. 기존 공개 로비 메타데이터 형식과 전송 프로토콜은 변경하지 않았다.

## 핵심 흐름

```text
MapListUI
  → MapSelectionManager
  → CustomRoomPlayer Command/TargetRpc
  → ServerMapSessionStore / MapSessionCache
  → SlimeRoomManager(MapId, ContentHash)
  → StageManager
  → MapDataNetworkSync
  → GameManager 서버 클리어 확정
  → 참가자별 MapCompletionRepository(MapId + ContentHash)
```

주요 위치:

- `Assets/01_Scripts/UI Scripts/Room/MapSelectionManager.cs`
- `Assets/01_Scripts/NetworkScripts/CustomRoomPlayer.cs`
- `Assets/01_Scripts/NetworkScripts/SlimeRoomManager.cs`
- `Assets/01_Scripts/PlayingScripts/StageManager.cs`
- `Assets/01_Scripts/MapEditorScripts/Network`
- `Assets/Tests/EditMode/MapTransferCoreTests.cs`

## 검증

- `FlipFriends.MapTransferCore.Tests` Edit Mode 테스트 9개가 통과했다.
- 역순 조립, 중복, 누락, 크기 초과, 이전 식별자, 라운드 로빈 순환·취소, 업로드 lease 무효화·교체를 검증한다.
- Unity 컴파일과 `Assembly-CSharp` 빌드는 오류 없이 통과했다.
- 맵 선택·전송 Edit Mode 전체 38개가 통과했으며 `CustomOnly` 전송 정책 경계 테스트를 포함한다.
- 6단계 완료 저장소와 기존 선택·전송을 포함한 Unity Edit Mode 전체 51개가 통과했다.
- 커스텀맵 완료 기록 테스트는 최초 저장, 같은 MapId의 ContentHash 변경 시 과거·현재 완료 구분, 자동 수신 데이터와 독립적인 MapId·ContentHash 저장 형식을 검증한다.
- GameRoom의 `MapSelectionManager`가 공식 카탈로그·팔레트와 공용 목록 인스턴스를 참조하고, GamePlay의 `StageManager`가 같은 카탈로그·팔레트와 `MapDataNetworkSync` 프리팹을 참조함을 YAML GUID·fileID로 확인했다.
- 완료 저장 추가 후에도 `CustomOnly` 전송 진입점, 24 KiB 청크, 512 KiB 상한, 서버 세션 저장소와 클라이언트 자동 수신 캐시 분리 정책이 유지됨을 정적 검색으로 확인했다.
- 실제 Steam 2인 환경의 자동 수신, 지연, 전송 중 이탈·선택 변경, 타임아웃과 Host/원격 GamePlay 일치는 수동 검증 대기다.
- 7단계 변경 후에도 24 KiB 청크, 512 KiB 상한, `CustomOnly` 전송 경계와 서버 세션 스냅샷 흐름이 유지됨을 정적으로 확인했다.
- 8단계 Unity Edit Mode 전체 64개가 통과했으며 기존 `MapTransferCore` 9개도 모두 유지된다.

## 남은 작업

- 실제 Steam 2인 및 최대 4인 수동 검증
- Mirror 연결과 Coroutine을 포함하는 Play Mode 통합 테스트
- `StageManager`의 런타임 로더·오브젝트 팩토리 책임 분리
- 재접속 정책과 실패 중 생성된 세션 오브젝트 정리
- 상세 전송 진행률 UI는 안정성 검증 이후 진행

## 다음 작업

Steam 2인 환경이 없다면 Mirror 연결을 대체하는 Play Mode 테스트 하네스를 만들어 취소·타임아웃 상태 전이를 자동 검증한다.
