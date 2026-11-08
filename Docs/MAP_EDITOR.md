# 맵 에디터 현재 상태

- 상태: Active
- 문서 정리: 2026-11-08 (실행 검증 날짜는 검증 절 참조)
- 범위: 맵 데이터, 편집, 저장소, 팔레트, 플레이 가능성 검증

## 현재 상태

- 새 장치 맵은 MapData2.1+별도 QuantumMapDevices 콘텐츠·변환/팔레트5:5다. 안정 장치ID/상자 초기좌표 참조·판정·목적지·쿨다운·장치 Sprite를 합친 경계를 해시에 넣는다. 누락/중복/잘못된 참조·미지원 판정은 전체 거부한다. 기존2:2/3:3/4:4와 기존 편집/팔레트/전송은 보존했다. 일반 MapEditor 장치 작성은 아직 미연결이다. [장치 구현·검증](Notes/2026-11-08-quantum-devices.md)을 따른다.

- 경계는 MapRenderBounds+여백10을 FP로 변환하며 제품 시작 때 초기 위치/경계를 재검증한다. 기존2:2 정적/로프·3:3 상호작용·4:4 복귀와 새5:5 장치의 원본/베이크/해시를 보존한다.

- Main은 정적/로프/상호작용/복귀/장치5종을 제품5로 실행한다. 상자는 원본 interactive.box 루트0.9·중력9.8·배율1/회전0만 지원한다.
- 로프는 수직 루트 박스 트리거이며 오프셋/스케일/180도 회전을 FP로 변환한다. 가로/경사/다중/합성/동적 판정 등 미지원 구성은 위치·원인과 함께 전체 거부한다. [로프 변환 근거](Notes/2026-11-06-quantum-wall-rope.md).
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

- Quantum 지원 맵의 변환/해시/잘못된 참조 거부·Main/재생/Photon 결과는 [공통 검증](QUANTUM_MIGRATION.md#검증)을 따른다. 일반 편집 UI/전송의 Quantum 전환 검증은 아니다.
- Mirror: 2026-10-26 Main→MapEditor→API 배치5개→KCP 테스트 플레이에서 탄환·상자 추락/재소환·완료/종료 후 편집 JSON 보존을 확인했다. [실행 범위](Notes/2026-10-26-stage-live-validation.md).
- 2026-10-10 EditMode 전체102개 통과 기록이 있으며 MapEditorCore의 저장/마이그레이션·ID·경로·초안 복원·데이터 검증을 포함한다. 세부 테스트별 범위와 과거 UI 실조작/렌더러 경계 결과는 [과거 상세 검증](Notes/2026-11-08-documentation-reference-history.md)에 보존한다.
- Undo/Redo 등 미구현 기능과 Quantum 편집/친구 전송 연결은 별도 남은 작업이다.

## 남은 작업

- Undo/Redo, 복사·붙여넣기, 다중 선택은 후순위

## 다음 작업

[최신 착수 순서](NEXT_SESSION.md#다음-작업)에 따라 기존 스테이지의 Tilemap/장치 판정을 작은 묶음부터 이식하고 해당 영역의 동작 문제와 회귀를 함께 확인한다.
