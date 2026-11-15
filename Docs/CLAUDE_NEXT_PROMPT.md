# Claude Code 작업 요청 — Mirror 제거 (전체 교체 순서 8)

`C:\Unity Project\Flip-Friends`에서 Mirror를 프로젝트에서 완전히 제거해줘. 2026-11-14 사용자 결정으로 **Mirror 버전은 출시하지 않는다.** 제품 경로는 Quantum뿐이고 Mirror는 병행 운영하지 않는다. 기준은 브랜치 `quantum-mirror-decoupling`(커밋 `56f48d8`)이다. Steam 로비(`SteamLobbyService`)와 Quantum 팔레트(`QuantumMapEditorPalette`)는 이미 Mirror에서 분리했다. 조사나 계획에서 멈추지 말고 제거, 검증, 문서 갱신까지 진행해줘.

## 착수

1. `CLAUDE.md`, `PROJECT_DOCUMENTATION.md`, `Docs/NEXT_SESSION.md`, `Docs/QUANTUM_MIGRATION.md`, `Docs/CUSTOM_MAP_NETWORK.md`, `Docs/MAP_EDITOR.md`, `.claude/rules/network.md`를 읽어.
2. Git 상태와 열린 Unity 씬의 미저장 여부를 확인해. 다른 작업자(Codex)의 미커밋 변경(예: `codex_map_editor_*` 이미지, `Assets/Editor/`, `Docs/MAP_EDITOR.md`)을 건드리거나 커밋하지 말고, 겹치는 파일이 있으면 먼저 알려줘. 에디터의 미저장 씬을 저장하거나 버리지 마.
3. 제거 전에 기준값을 기록해. 공식 시험 8종 해시, Quantum 매니페스트 6종 해시·`ValidateAndApply` 결과, 새 Play 첫 Main `QuantumProductChecks.RunOffline` 결과가 대상이다.

## 사용자에게 먼저 물을 것

- **원본 스테이지·장애물 프리팹**(`Assets/03_Prefabs/Stage` 21개, `Obstacle`, `OnOff` 등)은 Mirror 장치 스크립트(`BasicTrap`, `MovingTrap`, `Conveyor`, `CannonShooter`, `OnOffManager`, `SavePoint`, `RespawnHandler`, `ObjSummonSwitch`, `PickupObj` 등)를 쓴다. 다음 중 하나를 고르게 해.
  - (가) 스크립트를 지우고 프리팹의 Missing Script를 정리해 지형·스프라이트 참고용으로 남긴다.
  - (나) 원본 장치 수치를 `Docs/Notes/`에 뽑아 둔 뒤 (가)를 한다.
  - (다) 원본 프리팹도 지운다.
- NGO 패키지(`com.unity.netcode.gameobjects`, `Assets/NGO_Minimal_Setup`)도 이번에 지울지 물어.
- 그 밖에 Mirror 전용임이 확실한 코드·UI(기존 경로 버튼, Mirror 방 화면 등)는 묻지 말고 제거해.

## 알려진 의존 (착수 시 다시 확인)

- 공용·Quantum 코드에 남은 Mirror 연결:
  - `SavedMapCatalog`의 Mirror Transport 패킷 한계 분기. Quantum은 `MapSessionSnapshot.MaximumContentBytes`를 쓴다.
  - `MainMenuScreen.OpenMapEditor`의 `NetworkServer/NetworkClient` 검사.
  - `QuantumPrepareScreen`의 `legacyButton`("기존 경로")과 Mirror 방 화면(`HostRoomScreen`, `PublicLobbyScreen`, `PrivateJoinScreen`, `LobbyItem`).
  - `SteamLobbyService`의 외부 세션 API와 `SteamLobbyMatchmaking`의 Mirror 모드(HostAddressKey, 공개 목록, 비Quantum 생성 분기).
- `SteamLobbyService`는 지금 `SteamRoomManager.Start`가 만든다. 초대 콜백과 `+connect_lobby`를 놓치지 않도록 Main에서 미리 만드는 Mirror 비의존 소유자를 둬. `SteamAPI.Init`/`Shutdown`은 Steamworks.NET `SteamManager`에만 맡겨.
- 원본 Mirror 컴포넌트를 읽는 Quantum 에디터 도구·검사:
  - `QuantumDeviceSetup`, `QuantumOfficialTestStageSetup`, `QuantumTilemapReader.ReadReset`, `QuantumTilemapSetup`
  - `QuantumDeviceChecks`, `QuantumRespawnChecks`, `QuantumOfficialStageChecks`(Mirror 원본 스크립트 거부 검사), `QuantumMotionChecks`/`QuantumConveyorChecks`(원본 `MovingTrap`+`NetworkIdentity` 비교), `QuantumProductChecks`의 "Mirror 비활성 유지"
  - 생성 결과물이 이미 커밋된 도구는 원본 값을 상수·Notes로 옮기거나 도구를 지워. 결과 해시는 바뀌면 안 된다.
- 씬·에셋:
  - `GameRoom.unity`, `GamePlay.unity`(Build Settings 포함), `Main.unity`의 `SteamRoomManager`·`SlimeRoomManager`와 spawnPrefabs
  - `QuantumDeviceTest.unity`·`QuantumTilemapTest.unity`의 Mirror 컴포넌트
  - `Player.prefab`, `RoomPlayer.prefab`, `03_Prefabs/Network`, `DefaultNetworkPrefabs.asset`
  - 기존 `MapEditorPalette.asset`과 원본 팔레트 프리팹(`Box.prefab`, `MapPrefabs/Rope.prefab`, `MapEditor/*`)
  - 지우기 전에 Quantum 씬·`Resources`·공식 스테이지에서 `AssetDatabase.GetDependencies`로 참조(애니메이터, 스프라이트, 사운드 등)를 확인해. 필요한 에셋은 남기거나 옮겨.
- 스크립트: `using Mirror`/`NetworkBehaviour` 파일 약 55개(`NetworkScripts`, `PlayerScripts`, `GameObjScripts`, `ObstacleScripts`, `PlayingScripts`, `MapEditorScripts/Network`, `UI Scripts/Room` 등)와 `LocalNetworkTest`·`LocalStagePlayProbe`·`StageLifecycleTestRoomManager`·`PlayingScripts/Editor/Stage*`
- 벤더·설정: `Assets/Mirror` 전체(FizzySteamworks 포함), Standalone 스크립팅 심볼의 `MIRROR*`·`EDGEGAP_PLUGIN_SERVERS`. `Assets/Tests/EditMode`는 Mirror 참조가 없었다(재확인).

## 수행 순서

1. Quantum·공용 코드의 남은 Mirror 연결을 끊는다. 이 단계 끝에서 Quantum 코드가 Mirror 타입을 하나도 참조하지 않아야 한다.
2. Mirror 진입 UI와 씬·프리팹 연결을 제거한다. 씬·프리팹은 에디터 API로 고치고 YAML을 대량 수동 편집하지 마. `.meta` GUID를 직접 쓰지 마.
3. Mirror 의존 스크립트와 결정에 따른 원본 프리팹을 처리한다.
4. `Assets/Mirror`와 스크립팅 심볼을 지운다.
5. 각 단계 끝에서 컴파일이 깨지지 않게 하고, 단계별로 한국어 커밋을 나눠.

## 검증과 완료

- 저장소 지정 dotnet 빌드와 Unity 재컴파일 오류 0. 프로젝트 코드에서 `Mirror`·`NetworkBehaviour`·`NetworkServer` 검색 결과 0(주석·문서의 이력 설명 제외).
- 착수 기준값과 비교한다.
  - 공식 시험 8종 해시, 매니페스트 6종 해시·`ValidateAndApply`가 같아야 한다.
  - 새 Play 첫 Main `RunOffline` 전체 통과, 이어서 `QuantumLobbyChecks`(개발 Photon)를 실행한다.
  - MapEditor 진입과 HUD 테스트 플레이 왕복을 확인한다.
  - EditMode 테스트를 실행한다. CLI는 에디터를 닫아야 하니, 열려 있으면 Test Runner/MCP를 쓰거나 실행하지 못했다고 적어.
- Windows Standalone 빌드가 컴파일되는지 확인한다. 실행하지 못했으면 미실행으로 적는다.
- 문서를 Quantum 기준으로 정리한다.
  - `.claude/rules/network.md`를 Quantum 규칙으로 다시 쓴다(60줄 이하, `paths` 갱신). `CLAUDE.md`의 Mirror 언급(환경 제약, 벤더 목록, 싱글톤 함정)과 `AGENTS.md`도 정리한다.
  - `PROJECT_DOCUMENTATION.md`의 라우팅을 고친다.
  - `CUSTOM_MAP_NETWORK.md`·`MAP_EDITOR.md`(팔레트 두 벌 설명)·`PLAYER_MOVEMENT_SYNC.md`·`PLAYER_ANIMATION.md`·`STAGE_COMPONENTS.md`의 Mirror 절은 지우거나 Superseded/Historical로 표시한다.
  - `NEXT_SESSION.md` 전체 교체 순서 8을 완료로 표시한다.
- Steam 실계정·다른 PC·배포 빌드 실행 등 실제로 하지 않은 검사는 통과로 표시하지 마. 제거 범위, 검증 결과, 남은 제약을 한국어로 보고하고, 완료 후 이 프롬프트 파일은 정책에 따라 지워.
