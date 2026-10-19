# 맵 선택 및 완료 기록

- 상태: Active
- 범위: 방 생성 전 맵 선택, 최소 인원, 완료 리비전, 공개방 자격

## 현재 상태

- 방 생성 전에 공식·커스텀 맵을 선택한다. 방 생성 후 정책과 맵은 고정된다.
- 공식맵은 `OfficialMapCatalog`의 `official.stage.01`~`21`과 `CompletionRevision`으로 식별한다. 기존 `BuiltInMapCatalog`는 런타임에서 사용하지 않는다.
- 커스텀맵은 정규 GUID `MapId`와 정규 JSON의 SHA-256 `ContentHash`로 식별한다. 저장 형식 2.2에 최소 인원을 보관하며 2.0·2.1은 1명으로 마이그레이션한다.
- 최소 인원은 1~4명이다. 방의 최대 인원과 시작 직전 실제 준비 인원을 각각 검사한다.
- 비공개 방은 완료 여부와 관계없이 생성할 수 있다. 공개방은 호스트가 선택한 현재 리비전을 완료해야 생성할 수 있다.
- `HostRoomScreen`, `SteamRoomManager`, `SteamLobbyMatchmaking`에서 같은 `RoomCreationRules`를 재검사한다. 설정을 자동으로 비공개로 바꾸지 않고 차단 이유를 표시한다.
- Steam 로비 메타데이터에 맵 종류·ID·표시 이름·제작자·버전·최소 인원을 게시한다. 메타데이터 없는 구형 로비는 공개 목록에서 제외한다.
- `MapSelectionList`를 Main과 GameRoom에서 재사용한다. 목록·필터·시작 버튼은 기존 키보드·게임패드 탐색과 `ScrollViewController`에 연결된다.
- 맵 완료 상태와 공개방 자격은 로컬 진행도다. 보안·부정행위 방지 수단이 아니다.
- 서버가 시작 당시 맵과 준비 참가자 연결을 `GameplayCompletionSession`으로 고정한다. `StageManager`는 실제 로드에 성공한 리비전을 다시 검증한다.
- 게임 중 이탈자는 완료 대상에서 제거하고 남은 최초 참가자 전원이 완료하면 한 번만 결과를 보낸다. 뒤늦은 다른 연결이나 빈 참가자 목록은 완료가 아니다. 이는 main의 이탈 후 클리어 동작을 유지한다.
- 에디터의 저장 전 1인 테스트 플레이는 기존 복귀 흐름을 사용하며 완료 기록을 남기지 않는다. 로컬 네트워크 테스트는 카탈로그의 첫 공식맵을 사전 선택한다.
- 커스텀맵 생성은 `CustomMapRuntimeLoader`와 `PlacedObjectFactory`를 유지한다. 로드 실패 정리·사유 안내·낙하 복귀·게임 중 입장 차단도 유지한다.
- 자동 공유는 `CustomOnly` 방에서만 허용한다. `MapAvailabilityCheck` 상태 관리, 24 KiB 청크, 512 KiB 상한, 연결별 전송 큐를 유지한다.
- 완료 기록은 `Progress/map-completions.json`에 schemaVersion 1 JSON으로 저장한다. Unity에서 제공되는 Newtonsoft.Json을 사용한다. 임시 파일 flush와 원자적 교체를 사용하며 저장 실패 시 메모리 변경을 되돌린다.
- 손상 기록은 격리한다. 미지원 스키마와 읽기 실패는 원본을 보존하고 공개방 자격을 확인 불가로 처리한다.

## 핵심 흐름

HostRoomScreen → PendingRoomMapSelection → Steam 로비 메타데이터 → GameRoom 서버 재검증 → GameplayCompletionSession → StageManager → GameManager → 참가자별 로컬 완료 기록.

## 검증

- Unity 6000.6.3f1에서 Edit Mode 전체 161/161개 통과, 실패·건너뜀 0개. Unity 컴파일과 별도 Assembly-CSharp 빌드 오류 0개.
- Main·GameRoom·GamePlay의 참조·누락 스크립트와 21개 공식맵 스폰 목록을 Unity 에디터에서 검증했다. [호스트 화면 미리보기](Images/map-selection-host.png)도 확인했다.
- `MapCompletionRepositoryTests`는 원자적 저장, 스키마·손상 처리, 현재 리비전 자격, 최초 참가자·이탈·빈 세션 경계를 검증한다.
- `MapDataRepositoryTests`는 2.0·2.1 마이그레이션, MapId 보존, 잘못된 최소 인원 저장 거부를 검증한다. 기존 `MapAvailabilityCheck` 테스트도 함께 실행한다.
- `Phase4MapSelectionSceneBuilder.BuildAndValidate`는 Unity에서 세 씬의 카탈로그 참조·누락 스크립트와 21개 공식맵 스폰 등록을 확인하고 호스트 화면 미리보기를 만든다. 미리보기는 격리된 저장소를 사용한다.

## 남은 작업

- 실제 Steam 2~4인 공개·비공개 생성, 자동 공유, 참가자별 기록 저장, 이탈·재접속 수동 검증.
- 공식맵 최종 표시 이름·썸네일과 Steam Cloud 진행도 동기화 여부 확정.

## 다음 작업

Steam 2인 환경에서 비공개 미완료 맵 플레이 → 완료 기록 → 공개방 생성까지 확인한다.
