# 맵 에디터 현재 상태

- 상태: Active
- 최종 갱신: 2026-10-10
- 범위: 맵 데이터, 편집, 저장소, 팔레트, 플레이 가능성 검증

## 현재 상태

- Quantum 독립 시험은 기존 `MapData` 2.1·검증기·팔레트를 재사용한다. Mirror 생성/전송과 분리된 변환 v2·팔레트 v2에서 정적 박스와 `terrain.rope` 루트 박스 트리거를 처리한다. 로프 오프셋에도 배치 스케일·180도 회전을 적용하며 중심/크기는 불변 FP 설정에 담고 접촉/등반은 Quantum이 판정한다. 가로/경사 로프·자식/원형/합성/동적/비트리거 등 미지원 데이터는 위치·원인과 함께 전체 거부한다. [세부 규칙](QUANTUM_MIGRATION.md)을 따른다. 기존 저장 맵과 정적 시험 JSON을 보존했고 별도 `FlipFriendsRopeMap.json`·`QuantumRopeTest.unity`를 추가했다.
- 공용 Cannon/MapEditorCannon은 서버에서 탄환을 초기화한 뒤 Spawn하고, CannonProjectile이 거리별 수명을 관리한다. 원격 탄환은 서버 위치만 따르며 팔레트 저장 스키마와 발사 설정은 유지했다.

- 팔레트 `interactive.box`는 공용 Box.prefab을 사용한다. 단계 2의 PickupObj 서버 리셋 API가 추락 복귀·운반 해제에 적용되며 저장 형식/팔레트 연결은 유지한다.

- `MapData` 스키마는 2.1이며 정규 GUID `N` 형식의 `mapId`를 사용한다.
- 2.0 로컬 데이터는 최초 로드 시 2.1로 안전하게 마이그레이션한다. 잘못된 2.1 ID는 자동 교체하지 않고 거부한다.
- 저장 API는 생성, 수정, 가져오기를 구분하며 같은 MapId나 파일명의 무단 덮어쓰기를 차단한다.
- 맵 목록은 MapId, 파일명, 표시 이름을 분리하고 중복 ID 항목의 선택을 막는다.
- 팔레트 문자열 ID와 검증 규칙을 사용한다. 플레이어 시작점과 도착점 등 필수 조건을 검사한다.
- 배치, 드래그 연속 배치·삭제, 회전, 반전, 저장·불러오기 UI가 구현되어 있다.
- `Data`·`Validation` 폴더는 `FlipFriends.MapEditorCore` 어셈블리다(`Validation`은 asmref). Mirror에 의존하지 않으며, 검증기는 `MapEditorPalette` 대신 `IMapPaletteLookup`을 받는다. Mirror 패킷 한계를 쓰는 `SavedMapCatalog`는 Assembly-CSharp에 남는다.
- HUD의 `테스트 플레이`는 저장하지 않은 편집 상태로 혼자 플레이한다. `SlimeRoomManager.TryStartTestPlay`가 Steam 로비 없이 호스트를 열고 GameRoom을 바로 지나 GamePlay로 간다. 도착하거나 메뉴에서 나가면 호스트를 끄고 에디터로 돌아온다. Main 씬을 거쳐 들어와야 네트워크 매니저가 있다.
- 편집 상태(MapData 복사본, 저장 파일명)는 `MapEditorDraftStore`가 씬 전환 동안 맡고, `MapEditorManager.Awake`가 한 번 꺼내 복원한다.
- 커스텀 맵과 테스트 플레이에서도 추락이 막힌다. 서버가 `MapRenderBounds`로 맵 데이터와 팔레트 프리팹의 렌더러 범위를 계산해 `FallBoundary`를 만들고, 밖으로 나간 플레이어는 시작 지점으로, 상자는 배치 위치로 돌려보낸다. 리셋 키도 세이브 포인트가 없으면 원점이 아니라 시작 지점으로 보낸다.
- `MapBoundsPreview`(MapEditor 씬)가 배치 가능 범위를 흰 사각형으로, 추락 경계를 위가 열린 빨간 ㄷ자로 그린다. 추락 경계는 런타임과 같은 `MapRenderBounds` 계산이며, 이동·회전·크기 변경도 데이터 서명 비교로 바로 반영한다. 에디터 화면은 썸네일을 그리므로 선이 썸네일 크기와 어긋나 보일 수 있다.
- 로비 선택과 자동 공유는 `Docs/CUSTOM_MAP_NETWORK.md`가 담당한다.

## 핵심 흐름

```text
MapEditorManager
  → MapData / PlacedObjectData
  → MapDataValidator + MapEditorPalette
  → MapDataRepository
  → Application.persistentDataPath/Maps
```

주요 위치:

- `Assets/01_Scripts/MapEditorScripts/Data`
- `Assets/01_Scripts/MapEditorScripts/Validation`
- `Assets/01_Scripts/MapEditorScripts/UI`
- `Assets/07_ScriptableObject/MapEditorPalette.asset`

## 검증

- 2026-11-06 로프 반복/역순 변환·해시·오프셋·반전/180도·미지원 데이터 위치 진단/전체 거부·실제 검증 에셋의 해시/두 버전 불일치 거부를 통과했다. 두 플레이어의 로프 상태 전이와 새 러너 재생 확정 120~960 체크섬 8개 일치를 실행했다. Unity API로 씬/맵/검증 에셋을 저장·재로드했으며 최종 정적 회귀·Photon 결과는 [Quantum 문서](QUANTUM_MIGRATION.md)를 따른다.
- 2026-11-06 Quantum 시험 JSON의 기존 검증기 통과, 반복/역순 변환 동일성·해시 변화·변환/팔레트 버전 불일치와 미지원 데이터 거부를 실행했다. 새 씬·맵 에셋 저장/재로드, 두 플레이어의 지형 충돌·확정 프레임 입력 재생과 개발 Photon 같은 PC 2연결은 `Docs/QUANTUM_MIGRATION.md`의 이번 결과를 따른다. 기존 편집 UI 전체 이식을 뜻하지 않는다.
- 2026-10-26 Main → MapEditor → API 임시 배치 5개(시작/도착/지형/공용 Box/MapEditorCannon) → KCP 테스트 플레이를 실행했다. 서버 탄환 속도 10·실제 비행·대포 중지 후 거리 소멸, 상자 낙하/경계 아래 Release 후 추락 복귀, 운반 API 후 ServerReset의 참조 해제와 낙하 안정을 확인했다. API 완료/ExitGame 두 경로 모두 에디터 복귀 후 편집 JSON이 전후 완전히 같았으며 Main 복귀도 확인했다. 저장하지 않았고 UI 배치·실조작 클리어 검증과 구분한다. [상세](Notes/2026-10-26-stage-live-validation.md).

- 2026-10-26 팔레트 6종의 루트 Identity·Main spawn 등록, MapEditorCannon의 공용 투사체/서버 권한 참조 검사를 통과했다. 실제 편집 → 테스트 플레이의 탄환 비행·거리 제거·복귀는 미실행이다.

- 2026-10-26 팔레트 프리팹 전체에 PlatformController가 없는 것을 확인했다. 단계 4 발판 예측 변경으로 팔레트/맵 저장 형식을 바꾸지 않았으며, 맵 에디터 테스트 플레이의 플레이어 이동 회귀는 수동 확인이 남는다.

- 2026-10-26 공용 Box의 루트 NetworkIdentity 1개·Pickable 레이어와 팔레트 연결을 재확인했다. 격리 기본 스테이지 상자 리셋 검사는 통과했으나 맵 에디터 테스트 플레이의 실제 추락·운반 회귀는 미실행이다.

- C# 및 Unity 컴파일을 통과한 상태다. Unity `6000.6.3f1`에서도 `Assembly-CSharp` 빌드가 오류 없이 통과한다.
- 실제 1600x900 편집 UI, Main → MapEditor → Main 빌드 이동, 전체 편집 시나리오는 수동 검증이 남아 있다.
- `FlipFriends.MapEditorCore.Tests` Edit Mode 테스트 63개가 통과한다(2026-10-07, 전체 72개).
  - `MapDataRepositoryTests`: 임시 폴더(`SaveDirectoryOverride`)에서 저장·불러오기 왕복, MapId·파일명 중복 거부, 이름 변경 이동, 경로 이탈·예약어 거부, 빈·손상 JSON, 2.0 → 2.1 변환 후 재기록, 대문자 MapId 정규화, 미지원 버전, 잘못된 2.1 ID 거부, MapId 조회와 중복 거부를 보장한다.
  - `MapEditorDraftStoreTests`: 편집 상태와 파일명 보관·복원, 한 번 꺼내면 비워짐, 원본 변경과 분리된 복사본, null 거부를 보장한다.
  - `MapDataValidatorTests`: 시작점·도착점 최소 개수, 등록되지 않은·빈 팔레트 ID, 팔레트 없음, 비정규 MapId, 경계 밖·NaN 좌표, 최대 개수, 겹침 경고·치명 오류·허용 오차를 보장한다.

- 테스트 플레이는 2026-10-07 에디터 플레이 모드(Steam 로그인)에서 Main → MapEditor → 테스트 플레이 → GamePlay를 확인했다. 시작 지점에서 생성돼 지면 위에 서고, 도착 판정·메뉴 나가기·직접 클리어 요청 세 경로 모두 에디터로 돌아와 같은 MapId·이름·오브젝트 수와 표시 오브젝트가 복원됐다. 이후 Main 복귀도 정상이다. GameRoom을 바로 지나가며 방 플레이어 Command가 "client not ready" 경고로 버려지지만 게임 플레이어는 이름·색을 스스로 다시 보낸다.
- 2026-10-13 에디터 플레이 모드에서 범위 선 두 개가 그려지고 오브젝트 이동 시 추락 경계가 갱신되는 것을 확인했다. 팔레트 6종을 회전·스케일해 배치했을 때 `MapRenderBounds` 결과가 실제 프리팹 렌더러 범위와 일치했다.
- 추락 리스폰(플레이어·상자·리셋 키)은 2026-10-11 컴파일만 확인했다. 테스트 플레이에서 맵 아래·옆으로 떨어지기, 상자 떨어뜨리기, 들고 떨어지기를 수동 확인해야 한다. 2인 운반 중 추락은 `LocalNetworkTest`로 확인한다.
- 2026-10-10 사용자가 Windows 빌드에서 실제 키보드·게임패드로 테스트 플레이를 조작하고, 도착과 메뉴 나가기 양쪽에서 에디터로 돌아오는 것을 수동 확인했다.

## 남은 작업

- Undo/Redo, 복사·붙여넣기, 다중 선택은 후순위

## 다음 작업

Quantum의 로프 지원 형태와 v2 해시 경계를 유지한다. 가로/경사·동적 로프와 전체 맵 에디터는 별도 범위다. 기존 Mirror 편집 UI의 Box/Cannon 배치·운반·리셋·저장/불러오기 실조작 회귀는 별도로 남는다.
