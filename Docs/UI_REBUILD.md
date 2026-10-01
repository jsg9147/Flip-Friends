# Main 메뉴 UI 재구축 현재 상태

- 상태: Active
- 최종 갱신: 2026-10-01
- 범위: `Main.unity` 화면 전환, 로비 진입 UI, 설정과 키 바인딩

## 현재 상태

- 기존 Steam, Mirror, 입력, 사운드와 게임플레이는 유지하고 메뉴/UI 계층만 점진적으로 교체한다.
- `Assets/01_Scripts/UI Scripts/Rebuild`에 공통 화면·내비게이션과 Main, ModeSelect, HostRoom, PublicLobby, PrivateJoin, Settings, KeyBinding 전용 화면이 있다.
- `Main.unity`의 `MainUIManager` 오브젝트에 새 내비게이터가 연결되어 있다.
- `Main.unity`의 화면 전환은 전부 `ScreenNavigator` 한 곳을 지난다. `UIScreen` 11개(`main-menu`, `mode-select`, `host-room`, `public-lobby`, `private-join`, `settings`, `settings-graphics-audio`, `settings-color`, `key-binding`, `key-binding-keyboard`, `key-binding-gamepad`)가 `ScreenNavigator.screens`에 명시적으로 등록되어 있고, 화면을 여닫는 버튼은 모두 `ScreenNavigationButton` 18개다.
- 설정 트리의 창 6개는 모두 같은 내비게이터의 화면이다. 설정 루트는 `SettingsScreen`, 키 바인딩 단계는 `KeyBindingScreen`, 하위 창 4개는 공통 `NavigableScreen`을 쓴다.
- `NavigableScreen`은 `InputManager.OnCancelEvent`를 받아 보이는 화면에서만 `ScreenNavigator.Back()`을 호출한다. Cancel 처리 구현이 화면마다 중복되지 않는다.
- 설정 값의 ± 버튼은 `ValueAdjuster.Increase`/`Decrease`와 `ResolutionAdjuster.NextResolution`/`PreviousResolution`에 연결되어, 마우스 클릭과 키보드·게임패드 입력이 같은 증감 경로를 쓴다. `Screen Mode`만 아직 연결 대상이 없다.
- `SettingManager`는 설정 값의 로드·저장·적용만 담당한다. 화면 전환용 `GameObject` 필드 6개와 `WindowReset`, `Open*Window` 6개, `SettingActive`, `CancelBtnEvent`는 제거됐다.
- `MainUIManager`는 `TryOpenScreen` 브리지로 동작한다. 내비게이터가 있으면 새 경로로 조기 반환하고, 없을 때만 legacy `UIReset()` + `SetActive` 경로를 쓴다. 설정 화면은 더 이상 이 브리지를 거치지 않는다.
- Host 설정은 기존 `SteamRoomManager.HostLobby`에 연결되고, 공개 로비는 loading·empty·error·joining 상태를 구분한다.
- 비공개 참가는 `TMP_InputField` 기반 코드 입력과 기존 참가 로직을 연결한다.
- 기존 UI를 한 번에 제거하지 않고 새 흐름이 확인된 화면부터 교체한다.

## 핵심 흐름

```text
ScreenNavigationButton 또는 NavigableScreen의 Cancel 처리
  → ScreenNavigator
  → UIScreen 파생 화면(MainMenu, ModeSelect, HostRoom, PublicLobby, PrivateJoin, Settings, KeyBinding, NavigableScreen)
  → 기존 SteamRoomManager 및 설정 서비스(SettingManager, KeyRebindingManager)
```

## 검증

- 스크립트와 씬 연결의 컴파일 검증은 완료됐다. Unity `6000.6.3f1` 번들 dotnet으로 `Assembly-CSharp` 빌드가 오류 0건으로 통과한다.
- 중복 legacy listener 감사는 완료했다. 씬에서 `MainUIManager`나 `SettingManager`를 직접 호출하는 `onClick`은 0개다. 마지막으로 남아 있던 `Setting UI/Frame/Close Button`의 `MainUIOpen`을 Back으로 교체했다. 코드에서는 `GameModeUIOpen`만 5곳에서 호출된다(`HostSetting`, `PublicLobbyUI`, `HostRoomScreen`, `PrivateJoinScreen`, `PublicLobbyScreen`).
- 이중 소유 버그의 런타임 재현을 확인했다. 수정 전에는 `settings`가 Hide 상태일 때 `SettingManager.OpenSettingWindow()`를 호출하면 `activeSelf=true`, `CanvasGroup.alpha=0`, `CurrentScreen=main-menu`로 어긋났다. 씬의 `KeySetting Button` 경로에서는 `key-binding`이 `activeSelf=true`, `alpha=0`, `blocksRaycasts=false`로 열려 화면이 보이지 않고 입력도 받지 못했으며, `Setting UI`는 `activeSelf=false`인데 `IsVisible=true`로 남았다.
- 수정 후 플레이 모드에서 설정 트리 전체를 실제 버튼 `onClick`과 Cancel 입력으로 왕복 검증했다. `main-menu` → `settings` → `key-binding` → `key-binding-keyboard` → (Close) → `key-binding-gamepad` → (Cancel) → `key-binding` → (Close) → `settings` → `settings-graphics-audio` → (Back) → `settings` → `settings-color` → (Cancel) → `settings` → (Cancel) → `main-menu` 13단계 전부에서 `CurrentScreen`과 `activeSelf`·`alpha`·`blocksRaycasts`·`IsVisible`이 일치했고, 나머지 화면은 모두 비활성·`alpha` 0이었다.
- 설정 값 ± 버튼 12개를 플레이 모드에서 실제 `onClick`으로 검증했다. BGM 51, SFX 49로 증감되고 표시 텍스트와 `PlayerPrefs`가 함께 바뀌며, 해상도는 `1600 x 900` → `1600 x 1024` → `1600 x 900`으로 왕복하고, 색은 `150/250/250` → `149/251/251`로 바뀐다. 검증으로 바뀐 값은 원래대로 복원했다.
- 기존 메인 흐름 회귀도 확인했다. `main-menu` ↔ `mode-select` 왕복과 `mode-select`에서 연 `settings`의 Close가 `mode-select`로 복귀한다. 설정의 Close는 고정 복귀가 아니라 히스토리 복귀다.
- 플레이 모드와 컴파일 모두 `ScreenNavigator`·`UIScreen` 관련 경고·오류가 없다. 남은 콘솔 경고는 vendor `.meta` 버전 경고 등 기존 항목뿐이다.
- Main, GameRoom, GamePlay, MapEditor 4개 씬 모두 오류 없이 로드된다.
- 마우스·키보드·게임패드 실제 입력 이동과 Steam이 필요한 Host·공개 로비·비공개 참가 왕복은 수동 검증이 남아 있다.

## 남은 작업

- 화면 모드(`Screen Mode`) 전환 기능 신규 구현. 삭제된 `SettingsMenu`의 `ChangeFullscreenMode`와 함께 기능 자체가 사라져 ± 버튼 2개가 아직 죽어 있고 키보드·게임패드 경로도 없다. `ResolutionAdjuster`와 같은 모양으로 표시 텍스트·`PlayerPrefs` 저장·`Screen.fullScreenMode` 적용을 담당하는 어댑터가 필요하다.
- 설정 저장 경로 복구. `SettingManager.ApplySettings`와 `ResetSettings`를 호출하는 곳이 씬·코드 어디에도 없어 해상도 적용과 저장이 실행되지 않고, `SaveSettings`는 `ScreenRed`/`ScreenGreen`/`ScreenBlue`로 쓰는데 `LoadSettings`는 `Red`/`Green`/`Blue`로 읽어 색 설정이 저장되지 않는다. 음량과 색의 즉시 저장은 `ValueAdjuster`가 따로 처리하고 있어 두 저장 경로의 역할도 함께 정리해야 한다.
- CP949로 저장된 기존 스크립트의 UTF-8 변환. `ValueAdjuster`와 `ResolutionAdjuster`는 이번에 변환했고, `InputManager`·`KeyRebindingManager` 등 다른 파일에는 아직 깨진 주석이 남아 있다.
- 기존 화면 4개(`ModeSelectScreen`, `HostRoomScreen`, `PublicLobbyScreen`, `PrivateJoinScreen`)의 Cancel 처리를 `NavigableScreen`으로 통합. 지금은 같은 구독·해제 코드를 각자 복제하고 있다.
- Host, 공개 로비, 비공개 참가 화면의 실패 상태 포함 Steam 런타임 검증
- `UI Scripts/Delete`에 남은 `ButtonNav`, `ButtonNavigation` 정리. 둘 다 아직 씬에서 참조되므로 새 흐름이 대체한 뒤에 제거한다.
- `MainUIManager`에 남은 legacy `SetActive` 대체 경로(`UIReset`과 화면별 `GameObject` 필드) 제거. 내비게이터가 항상 존재한다는 것이 확인된 뒤에 진행한다.

## 다음 작업

설정 저장 경로를 복구한다. `SaveSettings`와 `LoadSettings`의 색 키 불일치를 맞추고, `ApplySettings`를 호출할 지점(설정 화면을 벗어날 때 또는 Apply 버튼)을 정해 해상도 변경이 실제로 적용·저장되게 한다.

착수 순서와 각 항목에서 먼저 정할 결정은 `Docs/NEXT_SESSION.md`에 정리돼 있다.
