# 커스텀 맵 네트워크 현재 상태

- 상태: Active
- 최종 갱신: 2026-10-10
- 범위: 로비 선택, 자동 공유, 세션 캐시, GamePlay 런타임 생성

## 현재 상태

- Quantum 독립 시험은 원본/베이크 정적 지형과 로프 FP 데이터를 검증하고, 참가자별 Photon 속성의 콘텐츠 SHA-256·변환 v2·팔레트 v2가 두 명 모두 일치한 뒤 세션을 시작한다. v1 검증 에셋·불일치·누락 제한 시간·연결 실패는 중단한다. MapData 2.1·기존 Mirror 친구 전송/세션 캐시는 보존했으며 Quantum 전송 전체는 미이식이다. 해시는 구매 인증이 아니다. [세부 규칙](QUANTUM_MIGRATION.md)을 따른다.
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

- 2026-11-06 로프 v2 맵의 새 개발 Photon `asia` 2연결에서 콘텐츠 합의 후 두 플레이어 로프 동작과 동일 확정 240~960 체크섬 7개 일치를 확인했다. RTT 175/126ms·같은 PC·인위적 지연/손실 없음. 시험 입력은 연결 순서 대신 확인된 전역 PlayerRef를 사용한다. `Logs/QuantumRopeCloudCheck.txt`에 기록했으며 Steam 인증·다른 PC·친구 맵 전송은 미실행이다.
- 2026-11-06 변환 맵으로 같은 PC의 개발 Photon `asia` 실제 두 연결을 새로 실행했다. 맵 확인 경계 이후 두 러너의 동일 확정 240~720(120간격) 체크섬이 일치했다. 최종 RTT 107/109ms, 인위적 지연·손실 없음(`Logs/QuantumCustomMapCloudCheck.txt`). Steam 다계정·다른 PC·친구 간 Quantum 맵 전송·출시 인증은 미실행이다.
- 2026-10-26 동일 개발 빌드 KCP 호스트/원격에서 지연 0/60ms 각각 21개 기본 스테이지 생성·API 완료 후 실제 복귀/정리를 확인했다. 지연 조건에서 완료한 호스트만 남기는 미완료자 이탈 후 복귀도 통과했다. 에디터 서버 전용(로컬 클라이언트 없음)의 접속 예외 수정 후 Stage 21 발사·API 완료 복귀를 확인했다. [상세와 미검증 범위](Notes/2026-10-26-stage-live-validation.md). Steam 전송·로비 정책 검증을 대체하지 않는다.

- 2026-10-26 격리 서버 완료 판정 검사에서 빈 게임·미도착자 대기·클라이언트 없는 복귀 요청 1회를 확인했다. Main/GameRoom/GamePlay의 카탈로그·팔레트·스폰 연결도 재검사했다. 실제 씬 전환·이탈 후 클리어·Steam 경로는 수동 확인이 남는다.

- `FlipFriends.MapTransferCore.Tests` Edit Mode 테스트 9개가 통과했다. Unity `6000.6.3f1`과 test-framework `1.8.0`에서 재실행해 9개 전부 통과를 확인했다.
- 역순 조립, 중복, 누락, 크기 초과, 이전 식별자, 라운드 로빈 순환·취소, 업로드 lease 무효화·교체를 검증한다.
- `MapAvailabilityCheckTests` 15개는 Mirror 연결을 문자열 참여자로, Coroutine을 수동 시계로 바꾼 하네스다. 전원 보유 시 완료, 누락자에게만 전송, manifest 10초 초과, 전송 제한이 첫 청크 송신부터 재짐(manifest 단계 알림은 무시), 취소 후 늦은 응답·마감 무시, 새 세대가 이전 응답·마감을 무시, 중복·명단 밖 응답, 식별자 불일치·로컬 데이터 손상·참여자 보고 실패, 3인 부분 완료, 빈 참여자, lease 마감 만료를 보장한다.
- 2026-10-10 Unity `6000.6.3f1` Test Runner에서 Edit Mode 전체 102개(`FlipFriends.MapTransferCore.Tests` 39개 포함)가 통과했다. 에디터 컴파일(Mirror weaver 포함)은 오류 0건이다.
- `BuiltInMapIdTests` 15개는 kebab-case 허용, 대문자·밑줄·공백·연속 하이픈·64자 초과 거부, 목록의 중복 ID와 형식 오류 위치 보고를 보장한다.
- 카탈로그 21개 항목이 이전 `stageMapPrefabs` 순서(Stage 1~21)와 같고, GameRoom `MapSelectionManager`와 GamePlay `StageManager`에 연결된 것을 에디터에서 확인했다. Stage 21이 Main 씬 `SlimeRoomManager`·`SteamRoomManager`의 `spawnPrefabs`에서 빠져 원격 클라이언트에 생성되지 않던 문제(변경 전부터 존재)를 등록으로 고쳤다. 실제 방에서 기본 스테이지, 특히 Stage 21을 원격 클라이언트까지 확인하는 것은 수동 검증이 남아 있다.
- 2026-10-10 로더·팩토리 분리, 로드 실패 정리, 재접속 정책 반영 후 Unity 컴파일(Mirror weaver 포함)과 `Assembly-CSharp` 빌드 오류 0건, Edit Mode 102개 통과를 확인했다. 런타임 동작(커스텀 맵 생성, 실패 시 GameRoom 복귀, 게임 중 이탈 후 클리어, 게임 중 Steam 로비 비노출)은 수동 검증이 남아 있다. 이탈·로비 항목은 2인 이상이 필요하다.
- 2026-10-10 로드 실패 알림 추가 후 Unity 컴파일 오류 0건, Edit Mode 102개 통과를 확인했다. 패널은 에디터에서 GameRoom Canvas 맨 위에 만들고 필드를 연결했으며, Danjo-bold 글꼴에 안내 문구의 한글 글리프가 있는 것을 확인했다. 실제로 실패를 일으켜 패널이 뜨는지는 수동 검증이 남아 있다(팔레트 연결을 잠시 끊고 커스텀 맵 시작 등).
- Unity 컴파일과 `Assembly-CSharp` 빌드는 오류 없이 통과했다. `6000.6.3f1`에서도 테스트 어셈블리를 포함해 오류 없이 빌드된다.
- 2026-10-10 사용자가 실제 Steam 2인 환경에서 커스텀 맵 선택·전송·시작 흐름을 수동 확인했다. 3~4인 환경은 확인하지 않았다.

## 남은 작업

- 기본 스테이지 2인 수동 검증, 최대 4인 수동 검증
- 실제 Mirror 연결(호스트·원격 클라이언트)과 청크 큐를 포함하는 Play Mode 통합 테스트. 상태 전이는 Edit Mode에서 검증된다.
- 상세 전송 진행률 UI는 안정성 검증 이후 진행

## 다음 작업

Quantum 정적 맵 일치 경계를 유지하며 이동 기능을 다음 단계로 이식한다. Steam 2~4인·다른 PC·지연/손실과 친구 맵 전송 이식은 별도 항목이며 기존 Mirror의 실조작 클리어·이탈·로드 실패 알림 회귀와 구분한다.
