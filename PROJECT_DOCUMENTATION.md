# Flip Friends 문서 진입점

## 목적

이 문서는 저장소 문서의 단일 라우터다. 프로젝트 전체 설명을 복제하지 않고, 작업에 필요한 최소 문서만 찾게 한다.

## 항상 적용하는 기준

1. 저장소 규칙은 Claude는 `CLAUDE.md`, Codex는 `AGENTS.md`를 기준으로 한다. 폴더별 규칙은 `.claude/rules/`에 있다.
2. 작업을 시작할 때 아래 표에서 해당 영역의 문서만 추가로 읽는다.
3. 코드·씬·Inspector가 문서와 다르면 실제 구현을 우선하고 같은 작업에서 문서를 수정한다.
4. `Active` 문서만 현재 구현 근거로 사용한다. 완료된 일회성 프롬프트와 대체된 계획서는 유지하지 않는다.
5. 작업 완료 시 해당 영역 문서의 `현재 상태`, `검증`, `다음 작업`만 갱신한다. 날짜별 상세 작업 일지는 Git 기록에 맡긴다.

## 작업별 문서 경로

| 작업 종류 | 필수 문서 | 선택적으로 확인할 항목 |
|---|---|---|
| 저장소 공통 규칙, 코딩 스타일, 빌드 | `CLAUDE.md` | 없음 |
| 네트워크, 플레이어, 씬 전환 | `.claude/rules/network.md` | 커스텀 맵 흐름은 `Docs/CUSTOM_MAP_NETWORK.md` |
| 장애물, 스위치, 충돌, 레이어·태그 | `.claude/rules/gameplay.md` | 없음 |
| UI, 화면 전환, 설정 | `.claude/rules/ui.md`와 `Docs/UI_REBUILD.md` | 없음 |
| 맵 에디터, 저장소, 팔레트, 검증 | `.claude/rules/map-editor.md`와 `Docs/MAP_EDITOR.md` | 커스텀 맵 전송도 바꾸면 `Docs/CUSTOM_MAP_NETWORK.md` |
| 로비 맵 선택, 자동 공유, MapId, 세션 캐시, 런타임 맵 생성 | `Docs/CUSTOM_MAP_NETWORK.md` | 에디터 데이터 형식도 바꾸면 `Docs/MAP_EDITOR.md` |
| Main 메뉴, 로비 목록 | `Docs/UI_REBUILD.md` | Steam/Mirror 흐름을 바꾸면 `Docs/CUSTOM_MAP_NETWORK.md` |
| 다음에 할 작업 고르기, 세션 인계 | `Docs/NEXT_SESSION.md` | 고른 항목이 속한 기능 문서 |
| 테스트 추가 또는 실패 조사 | `.claude/rules/tests.md`와 대상 기능 문서의 `검증` 절 | `Assets/Tests`와 Unity Console |

두 영역 이상에 걸친 작업은 해당 문서를 모두 읽되, 관련 없는 문서는 읽지 않는다.

## 활성 문서

| 문서 | 상태 | 책임 |
|---|---|---|
| `CLAUDE.md` | Active | Claude가 매 세션 읽는 저장소 공통 규칙 |
| `.claude/rules/*.md` | Active | 해당 폴더의 파일을 열 때만 읽히는 폴더별 규칙 |
| `AGENTS.md` | Active | Codex 전용 진입 문서. 내용은 `CLAUDE.md`와 `.claude/rules/`를 가리킨다 |
| `Docs/MAP_EDITOR.md` | Active | 맵 제작·저장·검증의 현재 상태 |
| `Docs/CUSTOM_MAP_NETWORK.md` | Active | 커스텀 맵 로비 선택·자동 공유·런타임 생성 |
| `Docs/UI_REBUILD.md` | Active | Main 메뉴/UI 교체 진행 상태 |
| `Docs/NEXT_SESSION.md` | Active | 남은 작업의 착수 순서와 먼저 정할 결정. 큐가 비면 삭제한다 |
| `Docs/Notes/*.md` | Active | 1회성 조사·실패한 시도·측정값. 자동으로 읽히지 않는다 |

## 문서 갱신 형식

기능 문서는 다음 구조를 유지한다.

- `상태`: Active, Draft, Superseded, Historical 중 하나
- `현재 상태`: 지금 코드가 실제로 보장하는 것
- `핵심 흐름`: 파일과 책임의 짧은 연결
- `검증`: 자동 검증, 수동 검증, 환경 제약
- `남은 작업`: 아직 구현되지 않은 항목만 기록
- `다음 작업`: 다음 세션에서 가장 먼저 할 한 가지

새 문서는 기존 활성 문서로 표현할 수 없는 독립 기능 영역에만 만든다. 새 활성 문서를 추가하면 이 파일의 라우팅 표와 활성 문서 표를 동시에 갱신한다.

## 폐기 정책

- 완료된 실행 프롬프트는 결과가 기능 문서에 반영된 뒤 삭제한다.
- 새 문서로 완전히 대체된 계획서와 로드맵은 삭제한다.
- 과거 결정이 현재 코드 이해에 반드시 필요하면 기능 문서에 한두 문장으로 남긴다.
- vendor 및 패키지 README·LICENSE는 이 체계의 관리 대상이 아니며 삭제하지 않는다.

## 기록 분류

새로 기록할 것이 생기면 아래 순서로 위치를 정한다. 지침 파일이 길어지면 규칙이 묻혀 지켜지지 않는다.

1. 모든 폴더에 해당하는 규칙인가 → `CLAUDE.md` (120줄 상한)
2. 특정 폴더에서만 쓰는 규칙인가 → `.claude/rules/<주제>.md`에 `paths` 프런트매터와 함께 (각 60줄 상한)
3. 지금 구현이 보장하는 것인가 → 해당 `Docs/<기능>.md`의 `현재 상태`·`검증` (100줄 상한)
4. 다음에 할 일인가 → `Docs/NEXT_SESSION.md` 또는 기능 문서의 `다음 작업`
5. 위 어디에도 안 맞는 1회성 조사·측정값인가 → `Docs/Notes/<날짜>-<주제>.md`
6. 단지 무엇을 바꿨는지인가 → Git 커밋 메시지. 문서에 적지 않는다
