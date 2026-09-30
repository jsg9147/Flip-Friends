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
- Setting과 Key Rebinding 화면 자체는 내비게이터 경로로 정상 동작한다. 다만 전용 파생 클래스가 없고, `MainUIManager`에 이 화면들을 열던 legacy 메서드가 호출자 없이 남아 있다.
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
- 플레이 모드 내비게이션 왕복을 확인했다. 시작 시 `CurrentScreen`이 `main-menu`이고 나머지 6개 화면은 비활성·`alpha` 0이다. `mode-select` → `settings` → `key-binding` 순서로 열면 각각 활성·`alpha` 1이 되고, `Back()` 3회로 `main-menu`까지 역순 복귀한다.
- Main, GameRoom, GamePlay, MapEditor 4개 씬 모두 오류 없이 로드된다.
- 마우스·키보드·게임패드 실제 입력 이동과 Steam이 필요한 Host·공개 로비·비공개 참가 왕복은 수동 검증이 남아 있다.

## 남은 작업

- Setting과 Key Rebinding 화면 구조 재구축. `settings`와 `key-binding`은 내비게이터로 정상 동작하지만 아직 베이스 `UIScreen`만 붙어 있어 화면별 로직을 담을 파생 클래스가 없다.
- `MainUIManager`의 미사용 legacy 메서드 제거. `SettingUIOpen`, `KeySettingUIOpen`, `KeyboardSettingUIOpen`, `GamepadSettingUIOpen`은 씬·프리팹·코드 어디에서도 호출되지 않는 죽은 코드다. 이 중 `KeySettingUIOpen`은 `keySettingUI.SetActive(false)`를 호출해 여는 메서드가 끄고 있으나, 호출자가 없어 실제 동작에는 영향이 없다. 제거 시 `UIReset()`과 설정 계열 `GameObject` 필드도 같이 정리한다.
- Host, 공개 로비, 비공개 참가 화면의 실패 상태 포함 Steam 런타임 검증
- 새 흐름 확인 후에만 구형 메뉴 스크립트 제거

## 다음 작업

`MainUIManager`에서 호출자가 없는 설정 계열 legacy 메서드와 그에만 쓰이는 `GameObject` 필드를 제거한다. 실제 호출되는 `MainUIOpen`(`SettingManager.cs`)과 `GameModeUIOpen`(Host·PublicLobby·PrivateJoin 화면)은 `TryOpenScreen` 브리지를 그대로 유지한다.
