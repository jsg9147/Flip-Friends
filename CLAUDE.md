# Flip Friends — Claude Code 지침

Unity 6(`6000.6.3f1`)과 Mirror로 만든 2D 멀티플레이 협동 플랫포머다. 최대 4명이 레벨을 오르고 퍼즐을 풀며, 오브젝트나 다른 플레이어를 들어 옮겨 도착 지점에 도달한다. 로비는 Steam(FizzySteamworks)으로 관리한다.

식별자는 영어, 주석·문서·커밋 메시지는 한국어로 쓴다. 사용자에게 하는 답변은 한글로 한다. 모든 소스 파일은 **UTF-8**로 저장한다.

## 기록을 어디에 쓰는가

이 파일은 매 세션 전체가 로드된다. 그래서 **모든 작업에 해당하는 것만** 둔다. 나머지는 아래 분류에 따라 다른 파일에 쓴다.

| 기록 종류 | 위치 | 읽는 시점 |
|---|---|---|
| 모든 작업에 적용되는 규칙 | `CLAUDE.md` (이 파일) | 매 세션 자동 |
| 특정 폴더에서만 적용되는 규칙 | `.claude/rules/<주제>.md` | 그 폴더의 파일을 열 때 자동 |
| 기능의 현재 상태·검증·다음 작업 | `Docs/<기능>.md` | 라우터가 지시할 때 |
| 다음 세션 착수 순서 | `Docs/NEXT_SESSION.md` | 무엇부터 할지 정할 때 |
| 1회성 조사, 실패한 시도, 측정값 | `Docs/Notes/<날짜>-<주제>.md` | 참조가 필요할 때만 |
| 어떤 문서를 읽을지 | `PROJECT_DOCUMENTATION.md` | 작업 시작할 때 |
| 변경 이력 | Git 커밋 메시지 | — |

규칙을 추가할 때는 "이게 모든 폴더에 해당하나"를 먼저 묻는다. 아니면 이 파일이 아니라 `.claude/rules/`에 넣는다. 진행 상태와 작업 일지는 이 파일에 쓰지 않는다.

코드·씬·Inspector가 문서와 다르면 실제 구현을 따르고, 같은 작업에서 문서를 고친다. `AGENTS.md`는 Codex 전용이다. Claude는 이 파일과 `.claude/rules/`를 기준으로 한다.

## 명령

에디터 실행:

```powershell
Start-Process "C:\Program Files\Unity\Hub\Editor\6000.6.3f1\Editor\Unity.exe" -ArgumentList '-projectPath','C:\Unity Project\Flip-Friends'
```

컴파일 확인. 에디터가 프로젝트를 열고 있어도 동작한다. 코드를 고쳤으면 이걸로 검증한다:

```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.6.3f1\Editor\Data\DotNetSdk\dotnet.exe" build Assembly-CSharp.csproj
```

Edit Mode 테스트는 Unity Test Runner로 돌린다. CLI로 돌릴 때는 에디터를 완전히 닫아야 한다. 열려 있으면 `Library` 잠금 때문에 실패한다:

```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.6.3f1\Editor\Unity.exe" -runTests -batchmode -projectPath "C:\Unity Project\Flip-Friends" -testPlatform EditMode -testResults "$env:TEMP\editmode-results.xml"
```

## 환경 제약

- 로비·네트워크 흐름은 Steam 클라이언트에 로그인되어 있어야 동작한다. `steam_appid.txt`가 프로젝트 루트에 있어야 한다.
- 2인 이상이 필요한 검증은 이 환경에서 끝까지 못 간다. 수동 검증으로 남기고 기능 문서의 `검증`에 환경 제약으로 적는다.
- 대상은 Windows Standalone(1600x900), 제품명 `Slime Climb`, 회사 `MNSGStudio`다.
- `Library/`, `Temp/`, `Logs/`, `UserSettings/`는 커밋하지 않는다.

## 구조

게임 코드는 `Assets/01_Scripts` 아래 기능별 폴더에 둔다: `NetworkScripts`, `PlayerScripts`, `ObstacleScripts`, `PlayingScripts`, `GameObjScripts`, `MapEditorScripts`, `UI Scripts`, `GameOption`, `Setting`. 프리팹 `Assets/03_Prefabs`, 스프라이트 `Assets/02_Sprites`, 씬 `Assets/00_Scenes`.

기능 스크립트는 기존 폴더에 둔다. 넓은 `Misc` 폴더를 새로 만들지 않는다.

`Assets/Mirror`, `Assets/Plugins/Demigiant`, `Assets/com.rlabrecque.steamworks.net`은 벤더 코드다. 작업이 그 수정을 명시할 때만 고친다.

## 코딩 스타일

- 들여쓰기는 스페이스 4칸. 파일 하나에 MonoBehaviour 하나, 파일명은 클래스명과 맞춘다.
- Inspector에 노출할 필드는 public 대신 `[SerializeField]`로 쓴다.
- 클래스·메서드·public 프로퍼티는 PascalCase, private 필드와 지역 변수는 camelCase, bool은 `is`/`has`/`can` 접두사를 붙인다.
- 주석은 한글로, 왜 그렇게 했는지(WHY)만 남긴다. 자명한 코드에는 달지 않는다.
- 클래스 하나는 역할 하나만 맡는다. 매직 넘버는 상수나 Inspector 필드로 뺀다. 같은 로직이 두 곳 이상이면 공통 메서드로 뺀다.
- 빈 `catch`로 예외를 삼키지 않는다. `try-catch`를 쓰면 `Debug.LogError` 또는 `Debug.LogException`으로 메시지·스택·오브젝트 이름을 남긴다. 예상된 실패는 `Debug.LogWarning`이다.

## 함정

- `.meta`의 GUID는 Unity가 만들게 두고 직접 작성하지 않는다.
- 씬과 프리팹의 YAML은 에디터에서 연결한다. 대량 수동 편집은 피한다.
- `GameManager`는 `DontDestroyOnLoad`를 쓰지 않는다. 씬 범위이며 씬마다 `Instance`가 초기화된다. `GameManager`, `StageManager`, `SoundManager`, `InputManager`가 싱글톤이다.

## 커밋과 PR

커밋 메시지는 짧고 명령형으로, 한 변경에만 집중한다. 한국어로 쓴다. PR에는 게임플레이 영향 요약, 연결된 작업, 테스트 내용을 적는다. UI나 씬 변경은 스크린샷을 첨부하고, 벤더 패키지나 네트워크 흐름을 고쳤으면 명시한다.

## 분량 상한

이 파일 120줄, `.claude/rules/` 각 파일 60줄, 기능 문서 100줄. 넘으면 분리하거나 지운다. 코드를 읽으면 알 수 있는 것(전체 디렉터리 트리, 패키지 목록, 파일별 설명), 표준 C#·Unity 관례, 자주 바뀌는 진행 상태는 넣지 않는다.
