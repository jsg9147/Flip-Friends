# 저장소 가이드라인 (Codex)

**Flip Friends**는 Unity 6(`6000.6.3f1`)과 Mirror로 만든 2D 멀티플레이 협동 플랫포머다. 최대 4명이 레벨을 오르고 퍼즐을 풀며, 오브젝트나 다른 플레이어를 들어 옮겨 도착 지점에 도달한다. 로비는 Steam(FizzySteamworks)으로 관리한다.

## 먼저 읽을 것

저장소 공통 규칙, 빌드·테스트 명령, 코딩 스타일, 환경 제약, 함정은 모두 **[`CLAUDE.md`](CLAUDE.md)** 에 있다. 작업을 시작하기 전에 그 파일을 읽는다. 이 파일은 규칙을 복제하지 않는다. 두 파일이 어긋나면 `CLAUDE.md`를 따른다.

작업 종류에 맞는 문서 경로는 [`PROJECT_DOCUMENTATION.md`](PROJECT_DOCUMENTATION.md)에서 고른다.

## 고치는 폴더에 따라 추가로 읽을 것

| 고치는 대상 | 읽을 파일 |
|---|---|
| `Assets/01_Scripts/NetworkScripts`, `PlayerScripts` | `.claude/rules/network.md` |
| `Assets/01_Scripts/ObstacleScripts`, `PlayingScripts`, `GameObjScripts` | `.claude/rules/gameplay.md` |
| `Assets/01_Scripts/UI Scripts`, `GameOption`, `Setting` | `.claude/rules/ui.md`, `Docs/UI_REBUILD.md` |
| `Assets/01_Scripts/MapEditorScripts` | `.claude/rules/map-editor.md`, `Docs/MAP_EDITOR.md` |
| 커스텀 맵 로비 선택·전송·런타임 생성 | `Docs/CUSTOM_MAP_NETWORK.md` |
| `Assets/Tests` | `.claude/rules/tests.md` |

`.claude/` 폴더는 Claude Code 설정 폴더지만, 그 안의 규칙 파일은 도구와 무관한 일반 마크다운이다. Codex도 그대로 읽으면 된다.

## 기록 위치

규칙·상태·조사 결과를 어디에 쓸지는 `PROJECT_DOCUMENTATION.md`의 `기록 분류`를 따른다. 진행 상태와 작업 일지는 이 파일과 `CLAUDE.md`에 쓰지 않는다. 작업이 끝나면 해당 `Docs/*.md`의 `현재 상태`, `검증`, `다음 작업`만 갱신한다.
