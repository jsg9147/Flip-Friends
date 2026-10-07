# 커스텀 맵 네트워크 현재 상태

- 상태: Active
- 최종 갱신: 2026-10-07
- 범위: 로비 선택, 자동 공유, 세션 캐시, GamePlay 런타임 생성

## 현재 상태

- 방장 선택 맵은 정규 UTF-8 바이트, SHA-256, 최대 512 KiB 정책으로 서버가 검증한다.
- 선택 업로드와 참가자 전송은 24 KiB 청크를 사용한다.
- 서버는 불변 `MapSessionSnapshot`을 만들고 MapId, ContentHash, 길이와 청크 수 manifest를 배포한다.
- 참여자는 `Available`, `Missing`, `HashMismatch`, `InvalidLocalData`로 응답한다. 필요한 연결에만 콘텐츠를 전송한다.
- 서버 전송은 연결별 라운드 로빈 큐이며 기본 프레임 상한은 2청크·48 KiB다.
- manifest 응답 제한은 10초, 선택 업로드는 12초, 첫 실제 청크 송신 이후 완료 제한은 30초다.
- 선택 변경, 새 시작, 입장·이탈, 씬 전환과 서버 종료 시 진행 중 상태를 무효화한다.
- 서버 `ServerMapSessionStore`와 로컬 클라이언트 `MapSessionCache`는 분리되어 있다.
- `SlimeRoomManager.currentMapData`와 전체 JSON 네트워크 필드는 제거됐다. GamePlay 서버는 MapId와 ContentHash가 맞는 서버 세션 데이터만 사용한다.
- 자동 수신 데이터는 Maps 폴더에 쓰지 않는다. 영구 저장은 `MapReceivePrompt`의 명시적 승인 흐름만 사용한다.
- 커스텀 맵을 로드하면 `StageManager`가 GamePlay 씬의 기본 시작 위치를 해제한다. 맵에 배치한 시작 지점(`NetworkStartPosition`)만 RoundRobin 대상이다. 이전에는 씬 시작 위치가 먼저 골라져 플레이어가 맵과 무관한 곳에서 생성됐다.
- 팔레트의 `BasicGround` 프리팹 레이어를 `Finish`에서 `Ground`로 고쳤다. 이전에는 플레이어 충돌 마스크에 걸리지 않아 커스텀 맵 지면을 통과해 떨어졌다.
- 맵 에디터 테스트 플레이는 같은 서버 세션 경로(`ServerMapSession`, `currentMapId`)로 맵을 생성한다. 흐름은 `Docs/MAP_EDITOR.md`에 있다.

## 핵심 흐름

```text
MapListUI
  → MapSelectionManager
  → CustomRoomPlayer Command/TargetRpc
  → ServerMapSessionStore / MapSessionCache
  → SlimeRoomManager(MapId, ContentHash)
  → StageManager
  → MapDataNetworkSync
```

주요 위치:

- `Assets/01_Scripts/UI Scripts/Room/MapSelectionManager.cs`
- `Assets/01_Scripts/NetworkScripts/CustomRoomPlayer.cs`
- `Assets/01_Scripts/NetworkScripts/SlimeRoomManager.cs`
- `Assets/01_Scripts/PlayingScripts/StageManager.cs`
- `Assets/01_Scripts/MapEditorScripts/Network`
- `Assets/Tests/EditMode/MapTransferCoreTests.cs`

## 검증

- `FlipFriends.MapTransferCore.Tests` Edit Mode 테스트 9개가 통과했다. Unity `6000.6.3f1`과 test-framework `1.8.0`에서 재실행해 9개 전부 통과를 확인했다.
- 역순 조립, 중복, 누락, 크기 초과, 이전 식별자, 라운드 로빈 순환·취소, 업로드 lease 무효화·교체를 검증한다.
- Unity 컴파일과 `Assembly-CSharp` 빌드는 오류 없이 통과했다. `6000.6.3f1`에서도 테스트 어셈블리를 포함해 오류 없이 빌드된다.
- 실제 Steam 2인 환경의 자동 수신, 지연, 전송 중 이탈·선택 변경, 타임아웃과 Host/원격 GamePlay 일치는 수동 검증 대기다.

## 남은 작업

- 실제 Steam 2인 및 최대 4인 수동 검증
- Mirror 연결과 Coroutine을 포함하는 Play Mode 통합 테스트
- 기본 맵의 안정적 문자열 ID와 `currentStage` 직접 의존 제거
- `StageManager`의 런타임 로더·오브젝트 팩토리 책임 분리
- 재접속 정책과 실패 중 생성된 세션 오브젝트 정리
- 상세 전송 진행률 UI는 안정성 검증 이후 진행

## 다음 작업

Steam 2인 환경이 없다면 Mirror 연결을 대체하는 Play Mode 테스트 하네스를 만들어 취소·타임아웃 상태 전이를 자동 검증한다.
