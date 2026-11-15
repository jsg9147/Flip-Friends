---
paths:
  - "Assets/QuantumUser/Simulation/**/*.cs"
  - "Assets/01_Scripts/MapEditorScripts/Quantum/**/*.cs"
  - "Assets/01_Scripts/PlayingScripts/**/*.cs"
  - "Assets/01_Scripts/GameObjScripts/**/*.cs"
  - "Assets/03_Prefabs/OfficialStages/**"
---

# 게임플레이 규칙

2026-11-15 Mirror 제거로 플레이어·장애물·스위치의 Unity 런타임 스크립트(`Controller2D`, `BasicTrap`, `MovingTrap`, `Switch` 등)는 없다. 게임 규칙은 Quantum 시뮬레이션(`.claude/rules/network.md`)이 맡는다. 수치와 원본 동작의 근거는 `Docs/QUANTUM_MIGRATION.md`와 그 Notes다.

## 기능 추가 패턴

- **새 장치·장애물**: 시뮬레이션 시스템(`Assets/QuantumUser/Simulation/FlipFriends*System.cs`)과 프레임 상태를 먼저 정하고, 공식 스테이지용 작성 컴포넌트(`MapEditorScripts/Quantum/Official/QuantumStage*.cs`)를 추가한다. 작성 컴포넌트는 값만 가지며 `Update`·물리 동작을 넣지 않는다.
- 작성 컴포넌트를 추가하면 `QuantumOfficialStageWriter.AllowedComponents`와 `QuantumOfficialStageConverter`의 변환·해시 입력을 함께 고친다. 허용 목록 밖의 컴포넌트가 있는 프리팹은 작성기가 위치와 함께 거부한다.
- 크기·방향은 컴포넌트 값으로 정한다. 작성기는 회전0·배율1만 받는다(Tilemap 판정 제외). 경사는 45도 이하만 지원한다.
- MapEditor 팔레트 프리팹(`03_Prefabs/MapEditor/Quantum`)에는 Transform·SpriteRenderer·BoxCollider2D만 둔다(시작점은 콜라이더 없음). `QuantumMapConverter`가 그 밖의 컴포넌트를 거부한다.

## 원본 프리팹

`03_Prefabs/Stage` 21개와 `Obstacle`·`Switch.prefab`은 장치 스크립트를 뗀 지형·스프라이트 참고용이다. 공식 맵으로 옮길 때는 `Docs/Notes/2026-11-15-mirror-device-values.md`의 원본 수치를 보고 작성 컴포넌트로 다시 만든다. 원본 프리팹에 런타임 스크립트를 다시 붙이지 않는다.

## 추락

추락 경계는 Sprite·Tilemap 렌더러 범위에 여백(`FallBoundary.Margin`)을 더해 정하며 위쪽은 검사하지 않는다. 공식 스테이지는 프리팹 렌더러로, 저장 맵은 맵 에디터 미리보기와 같은 `MapRenderBounds`로 잰다. 한쪽 규칙을 바꾸면 다른 쪽도 맞춘다. 판정 영역에 들어가야 하는 지형이나 장치는 렌더러가 있어야 한다.

## 레이어와 태그

- 태그: `"Ground"`는 지형 Tilemap, `"Reset"`은 구간 복귀 트리거. 원본 프리팹에는 `"Trap"`·`"Enemy"`·`"Rope"`·`"Finish"`·`"Bounce"`·`"Spring"`이 남아 있다.
- 레이어: `"Ground"`, `"Reset"`, `"Player"`, `"Pickable"`

새 태그나 레이어를 만들기 전에 위 목록으로 해결되는지 본다. 추가하면 이 파일에 적는다.
