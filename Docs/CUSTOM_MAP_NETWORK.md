# 커스텀 맵 네트워크 현재 상태

- 상태: Active
- 문서 정리: 2026-11-08 (실행 검증 날짜는 검증 절 참조)
- 범위: 로비 선택, 자동 공유, 세션 캐시, GamePlay 런타임 생성

## 현재 상태

- 현재 제품 합의는 제품6/ff-product-6이다. 새 Tilemap 사용례6:6과 기존2:2~5:5를 구분해 검증한다. 형상·Transform·Reset 조건/목적지 참조·경계가 해시에 포함된다. Steam/친구 맵 전송은 기존 Mirror 경로이며 새 출시 인증 폴백은 없다. [Tilemap 기록](Notes/2026-11-08-quantum-tilemap.md).

- 장치5:5의 참조/목적지/쿨다운/경계 검증과 정책을 제품6에서도 유지한다. 기존 Steam 로비·친구 맵 전송은 Mirror이며 출시 인증은 미완료다. [장치 정책](Notes/2026-11-08-quantum-devices.md)을 따른다.


- Main Start의 Quantum 준비 화면·지원 맵6종·제품 세션은 Mirror 방 플레이어나 맵 캐시에 의존하지 않는다. 제품 온라인 세션은 v2~v6 콘텐츠와 제품6 규칙을 전원 합의한 뒤 시작하고 진행 중 새 입장을 닫는다. 출시 연결은 설정 미완료로 실패하며 에디터 개발 앱 두 연결과 구분한다. Steam 로비/초대·친구 맵 전송·일반 GameRoom은 아직 기존 Mirror 경로다.
- 원본/베이크와 안정 콘텐츠 참조를 검증하고 참가자별 SHA-256·제품/변환 버전이 일치해야 시작한다. 구버전·불일치·누락 제한시간·연결 실패는 중단한다. MapData2.1을 유지하며 해시를 구매 인증으로 간주하지 않는다.
- 로비 플레이어 목록은 공통 OnStartServer에서 초기화해 서버 전용 접속도 처리한다. 이탈 연결은 Mirror pendingPlayers에서도 제거해 다음 GamePlay 로드에 재사용하지 않는다.

- 스테이지 클리어는 서버가 기존 SlimeRoomManager.ReturnRoomScene을 직접 한 번 호출한다. 클라이언트 Command에 의존하던 전용 서버 경로를 수정했으며 테스트 플레이/캐시 정리 정책은 기존 복귀 메서드를 따른다.

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
- 기본 맵은 `BuiltInMapCatalog`(`Assets/07_ScriptableObject/BuiltInMapCatalog.asset`)의 문자열 ID(`stage-01`~`stage-21`, 소문자 kebab-case, `BuiltInMapId` 규칙)로 고른다. `CmdSelectBuiltInMap`, `selectedMapId` SyncVar, `SlimeRoomManager.currentBuiltInMapId`, `StageManager` 로드가 모두 ID를 쓴다. 정수 `currentStage`·`selectedStage`와 `StageManager.stageMapPrefabs`는 제거됐다. 카탈로그 순서는 GameRoom `mapButtons` 순서와만 연결되고, 항목이 없는 버튼은 누를 수 없다.
- GamePlay 서버의 커스텀 맵 생성은 세 단계로 나뉜다. `StageManager`가 기본 맵과 커스텀 맵 중 무엇을 띄울지 정하고, `CustomMapRuntimeLoader`가 생성 순서(씬 시작 위치 해제 → `MapDataNetworkSync` 생성 → 배치 오브젝트 생성)를 맡고, `PlacedObjectFactory`가 배치 항목 하나를 팔레트 프리팹으로 만들어 Spawn한다. 두 클래스는 MonoBehaviour가 아니므로 Inspector 연결은 `StageManager`에 그대로 있다.
- 로드 실패 정책: 세션 맵 누락, 팔레트·동기화 프리팹 미연결, 배치 오브젝트 하나라도 생성 실패, 기본 맵 카탈로그 누락이면 실패다. 커스텀 맵은 일부만 생성된 상태로 두지 않는다. 로더가 이미 Spawn한 오브젝트를 모두 `NetworkServer.Destroy`하고 씬 시작 위치를 다시 등록한다. 이후 `StageManager`가 다음 프레임에 `SlimeRoomManager.ReturnRoomSceneAfterLoadFailure`로 전원을 GameRoom에 돌려보낸다(테스트 플레이는 에디터 복귀). 서버는 그 시점의 연결 목록을 기억했다가 각 클라이언트가 GameRoom에서 준비되면(`OnServerReady`) `CustomRoomPlayer.TargetShowStageLoadFailure`로 사유를 한 번씩 보낸다. 사이에 나간 연결은 목록에서 빠지고, 새로 들어온 참여자는 받지 않는다. 클라이언트는 GameRoom Canvas의 `StageLoadFailureNotice` 패널에 사유를 띄운다. 맵 선택 화면은 이때 닫혀 있으므로 별도 패널이다. Submit·Cancel은 준비·방 나가기에 쓰이므로 패널은 닫기 버튼이나 8초 경과로 닫힌다.
- 재접속 정책: GamePlay 진행 중 입장·재입장은 받지 않는다. Mirror `NetworkRoomManager`가 Room 씬이 아니면 새 연결을 끊는다. 이와 맞추어 `SteamRoomManager`는 GameRoom에서만 Steam 로비를 입장 가능으로 둔다. 이탈한 플레이어는 GameRoom으로 돌아온 뒤 새 참여자로 다시 들어오며, 이때 보유 검사가 무효화되고 클라이언트 `MapSessionCache`는 `OnStopClient`에서 이미 비워져 있다.
- 게임 중 이탈하면 남은 인원으로 클리어를 판정한다. `GameManager.FinishCheck`는 플레이어 목록을 캐시하지 않고, 서버는 이탈 다음 프레임에 판정을 다시 부른다. 이전에는 이탈자가 캐시에 남아 클리어가 불가능했다.
- 맵 에디터 테스트 플레이는 같은 서버 세션 경로(`ServerMapSession`, `currentMapId`)로 맵을 생성한다. 흐름은 `Docs/MAP_EDITOR.md`에 있다.

## 핵심 흐름

```text
MapListUI
  → MapSelectionManager
  → CustomRoomPlayer Command/TargetRpc
  → ServerMapSessionStore / MapSessionCache
  → SlimeRoomManager(MapId, ContentHash)
  → StageManager
  → CustomMapRuntimeLoader → PlacedObjectFactory
  → MapDataNetworkSync
```

주요 위치:

- `Assets/01_Scripts/UI Scripts/Room/MapSelectionManager.cs`
- `Assets/01_Scripts/NetworkScripts/CustomRoomPlayer.cs`
- `Assets/01_Scripts/NetworkScripts/SlimeRoomManager.cs`
- `Assets/01_Scripts/PlayingScripts/StageManager.cs`
- `Assets/01_Scripts/MapEditorScripts/Network` (`CustomMapRuntimeLoader`, `PlacedObjectFactory` 포함)
- `Assets/01_Scripts/MapEditorScripts/Network/Core/MapAvailabilityCheck.cs`
- `Assets/Tests/EditMode/MapTransferCoreTests.cs`, `MapAvailabilityCheckTests.cs`

## 검증

- 제품5 콘텐츠 합의·불일치 거부·개발 Photon 두 연결·종료 정리는 [공통 검증](QUANTUM_MIGRATION.md#검증)을 따른다. Steam/친구 전송 전체 이식이나 출시 인증 통과를 의미하지 않는다.
- Mirror: 2026-10-10 EditMode102개와 사용자 Steam2인 커스텀 맵 선택·전송·시작 확인 기록이 있다. 3~4인·전송 중 이탈/변경·배포 환경은 별도 검증이 필요하다.
- 2026-10-26 KCP의 스테이지/API 완료 왕복·서버 전용 초기화·이탈 후 재시작 결과는 [실행 기록](Notes/2026-10-26-stage-live-validation.md)을 따른다.
- 청크/제한시간/세대·취소/ID 테스트와 카탈로그·UI 참조 상세는 [과거 상세 검증](Notes/2026-11-08-documentation-reference-history.md)에 보존한다.

## 남은 작업

- 기본 스테이지 2인 수동 검증, 최대 4인 수동 검증
- 실제 Mirror 연결(호스트·원격 클라이언트)과 청크 큐를 포함하는 Play Mode 통합 테스트. 상태 전이는 Edit Mode에서 검증된다.
- 상세 전송 진행률 UI는 안정성 검증 이후 진행

## 다음 작업

제품 Quantum 경로의 이동/상호작용·스테이지 지원을 확장한 뒤 친구 맵 전송·Steam 로비/초대를 연결한다. 실제 출시 인증·다른 PC·Steam 다계정·지연/손실 시험은 미실행이며 기존 Mirror 회귀와 구분한다.
