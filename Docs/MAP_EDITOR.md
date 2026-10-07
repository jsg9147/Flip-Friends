# 맵 에디터 현재 상태

- 상태: Active
- 최종 갱신: 2026-10-07
- 범위: 맵 데이터, 편집, 저장소, 팔레트, 플레이 가능성 검증

## 현재 상태

- `MapData` 스키마는 2.1이며 정규 GUID `N` 형식의 `mapId`를 사용한다.
- 2.0 로컬 데이터는 최초 로드 시 2.1로 안전하게 마이그레이션한다. 잘못된 2.1 ID는 자동 교체하지 않고 거부한다.
- 저장 API는 생성, 수정, 가져오기를 구분하며 같은 MapId나 파일명의 무단 덮어쓰기를 차단한다.
- 맵 목록은 MapId, 파일명, 표시 이름을 분리하고 중복 ID 항목의 선택을 막는다.
- 팔레트 문자열 ID와 검증 규칙을 사용한다. 플레이어 시작점과 도착점 등 필수 조건을 검사한다.
- 배치, 드래그 연속 배치·삭제, 회전, 반전, 저장·불러오기 UI가 구현되어 있다.
- `Data`·`Validation` 폴더는 `FlipFriends.MapEditorCore` 어셈블리다(`Validation`은 asmref). Mirror에 의존하지 않으며, 검증기는 `MapEditorPalette` 대신 `IMapPaletteLookup`을 받는다. Mirror 패킷 한계를 쓰는 `SavedMapCatalog`는 Assembly-CSharp에 남는다.
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

- C# 및 Unity 컴파일을 통과한 상태다. Unity `6000.6.3f1`에서도 `Assembly-CSharp` 빌드가 오류 없이 통과한다.
- 실제 1600x900 편집 UI, Main → MapEditor → Main 빌드 이동, 전체 편집 시나리오는 수동 검증이 남아 있다.
- `FlipFriends.MapEditorCore.Tests` Edit Mode 테스트 58개가 통과한다(2026-10-07, 전체 67개).
  - `MapDataRepositoryTests`: 임시 폴더(`SaveDirectoryOverride`)에서 저장·불러오기 왕복, MapId·파일명 중복 거부, 이름 변경 이동, 경로 이탈·예약어 거부, 빈·손상 JSON, 2.0 → 2.1 변환 후 재기록, 대문자 MapId 정규화, 미지원 버전, 잘못된 2.1 ID 거부, MapId 조회와 중복 거부를 보장한다.
  - `MapDataValidatorTests`: 시작점·도착점 최소 개수, 등록되지 않은·빈 팔레트 ID, 팔레트 없음, 비정규 MapId, 경계 밖·NaN 좌표, 최대 개수, 겹침 경고·치명 오류·허용 오차를 보장한다.

## 남은 작업

- 저장하지 않은 편집 상태로 테스트 플레이 후 에디터 복귀
- Undo/Redo, 복사·붙여넣기, 다중 선택은 후순위

## 다음 작업

저장하지 않은 편집 상태로 테스트 플레이한 뒤 에디터로 돌아올 때 편집 내용이 보존되게 한다.
