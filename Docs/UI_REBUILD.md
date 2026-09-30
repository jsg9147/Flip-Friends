# Main 메뉴 UI 재구축 현재 상태

- 상태: Active
- 최종 갱신: 2026-09-30
- 범위: `Main.unity` 화면 전환, 로비 진입 UI, 설정과 키 바인딩

## 현재 상태

- 기존 Steam, Mirror, 입력, 사운드와 게임플레이는 유지하고 메뉴/UI 계층만 점진적으로 교체한다.
- `Assets/01_Scripts/UI Scripts/Rebuild`에 공통 화면·내비게이션과 Main, ModeSelect, HostRoom, PublicLobby, PrivateJoin 전용 화면이 있다.
- `Main.unity`의 `MainUIManager` 오브젝트에 새 내비게이터가 연결되어 있다.
- Main UI와 GameMode UI는 새 화면 흐름에 연결됐다.
- `Main.unity`에는 `UIScreen` 7개(`main-menu`, `mode-select`, `host-room`, `public-lobby`, `private-join`, `settings`, `key-binding`)와 `ScreenNavigationButton` 7개가 있다.
- `main-menu`와 `mode-select` 등 로비 진입 화면은 전용 파생 클래스를 쓰지만, `settings`와 `key-binding`은 아직 베이스 `UIScreen`만 붙어 있다.
- `MainUIManager`는 `TryOpenScreen` 브리지로 동작한다. 내비게이터가 있으면 새 경로로 조기 반환하고, 없을 때만 legacy `UIReset()` + `SetActive` 경로를 쓴다.
- Host 설정은 기존 `SteamRoomManager.HostLobby`에 연결되고, 공개 로비는 loading·empty·error·joining 상태를 구분한다.
- 비공개 참가는 `TMP_InputField` 기반 코드 입력과 기존 참가 로직을 연결한다.
- Setting과 Key Rebinding은 아직 새 흐름으로 옮기지 않았다. `MainUIManager`의 설정 계열 메서드는 `TryOpenScreen`을 거치지 않는다.
- 기존 UI를 한 번에 제거하지 않고 새 흐름이 확인된 화면부터 교체한다.

## 핵심 흐름

```text
ScreenNavigationButton
  → ScreenNavigator
  → UIScreen 파생 화면
  → 기존 SteamRoomManager 및 설정 서비스
```

## 검증

- 스크립트와 씬 연결의 컴파일 검증은 완료됐다. Unity `6000.6.3f1`에서 `Assembly-CSharp` 빌드가 오류 없이 통과한다.
- 중복 legacy listener 감사는 완료했다. `Main.unity`에서 `MainUIManager`를 직접 호출하는 `onClick`은 `MainUIOpen` 1개뿐이고, 그 버튼에는 `ScreenNavigationButton`이 없어 중복 호출이 아니다.
- 전체 마우스·키보드·게임패드 이동과 런타임 왕복은 수동 검증이 남아 있다.

## 남은 작업

- Setting과 Key Rebinding 화면 구조 재구축. 지금은 경로가 이원화되어 있어 아래 두 문제가 함께 걸려 있다.
  - 씬의 Setting 버튼은 `ScreenNavigationButton(targetScreenId: settings)`로 내비게이터를 타지만, `MainUIManager.SettingUIOpen`·`KeyboardSettingUIOpen`·`GamepadSettingUIOpen`은 legacy `UIReset()` + `SetActive`만 호출한다. `UIScreen.HideImmediate`가 `CanvasGroup.alpha`를 0으로 남기므로 legacy 경로로 열면 화면이 보이지 않거나 입력을 받지 못하고, 내비게이터의 `CurrentScreen`도 갱신되지 않아 상태가 어긋난다. 런타임 확인 필요.
  - `MainUIManager.KeySettingUIOpen`이 `keySettingUI.SetActive(false)`를 호출한다. 여는 메서드가 끄고 있다.
- Host, 공개 로비, 비공개 참가 화면의 전체 런타임 왕복 및 실패 상태 수동 검증
- 새 흐름 확인 후에만 구형 메뉴 스크립트 제거

## 다음 작업

Setting과 Key Rebinding을 `ScreenNavigator` 경로로 옮긴다. 전용 `UIScreen` 파생 클래스를 만들고 `MainUIManager`의 설정 계열 메서드도 `TryOpenScreen`을 쓰게 바꿔 legacy `SetActive` 경로를 없앤다.
