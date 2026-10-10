# 커스텀 맵 네트워크 현재 상태

- 상태: Active
- 최종 갱신: 2026-10-10
- 범위: 로비 선택, 자동 공유, 세션 캐시, GamePlay 런타임 생성

## 현재 상태

- 방장 선택 맵은 정규 UTF-8 바이트, SHA-256, 최대 512 KiB 정책으로 서버가 검증한다.
- 선택 업로드와 참가자 전송은 24 KiB 청크를 사용한다.
- 서버는 불변 `MapSessionSnapshot`을 만들고 MapId, ContentHash, 길이와 청크 수 manifest를 배포한다.
- 참여자는 `Available`, `Missing`, `HashMismatch`, `InvalidLocalData`로 응답한다. 필요한 연결에만 콘텐츠를 전송한다.
- 서버 전송은 연결별 라운드 로빈 큐이며 기본 프레임 상한은 2청크·48 KiB다.
- manifest 응답 제한은 10초, 선택 업로드는 12초, 첫 실제 청크 송신 이후 완료 제한은 30초다.
- 보유 검사의 상태 전이(참여자별 manifest·전송 상태, 세대·식별자 대조, 마감 시각)는 `MapAvailabilityCheck<TParticipant>`가 맡는다. Mirror와 Unity API에 의존하지 않는다. `MapSelectionManager`는 연결을 참여자 키로 넘기고, 제한 시간은 Coroutine 대신 서버 `Update`에서 `Time.realtimeSinceStartupAsDouble`로 `Tick`한다. 선택 업로드 제한은 `SelectionUploadLease`의 마감 시각으로 잰다.
- 취소 안내(`SelectionChanged`)는 manifest 대기뿐 아니라 청크 전송 중에도 나간다. 이전에는 전송 단계에서 취소되면 상태 메시지가 '전송 중'으로 남았다.
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
- `Assets/01_Scripts/MapEditorScripts/Network/Core/MapAvailabilityCheck.cs`
- `Assets/Tests/EditMode/MapTransferCoreTests.cs`, `MapAvailabilityCheckTests.cs`

## 검증

- `FlipFriends.MapTransferCore.Tests` Edit Mode 테스트 9개가 통과했다. Unity `6000.6.3f1`과 test-framework `1.8.0`에서 재실행해 9개 전부 통과를 확인했다.
- 역순 조립, 중복, 누락, 크기 초과, 이전 식별자, 라운드 로빈 순환·취소, 업로드 lease 무효화·교체를 검증한다.
- `MapAvailabilityCheckTests` 15개는 Mirror 연결을 문자열 참여자로, Coroutine을 수동 시계로 바꾼 하네스다. 전원 보유 시 완료, 누락자에게만 전송, manifest 10초 초과, 전송 제한이 첫 청크 송신부터 재짐(manifest 단계 알림은 무시), 취소 후 늦은 응답·마감 무시, 새 세대가 이전 응답·마감을 무시, 중복·명단 밖 응답, 식별자 불일치·로컬 데이터 손상·참여자 보고 실패, 3인 부분 완료, 빈 참여자, lease 마감 만료를 보장한다.
- 2026-10-10 Unity `6000.6.3f1` Test Runner에서 Edit Mode 전체 87개(`FlipFriends.MapTransferCore.Tests` 24개 포함)가 통과했다. 에디터 컴파일(Mirror weaver 포함)과 `Assembly-CSharp` 빌드는 오류 0건이다.
- Unity 컴파일과 `Assembly-CSharp` 빌드는 오류 없이 통과했다. `6000.6.3f1`에서도 테스트 어셈블리를 포함해 오류 없이 빌드된다.
- 실제 Steam 2인 환경의 자동 수신, 지연, 전송 중 이탈·선택 변경, 타임아웃과 Host/원격 GamePlay 일치는 수동 검증 대기다.

## 남은 작업

- 실제 Steam 2인 및 최대 4인 수동 검증
- 실제 Mirror 연결(호스트·원격 클라이언트)과 청크 큐를 포함하는 Play Mode 통합 테스트. 상태 전이는 Edit Mode에서 검증된다.
- 기본 맵의 안정적 문자열 ID와 `currentStage` 직접 의존 제거
- `StageManager`의 런타임 로더·오브젝트 팩토리 책임 분리
- 재접속 정책과 실패 중 생성된 세션 오브젝트 정리
- 상세 전송 진행률 UI는 안정성 검증 이후 진행

## 다음 작업

기본 맵에 안정적 문자열 ID를 주고 `currentStage` 직접 의존을 걷어낸다.
