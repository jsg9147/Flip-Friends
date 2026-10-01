---
paths:
  - "Assets/01_Scripts/UI Scripts/**/*.cs"
  - "Assets/01_Scripts/GameOption/**/*.cs"
  - "Assets/01_Scripts/Setting/**/*.cs"
---

# UI 규칙

- 화면 전환은 `ScreenNavigator`로 통합한다. 개별 스크립트가 패널을 직접 켜고 끄지 않는다.
- UI 입력은 싱글톤 `InputManager`의 `OnSubmitEvent`, `OnMenuEvent`를 구독한다. UI에서 New Input System 액션을 따로 만들지 않는다.
- 설정 값의 소유자는 하나다. `SettingManager`와 화면 전환 책임이 겹치면 `Docs/UI_REBUILD.md`를 먼저 읽는다.
- 설정 값은 Adjuster가 `PlayerPrefs`에 0~1 float으로 저장하고, 그 값을 게임에 적용하는 것은 `SettingManager`가 맡는다. 저장과 적용을 한 클래스에 합치지 않는다.
- 패널 참조는 `[SerializeField]`로 Inspector에서 연결한다. `GameObject.Find`나 경로 문자열로 찾지 않는다.
- 작업 결과는 `Docs/UI_REBUILD.md`의 `현재 상태`, `검증`, `다음 작업`에 반영한다.
