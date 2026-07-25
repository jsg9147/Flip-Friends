# 유저 맵 에디터 시스템 로드맵

## 목표
방장이 인게임 에디터로 맵을 제작하고, 해당 맵 데이터를 모든 클라이언트와 공유하여 함께 플레이하는 시스템 구축.

---

## 전체 아키텍처 흐름

```
[방장] MapEditor 씬에서 맵 제작
    → JSON 직렬화 (MapData)
    → SteamRoomManager가 Steam 로비 메타데이터 또는 Mirror SyncVar로 전송
    → 모든 클라이언트가 동일한 MapData 수신
    → StageManager가 MapData를 파싱하여 오브젝트 Spawn
    → 기존 GamePlay 씬에서 플레이
```

---

## 단계별 로드맵

### ✅ 사전 분석 완료
- `StageManager.StageLoad()` — prefab 인덱스(int)로 통째로 하나의 스테이지 프리팹을 Spawn
- `SlimeRoomManager.currentStage` — int 인덱스를 게임 시작 시 전달
- `SteamRoomManager` — Steam 로비 메타데이터 Key/Value 시스템 보유 (`SetLobbyData` / `GetLobbyData`)
- Mirror 패킷 기본 한계: ~64KB → 대형 맵은 압축 또는 분할 전송 고려 필요

---

### 🔲 1단계: 맵 데이터 직렬화 구조 설계
**상태**: 미완료  
**목표**: 맵 데이터를 JSON으로 저장/불러오기하는 핵심 데이터 클래스 작성

**작업 목록**:
- [x] `MapData.cs` 작성 — 직렬화 가능한 맵 데이터 컨테이너
  - `string mapName`
  - `string authorName`
  - `string version`
  - `List<PlacedObjectData> objects`
- [x] `PlacedObjectData.cs` 작성 — 배치된 오브젝트 하나의 데이터
  - `int prefabID` — `MapEditorPalette`의 팔레트 인덱스
  - `SerializableVector3 position` (JsonUtility Vector3 직렬화 지원)
  - `float rotation` (Z축)
  - `SerializableVector3 scale`
- [x] `MapDataRepository.cs` 작성 — JSON 파일 저장/불러오기 유틸
  - `Save(MapData data)` — `persistentDataPath/Maps/{mapName}.json`
  - `Load(string mapName) : MapData`
  - `GetAllMapNames() : List<string>`
  - `ToJson / FromJson` — 네트워크 전송용 직렬화
- [x] `MapEditorPalette.cs` 작성 — 배치 가능한 프리팹 목록 (ScriptableObject)
  - `List<PaletteEntry> entries` (prefabID → GameObject 매핑)
  - Inspector에서 `[CreateAssetMenu]`로 생성 가능

**파일 위치**: `Assets/01_Scripts/MapEditorScripts/`

---

### 🔲 2단계: StageManager 맵 로드 방식 확장
**상태**: 미완료  
**의존**: 1단계 완료 후 진행  
**목표**: 기존 prefab 인덱스 방식 유지하면서 MapData 방식도 지원

**작업 목록**:
- [x] `SlimeRoomManager`에 `currentMapData` (string, JSON) 필드 추가
- [x] `StageManager.StageLoad()` 리팩터
  - `currentMapData`가 있으면 → `LoadFromMapData()` 호출
  - 없으면 → 기존 `LoadPrefabStage()` (하위 호환 유지)
- [x] `StageManager.LoadFromMapData(MapData data)` 구현
  - `MapEditorPalette`에서 prefabID로 프리팹 조회
  - 각 `PlacedObjectData`마다 `Instantiate` + `NetworkServer.Spawn`
  - `SpawnPlacedObject()` 메서드로 SRP 분리

**수정 파일**:
- `Assets/01_Scripts/PlayingScripts/StageManager.cs`
- `Assets/01_Scripts/NetworkScripts/SlimeRoomManager.cs`

---

### 🔲 3단계: 맵 데이터 네트워크 전송
**상태**: 미완료  
**의존**: 2단계 완료 후 진행  
**목표**: 방장의 MapData를 게임 시작 전 모든 클라이언트에 전달

**작업 목록**:
- [x] **선택한 방식**: 서버 전용 처리 + 별도 `MapDataNetworkSync` NetworkBehaviour
  - StageManager는 서버에서만 Spawn하므로 클라이언트에 JSON 전송 불필요
  - `MapDataNetworkSync` (SyncVar)로 클라이언트도 JSON 조회 가능 (맵 이름 표시, 로컬 저장 등)
  - Mirror 패킷 한계(~64KB) 범위 내에서 동작
- [x] `CustomRoomPlayer.CmdCustomMapSelect(string mapJson)` 추가 — 커스텀 맵 선택 Command
- [x] `MapSelectionManager.CustomMapLoad(string mapJson)` 추가 — 커스텀 맵 진입점
- [x] `SlimeRoomManager.ReturnRoomScene()` — 룸 복귀 시 `currentMapData` 초기화
- [x] `MapDataNetworkSync.cs` 신규 생성 — SyncVar로 JSON 전파, `GetMapData()` 조회 메서드
- [x] `StageManager.SpawnMapDataSync()` — 커스텀 맵 로드 시 MapDataNetworkSync 오브젝트 Spawn

---

### 🔲 4단계: 맵 에디터 UI 씬
**상태**: 미완료  
**의존**: 1~3단계 완료 후 진행  
**목표**: 방장이 직접 오브젝트를 배치하여 맵을 만드는 에디터 UI

**작업 목록**:
- [x] `SampleScene.unity` → 맵 에디터 씬으로 활용 (MCP로 UI 자동 배치 완료)
- [x] `MapEditorManager.cs` — 싱글톤, MapData 상태 관리, Save/Load, ReturnToLobby
- [x] `ObjectPlacer.cs` — 그리드 스냅 배치/삭제, 고스트 미리보기, `RebuildFromMapData`, `ClearAll`
- [x] `PaletteUI.cs` — 팔레트 버튼 동적 생성, 선택 시 ObjectPlacer에 prefabID 전달
- [x] `MapEditorHUD.cs` — 맵 이름 InputField, 저장/불러오기/새 맵/뒤로가기 버튼, 로드 팝업
- [x] `MapEditorCamera.cs` — 중간 버튼 패닝, 스크롤 줌 (orthographicSize)
- **배치 방식**: 실제 프리팹 대신 `PaletteEntry.thumbnail` 스프라이트로 표시 (게임 스크립트 미실행)
- **미구현 (5단계로 이동)**: 테스트 플레이 씬 전환, 맵 썸네일 자동 생성

---

### ✅ 5단계: 맵 공유 및 저장 시스템
**상태**: 완료  
**의존**: 4단계 완료 후 진행  
**목표**: 로컬 맵 파일 관리 및 로비 내 맵 공유

**작업 목록**:
- [x] `MapDataNetworkSync.cs` — SyncVar hook + `OnMapDataReceived` 이벤트 추가
- [x] `MapListUI.cs` — GameRoom 씬에서 방장이 로컬 맵을 선택, `MapSelectionManager.CustomMapLoad()` 호출
- [x] `MapReceivePrompt.cs` — GamePlay 씬에서 클라이언트가 수신한 맵 저장 여부 선택 (Coroutine으로 `MapDataNetworkSync` Spawn 대기 후 이벤트 구독)
- [ ] 맵 썸네일 자동 생성 (에디터 카메라 스크린샷) — 선택 사항
- [ ] (선택) Steam 클라우드 저장 연동

**Unity Editor 연결 필요 사항**:
- `StageManager` Inspector: `Palette` 필드 → `MapEditorPalette` 에셋, `mapDataSyncPrefab` → `MapDataNetworkSync` 컴포넌트를 가진 프리팹
- `MapDataNetworkSync` 프리팹 → Mirror의 Registered Spawnable Prefabs에 등록
- `MapEditorManager` → `Palette` 필드 연결
- `MapListUI` 컴포넌트를 GameRoom 씬의 UI GO에 추가, 필드 연결 (`MapSelectionManager` 참조 포함)
- `MapReceivePrompt` 컴포넌트를 GamePlay 씬에 추가, 팝업 패널 UI 구성 후 연결
- Build Settings에 `SampleScene` (맵 에디터 씬) 추가
- 메인 메뉴에 맵 에디터 진입 버튼 추가

---

## 파일 구조 (신규 생성)

```
Assets/
├── 01_Scripts/
│   └── MapEditorScripts/
│       ├── Data/
│       │   ├── MapData.cs
│       │   ├── PlacedObjectData.cs
│       │   └── MapDataRepository.cs
│       ├── Editor/
│       │   ├── MapEditorManager.cs
│       │   ├── ObjectPlacer.cs
│       │   ├── MapEditorCamera.cs
│       │   └── PaletteUI.cs
│       └── Network/
│           └── MapDataNetworkSync.cs
├── 05_ScriptableObjects/
│   └── MapEditorPalette.asset
└── 00_Scenes/
    └── MapEditor.unity
```

---

## 진행 체크리스트

| 단계 | 상태 | 완료일 |
|------|------|--------|
| 사전 분석 | ✅ 완료 | 2026-07-12 |
| 1단계: 데이터 직렬화 | ✅ 완료 | 2026-07-12 |
| 2단계: StageManager 확장 | ✅ 완료 | 2026-07-12 |
| 3단계: 네트워크 전송 | ✅ 완료 | 2026-07-12 |
| 4단계: 에디터 UI + 씬 설정 | ✅ 완료 | 2026-07-12 |
| 5단계: 맵 공유/저장 | ✅ 완료 | 2026-07-18 |

---

## 주요 결정 사항 (작업 중 기록)

- **2026-07-12**: 기존 프로젝트 유지 결정 — Mirror + Steam 네트워킹이 이미 완성되어 있어 새 프로젝트보다 효율적
- **2026-07-12**: 1단계부터 순차 진행하기로 결정
- **3단계 네트워크 전송 방식**: 아직 미결정 (옵션 A/B/C 중 택일 필요)

---

## 참고 파일

- `Assets/01_Scripts/PlayingScripts/StageManager.cs` — 수정 대상 (2단계)
- `Assets/01_Scripts/NetworkScripts/SlimeRoomManager.cs` — 수정 대상 (2~3단계)
- `Assets/01_Scripts/NetworkScripts/SteamRoomManager.cs` — Steam 로비 데이터 전송 참고
