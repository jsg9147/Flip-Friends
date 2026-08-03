# 맵 선택 및 클리어 기록 시스템

- 상태: Active
- 최종 갱신: 2026-08-04
- 범위: 방 맵 정책, 공식·커스텀 맵 목록, 공통 맵 식별자, 최소 클리어 인원, 클리어 기록과 공개방 자격

## 목표

- 방 생성 전에 공식맵 또는 커스텀맵과 실제 플레이할 맵을 선택한다.
- 공개 로비에서 맵 종류, 선택 맵과 최소 필요 인원을 입장 전에 확인할 수 있게 한다.
- 공식맵은 배열 위치가 아닌 변경되지 않는 문자열 ID로 식별한다.
- 맵별 최소 클리어 가능 인원을 정의하고 게임 시작 시 실제 준비 인원을 검사한다.
- 함께 클리어한 모든 참가자의 로컬 진행도에 완료 기록을 남긴다.
- 공식맵과 커스텀맵 목록에서 개인별 완료 상태를 표시한다.
- 현재 리비전을 클리어하지 않은 방장은 해당 맵으로 비공개 방만 만들 수 있게 한다.

## 확정 정책

- `RoomMapPolicy`는 `OfficialOnly`, `CustomOnly` 두 값으로 시작하며 방 생성 후 변경하지 않는다.
- `LobbyMapKind`는 현재 선택된 맵 종류를 나타내며 `None`, `Official`, `Custom`을 사용한다.
- 정확한 맵은 방 생성 전에 선택하며 초기 구현에서는 생성 후 맵 종류와 맵을 고정한다.
- 맵 목록 프리젠터는 HostRoom과 향후 GameRoom 확인 UI가 함께 사용할 수 있게 만든다.
- 최소 클리어 가능 인원은 1~4 범위다. 방 최대 인원과 게임 시작 시 실제 준비 인원이 모두 이 값 이상이어야 한다.
- 커스텀맵 최소 인원은 제작자 선언값이며 자동 분석값으로 취급하지 않는다.
- 공식맵 일반 완료 ID는 `official.*`, 현재 버전 완료 판정은 `MapId + CompletionRevision`을 사용한다.
- 커스텀맵 일반 완료 표시는 정규 GUID `MapId`, 현재 버전 완료 판정은 `MapId + ContentHash`를 사용한다.
- 완료 기록은 서버가 클리어를 확정한 후 각 참가자 클라이언트가 로컬에 저장한다.
- 자동 수신 후 영구 저장하지 않은 커스텀맵도 MapId 완료 기록은 유지한다.
- 비공개 방은 클리어 여부와 관계없이 만들 수 있다.
- 공개 방은 방장이 선택 맵의 현재 리비전을 클리어한 경우에만 만들 수 있다.
- 자격 미달 시 공개 방을 자동으로 비공개로 바꾸지 않고 생성 버튼을 막으며 이유를 표시한다.
- 로컬 완료 기록 기반 제한은 보안 또는 부정행위 방지 수단이 아닌 공개방 품질을 위한 소프트 게이트다.

## 현재 상태

- 1~5단계 기반 모델, 공식맵 MapId 로딩 전환, 맵 요구조건, 방 생성 전 선택 UI와 Steam 로비 정책 전달이 구현됐다.
- Unity·Mirror 비의존 `FlipFriends.MapSelectionCore`에 `RoomMapPolicy`, `LobbyMapKind`, `MapKey`와 ID 정규화 규칙이 있다.
- `OfficialMapCatalog`와 21개 기존 스테이지의 `official.stage.01`~`official.stage.21` 고정 ID가 생성됐다.
- 기존 공식 선택 enum 값은 숫자 호환을 유지하면서 `BuiltIn`에서 `Official`로 이름을 정리했다.
- 공식맵 버튼은 카탈로그 순서를 임시 UI 연결에만 사용하고 서버에는 `official.*` MapId를 전달한다.
- `SlimeRoomManager`는 `currentMapKind`, 공통 `currentMapId`, 커스텀 전용 `currentMapContentHash`를 세션 상태로 보관한다.
- `MapCompletionRepository`는 `schemaVersion: 1` JSON으로 공식·커스텀 완료 기록을 로컬에 원자적으로 저장한다.
- 공개방 생성 자격은 Unity 비의존 `RoomCreationRules`가 맵 선택, 최대 인원, 현재 리비전 확인 상태를 공식·커스텀 공통 모델로 판정한다.
- 비공개 방은 완료 기록과 관계없이 생성할 수 있고, 공개방은 선택 시 고정한 현재 `MapCompletionTarget`의 완료 기록이 있어야 생성할 수 있다.
- `HostRoomScreen`은 선택 맵·공개 여부·최대 인원·완료 기록 변경을 즉시 반영하며, 현재 리비전 미완료 시 비공개 방에서 먼저 완료해야 한다는 이유를 표시한다. 공개 설정은 자동 변경하지 않는다.
- `SteamRoomManager`와 `SteamLobbyMatchmaking.CreateLobby`도 로컬 `MapCompletionRepository`로 같은 자격을 각각 재검사한다.
- 완료 저장소가 미지원 스키마이거나 파일 시스템 실패로 현재 완료를 확인할 수 없으면 공개방만 안전하게 거부하고 원인을 로그와 HostRoom UI에 표시한다.
- 완료 기록은 `MapKey`의 종류와 ID를 공통 식별자로 사용하고, 공식맵은 양의 `CompletionRevision`, 커스텀맵은 소문자 SHA-256 `ContentHash`를 리비전으로 구분한다.
- 완료 조회는 해당 `MapKey`의 과거 기록을 포함하는 일반 완료와 현재 리비전의 정확한 기록을 별도로 제공한다.
- `SlimeRoomManager`는 게임 시작 직전 실제 준비 참가자 연결과 선택 맵을 `GameplayCompletionSession`으로 고정한다.
- `StageManager`는 실제 스폰에 성공한 맵만 완료 대상으로 보관하고, 클리어 시 공식맵 카탈로그 리비전 또는 커스텀맵 서버 세션 해시를 다시 검증한다.
- `GameManager`는 서버 참가자 스냅샷 전원이 완료 상태일 때 한 번만 결과를 확정하고 각 참가자 연결에 완료 결과를 보낸다.
- 각 참가자 클라이언트는 자신의 `Application.persistentDataPath/Progress/map-completions.json`에 결과를 저장한다. 저장 실패는 로그에 남지만 서버 씬 전환을 막지 않는다.
- 공식·커스텀 목록은 공통으로 `미완료`, `과거 리비전 완료`, `현재 리비전 완료`를 표시하며, 새 기록 저장 이벤트를 받으면 현재 열린 목록을 즉시 다시 만든다.
- `StageManager`는 맵 종류로 분기하고 공식맵은 `OfficialMapCatalog`, 커스텀맵은 기존 서버 세션 스냅샷에서 로드한다.
- `MapPlayRequirements`가 최소 클리어 인원을 1~4명으로 제한한다.
- 공식맵은 최소 인원과 양의 `CompletionRevision`, 커스텀맵 스키마 2.2는 제작자 선언 최소 인원을 보관한다.
- `MapSelectionList` 프리팹과 `MapListUI`가 HostRoom과 GameRoom에서 공식·커스텀 목록을 재사용한다.
- 방 생성 전에 실제 맵을 선택하며 최대 인원이 맵 최소 인원보다 적으면 생성 버튼을 비활성화한다.
- 선택한 맵은 `PendingRoomMapSelection`으로 GameRoom 서버에 전달되고 방이 유지되는 동안 고정된다.
- `LobbyMapMetadata`가 `RoomMapPolicy`, `MapKey`, 표시 이름·제작자·버전과 최소 인원을 하나의 검증된 값으로 묶는다.
- Steam 로비는 맵 정책·종류·MapId·표시 정보·최소 인원을 메타데이터로 게시하고, 공개 목록은 이를 입장 전에 표시한다.
- 공개 목록 조회에는 맵 종류와 파티 인원으로 거를 수 있는 `LobbyMapFilter` 경계가 있다. 메타데이터가 없거나 올바르지 않은 구형 로비는 공개 목록에서 제외한다.
- 호스트 서버는 방 생성 시 확정한 정책·MapKey·최소 인원과 실제 선택을 공식·커스텀 선택 및 시작 직전에 다시 비교한다.
- 시작 시 전원 준비뿐 아니라 연결된 실제 준비 완료 참가자 수가 선택 맵 최소 인원 이상인지 확인한다.
- 커스텀맵 선택 업로드, manifest, 청크와 완료 응답은 서버와 수신 클라이언트 모두 `CustomOnly` 방에서만 허용한다.
- 맵 목록 생성과 표시 문자열 책임은 `MapListUI`, 네트워크 선택 상태와 전송 조정은 `MapSelectionManager`가 담당한다.

## 핵심 흐름

```text
HostRoomScreen
  → RoomMapPolicy와 실제 MapKey 선택
  → MapPlayRequirements 최소 인원 검사
  → MapCompletionRepository 현재 리비전 완료 검사
  → Steam 로비 생성 / SlimeRoomManager
  → MapSelectionManager 서버 재검증
  → StageManager
  → GameManager 클리어 확정
  → 참가자별 MapCompletionRepository
```

## 단계별 계획

### 1단계: 식별 모델과 공식맵 카탈로그 — 완료

- 방 정책과 선택 종류를 서로 다른 enum으로 정의한다.
- 공식·커스텀 공통 완료 키인 `MapKey`를 정의한다.
- 공식맵 ID 정규 형식과 중복 검증을 추가한다.
- 기존 21개 공식맵을 고정 ID와 프리팹으로 카탈로그화한다.
- Unity 비의존 Edit Mode 테스트를 추가한다.

### 2단계: 공식맵 로딩 전환 — 완료

- `SlimeRoomManager`에 맵 종류와 공식·커스텀 공통 MapId를 보관한다.
- `MapSelectionManager`가 stage index 대신 공식 MapId를 선택하고 서버에서 카탈로그를 재검증한다.
- `StageManager`가 맵 종류로 분기하고 공식맵 프리팹을 `OfficialMapCatalog`에서 조회한다.
- `currentStage`, `selectedStage`, `stageMapPrefabs` 의존을 제거했다.

### 3단계: 맵 요구조건 데이터 — 완료

- 공통 `MapPlayRequirements` 경계를 추가하고 최소 인원 범위를 1~4로 검증한다.
- `OfficialMapEntry`에 `MinimumPlayersToClear`, `CompletionRevision`을 추가했다.
- 커스텀 `MapData`에 최소 인원을 추가하고 스키마를 2.2로 올렸다. 2.1과 2.0 데이터는 기본값 1명으로 마이그레이션한다.
- 현재 공식맵 21개는 기존 1인 시작 호환성을 유지해 최소 인원 1명, 완료 리비전 1로 등록했다.
- 이 단계에서는 공개방 제한과 완료 기록 저장을 활성화하지 않는다.

### 4단계: 재사용 맵 목록 UI와 방 생성 전 선택 — 완료

- 고정 `mapButtons` 연결을 공식·커스텀 목록 프리젠터 방식으로 교체한다.
- 공식맵 목록은 `OfficialMapCatalog`, 커스텀맵 목록은 `SavedMapCatalog`에서 만든다.
- 목록 항목에 이름, 제작자, 버전, 최소 인원, 검증 경고와 완료 상태 영역을 둔다.
- HostRoom에서 방 종류, 맵 종류, 실제 맵, 최대 인원을 방 생성 전에 선택한다.
- 목록 UI는 HostRoom과 향후 GameRoom 확인 화면에서 재사용할 수 있게 한다.
- 최대 인원이 선택 맵 최소 인원보다 작으면 생성할 수 없게 한다.
- `MapSelectionList` 프리팹을 Main과 GameRoom에 배치하고 기존 고정 `mapButtons` 연결과 임시 커스텀 목록 패널을 제거했다.
- 완료 상태 저장은 6단계 범위이므로 현재 목록의 상태 영역에는 선택 가능 여부와 검증 경고를 표시한다.

### 5단계: 방 정책, 네트워크 검증과 Steam 로비 정보 — 완료

- `SteamRoomManager`와 `SteamLobbyMatchmaking`에 `RoomMapPolicy`, `MapKey`, 최소 인원을 전달한다.
- Steam 로비 메타데이터에 맵 종류, MapId, 표시 이름·제작자·버전과 최소 인원을 저장한다.
- 공개 로비 항목에서 선택 맵과 최소 인원을 입장 전에 표시하고 맵 종류·파티 인원 필터 경계를 제공한다.
- 모든 선택과 시작 요청에서 방 정책, `MapKey`, 요구 인원을 호스트 서버가 재검증한다.
- 게임 시작 직전 실제 준비 완료 참가자가 선택 맵 최소 인원 이상인지 다시 확인한다.
- 커스텀맵 선택 업로드·manifest·청크 전송은 `CustomOnly` 방에서만 허용한다.
- 목록 생성·표시 문자열은 `MapListUI`, 네트워크 상태·전송 조정은 `MapSelectionManager`로 책임을 나눴다.

### 6단계: 클리어 기록과 목록 표시 — 완료

- 버전이 있는 JSON `MapCompletionRepository`를 추가한다.
- 서버가 실제 플레이한 `MapKey`와 리비전으로 전원 클리어를 확정한다.
- 모든 참가자가 자신의 로컬 완료 기록을 저장하도록 결과를 전달한다.
- 공식맵은 `CompletionRevision`, 커스텀맵은 `ContentHash`까지 기록해 현재 리비전 완료 여부를 판정한다.
- 일반 완료 배지와 현재 리비전 완료 상태를 구분하고 목록을 즉시 갱신한다.
- 손상 파일, 중복 기록, 저장 실패와 향후 스키마 마이그레이션을 처리한다.

저장 형식은 다음 필드를 사용한다.

```json
{
  "schemaVersion": 1,
  "records": [
    {
      "mapKind": "Official",
      "mapId": "official.stage.01",
      "completionRevision": 1,
      "contentHash": null
    },
    {
      "mapKind": "Custom",
      "mapId": "0123456789abcdef0123456789abcdef",
      "completionRevision": 0,
      "contentHash": "64자리 소문자 SHA-256"
    }
  ]
}
```

- 같은 종류·MapId·리비전 레코드는 로드 시 하나로 정규화한다.
- 잘못된 종류·MapId·리비전·해시는 제외하고 원인을 로그에 남긴다.
- 손상 JSON은 `.corrupt.<UTC ticks>` 파일로 격리한 뒤 빈 저장소로 복구한다.
- 지원하지 않는 `schemaVersion` 파일은 보존하고 덮어쓰지 않아 향후 명시적 마이그레이션 경계를 유지한다.
- 저장은 같은 디렉터리의 임시 파일을 디스크에 flush한 뒤 기존 파일과 원자적으로 교체한다. 교체 실패 시 메모리 변경을 되돌리고 기존 파일을 유지한다.

### 7단계: 미클리어 맵 공개방 제한 — 완료

- 비공개 방은 선택 맵 클리어 여부와 관계없이 생성할 수 있게 한다.
- 공개 방은 방장이 선택 맵의 현재 리비전을 클리어한 경우에만 생성할 수 있게 한다.
- HostRoom UI와 로컬 생성 로직에서 동일 조건을 검사한다.
- 자격 미달 시 공개 생성 버튼을 비활성화하고 비공개 테스트가 필요하다는 이유를 표시한다.
- 로컬 기록 조작을 막는 보안 기능으로 표현하지 않고 소프트 게이트임을 유지한다.

### 8단계: 통합 검증과 정리 — 자동 검증 완료

- 비공개 미클리어 테스트부터 클리어 후 공개방 생성까지 전체 흐름을 검증한다.
- 최소 인원 미달 생성·시작 거부와 충분한 인원의 정상 시작을 검증한다.
- Steam 2~4인에서 로비 정보, 자동 공유, 클리어 기록, 이탈과 재접속을 확인한다.
- 이전 stage index 직접 의존과 런타임 임시 UI 생성을 제거한다.
- 관련 활성 문서의 현재 상태와 다음 작업을 갱신한다.

## 검증

- Unity 6000.5.0f1 배치 모드에서 전체 Edit Mode 테스트 64개가 통과했다. `MapSelectionCore` 55개와 기존 `MapTransferCore` 9개이며 실패·건너뜀은 0개다.
- 7단계 추가 테스트 13개 케이스가 비공개 미완료 공식·커스텀 허용, 공개 미완료·과거 리비전 거부, 현재 리비전 허용, 공통 규칙, 최소 인원 우선 차단, 저장 이벤트 재평가, 직접 생성 게이트, 미지원 스키마와 읽기 실패를 검증한다.
- 최종 Unity 실행은 반환 코드 0으로 종료됐고 Console 로그에 C# 컴파일 오류나 테스트 실패가 없다.
- `dotnet build "Flip Friends.sln"`은 오류 0개로 통과했다. vendor·Unity 직렬화·obsolete 분석 경고는 유지된다.
- Main의 `HostRoomScreen` 버튼·선택/검증 텍스트 참조, Main과 GameRoom의 `MapSelectionList` 인스턴스 오버라이드, GameRoom의 공식 카탈로그 및 24 KiB/512 KiB 전송 설정을 YAML로 재확인했다.
- 공식 카탈로그의 21개 스테이지 프리팹 GUID가 모두 실제 에셋으로 해석되며 MapId·완료 리비전·프리팹 순서는 유지된다.
- 사용되지 않던 legacy stage index 조회 API와 직렬화 필드를 제거했다. `currentStage`, `selectedStage`, `stageMapPrefabs` 및 legacy stage index 검색 결과가 없다.
- 맵 목록 UI에는 프리팹 목록 항목 생성 외에 런타임 임시 `GameObject` 또는 `AddComponent` 경로가 없다.
- 공개 생성 흐름은 `OfficialMapEntry.CompletionRevision` 또는 `SavedMapListEntry.ContentHash` → `PendingRoomMapSelection.CompletionTarget` → `HostRoomScreen` → `SteamRoomManager` → `SteamLobbyMatchmaking` 순서로 동일 저장소 판정을 반복한다.
- 비공개 생성은 유효한 선택과 최소 인원 검사를 유지하되 완료 확인 상태를 차단 사유로 사용하지 않는다.

- 6단계 반영 후 Unity Edit Mode 전체 51개가 통과했다.
- 6단계 저장소 테스트는 공식맵 최초·중복·리비전 변경, 커스텀맵 최초·해시 변경, 종류별 ID 분리, 손상 JSON 격리, 미지원 스키마 보존, 잘못된 값 거부, 중복 정규화, 원자적 교체 실패 보존, 목록 갱신 이벤트를 검증한다.
- 서버 참가자 스냅샷 테스트는 일부 참가자 누락과 예상 외 참가자 포함을 모두 완료로 인정하지 않는 경계를 검증한다.
- 테스트 파일 시스템은 `Path.GetTempPath()` 아래 매 테스트마다 별도 GUID 디렉터리를 주입하며 실제 사용자 저장 경로를 사용하지 않는다.
- 신규 파일을 포함한 `dotnet build "Flip Friends.sln"`이 오류 0개로 통과했다. 기존 vendor·직렬화 분석 경고는 유지된다.
- Unity 스크립트 강제 새로고침과 도메인 리로드 후 Console 컴파일 오류가 0건이었다.
- Unity에서 현재 열린 Main 씬을 검사해 누락 스크립트와 깨진 프리팹이 0건임을 확인했다.
- `MapSelectionList` 프리팹은 `MapListUI`, 공식맵 카탈로그, 맵 팔레트, 목록 항목과 버튼 참조가 유지된다. Main 인스턴스는 `HostRoomScreen`, GameRoom 인스턴스는 `MapSelectionManager`를 각각 오버라이드 참조한다.
- GamePlay의 `GameManager`와 `StageManager`는 각각 `NetworkIdentity`와 함께 존재하며, `StageManager`의 공식맵 카탈로그·맵 팔레트·`MapDataNetworkSync` 프리팹 참조를 YAML GUID·fileID로 확인했다.
- 공식맵 완료 흐름은 `MapSelectionManager → GameplayCompletionSession → StageManager(OfficialMapCatalog 재조회) → GameManager → TargetRpc → MapCompletionRepository → MapListUI` 순서로 정적 추적했다.
- 커스텀맵 완료 흐름은 `MapSelectionManager/ServerMapSessionStore → GameplayCompletionSession(MapId + ContentHash) → StageManager 서버 세션 재검증 → GameManager → TargetRpc → MapCompletionRepository → MapListUI` 순서로 정적 추적했다.
- 공식 카탈로그는 21개 MapId와 21개 양의 완료 리비전을 유지한다. 이전 stage index 직접 로딩 의존은 없고 Steam 로비 맵 메타데이터, 최소 인원 검사, `CustomOnly` 전송 제한, 24 KiB 청크와 512 KiB 상한도 유지됨을 검색으로 확인했다.
- 변경·신규 파일 56개의 엄격한 UTF-8 디코딩이 모두 성공했고 `git diff --check`가 통과했다.

- Unity Edit Mode에서 전체 38개가 통과했다. `MapSelectionCoreTests` 29개와 기존 `MapTransferCoreTests` 9개다.
- 5단계 추가 테스트 9개는 로비 정책·종류 불일치, 최소 인원 범위, 공개 목록 필터, MapKey·요구 인원 재검증, 실제 준비 인원 경계와 `CustomOnly` 전송 제한을 검증한다.
- `dotnet build "Flip Friends.sln"`과 신규 테스트 어셈블리 빌드가 오류 없이 통과했다.
- Unity에서 `OfficialMapCatalog`를 로드해 21개 항목과 검증 오류 0개를 확인했다.
- 카탈로그의 21개 `{fileID, guid}` 프리팹 참조가 기존 GamePlay 배열과 모두 일치한다.
- `dotnet build "Flip Friends.sln"`이 오류 0개로 통과했다.
- 이전 stage index 의존 검색과 맵 종류 빈 문자열 추론 검색 결과가 없다.
- `GameRoom`과 `GamePlay` 씬에는 동일한 `OfficialMapCatalog` 참조만 연결했으며 관련 없는 씬 변경은 없다.
- 요구조건 경계 테스트가 1명·4명을 허용하고 0명·5명을 거부하며, 카탈로그의 잘못된 최소 인원과 완료 리비전을 거부한다.
- 공식맵 카탈로그 21개 항목 모두 최소 인원 1명, 완료 리비전 1이며 카탈로그 검증 규칙을 통과한다.
- Main과 GameRoom의 `MapSelectionList` 인스턴스가 각각 `HostRoomScreen`, `MapSelectionManager`를 참조하고 공용 프리팹의 카탈로그·팔레트·목록 항목 참조가 모두 연결됐다.
- GameRoom의 고정 `mapButtons` 직렬화와 이전 임시 `MapListUI` 컴포넌트가 제거됐다.
- Main 씬 Unity 검증에서 누락 스크립트와 깨진 프리팹이 0건이었다. GameRoom·GamePlay와 `MapSelectionList`는 관련 GUID·fileID 및 카탈로그·팔레트·화면 컨텍스트 참조를 정적으로 확인했다.
- `dotnet build "Flip Friends.sln"`이 5단계 반영 후에도 오류 0개로 통과했다. 기존 vendor·직렬화 분석 경고는 유지된다.

## 남은 작업

- 실제 Steam 2~4인 환경의 공개·비공개 생성, 자동 공유, 클리어 기록, 이탈·재접속 수동 검증
- 공식맵 썸네일과 최종 표시 이름 확정
- 클리어 진행도의 Steam Cloud 동기화 여부는 별도 결정

## 다음 작업

Steam 2~4인 환경에서 공개·비공개 로비와 커스텀맵 자동 공유를 수동 검증한다.
