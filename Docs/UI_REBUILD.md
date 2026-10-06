# Main 메뉴 UI 재구축 현재 상태

- 상태: Active
- 최종 갱신: 2026-10-06
- 범위: `Main.unity` 화면 전환, 로비 진입 UI, 설정과 키 바인딩

## 현재 상태

- 기존 Steam, Mirror, 입력, 사운드와 게임플레이는 유지하고 메뉴/UI 계층만 점진적으로 교체한다.
- `Assets/01_Scripts/UI Scripts/Rebuild`에 공통 화면·내비게이션과 Main, ModeSelect, HostRoom, PublicLobby, PrivateJoin, Settings, KeyBinding 전용 화면이 있다.
- `Main.unity`의 `MainUIManager` 오브젝트에 새 내비게이터가 연결되어 있다.
- `Main.unity`의 화면 전환은 전부 `ScreenNavigator` 한 곳을 지난다. `UIScreen` 11개(`main-menu`, `mode-select`, `host-room`, `public-lobby`, `private-join`, `settings`, `settings-graphics-audio`, `settings-color`, `key-binding`, `key-binding-keyboard`, `key-binding-gamepad`)가 `ScreenNavigator.screens`에 명시적으로 등록되어 있고, 화면을 여닫는 버튼은 모두 `ScreenNavigationButton` 18개다.
- 설정 트리의 창 6개는 모두 같은 내비게이터의 화면이다. 설정 루트는 `SettingsScreen`, 키 바인딩 단계는 `KeyBindingScreen`, 하위 창 4개는 공통 `NavigableScreen`을 쓴다.
- `NavigableScreen`은 `InputManager.OnCancelEvent`를 받아 보이는 화면에서만 `OnCancel()`을 호출하고, 기본 구현은 `ScreenNavigator.Back()`이다. `ModeSelectScreen`·`HostRoomScreen`·`PublicLobbyScreen`·`PrivateJoinScreen`도 이 베이스를 쓴다. 뒤 세 화면은 `FallbackScreenId`를 `mode-select`로 두어 히스토리가 비어도 Cancel·Back이 `mode-select`로 간다(히스토리에 남기지 않음).
- 코드에서 `AddListener`로 연결하는 버튼(Host의 Room Type·Create·Cancel, 공개 로비의 Refresh·Back, 비공개 참가의 Join·Back)은 Inspector `onClick`을 비워 둔다. 둘 다 있으면 한 번 클릭에 두 번 실행된다.
- 설정 값의 ± 버튼은 `ValueAdjuster.Increase`/`Decrease`와 `ResolutionAdjuster.NextResolution`/`PreviousResolution`, `ScreenModeAdjuster.NextScreenMode`/`PreviousScreenMode`에 연결되어, 마우스 클릭과 키보드·게임패드 입력이 같은 증감 경로를 쓴다.
- 설정 값의 저장은 `ValueAdjuster`·`ResolutionAdjuster`·`ScreenModeAdjuster`가 각자 맡고, `SettingManager`는 저장된 값을 게임에 적용하는 책임만 갖는다. 화면 전환용 `GameObject` 필드 6개와 `WindowReset`, `Open*Window` 6개, `SettingActive`, `CancelBtnEvent`는 제거됐다.
- 음량과 색은 ± 조작마다 즉시 저장되고, 해상도와 화면 모드는 `SettingsScreen`이 보이거나 숨을 때 `SettingManager.ApplySettings`가 확정한다. 둘은 `Screen.SetResolution` 한 번으로 같이 적용한다. 같은 프레임에 `Screen.fullScreenMode`를 따로 바꾸면 한쪽 요청이 다른 쪽을 덮을 수 있기 때문이다. 조작 중에는 창 크기가 바뀌지 않고, 하위 창에서 바꾼 값은 설정 루트로 돌아오는 시점에 적용된다.
- 화면 모드는 `FullScreenWindow`(Fullscreen)와 `Windowed` 두 가지다. 삭제된 `SettingsMenu`와 같은 구성이다. 저장값이 없으면 현재 창의 모드를 따르고, `ScreenMode` 키에 enum 이름 문자열로 저장한다.
- Adjuster의 키보드·게임패드 좌우 입력은 `HorizontalStepInput`이 해석한다. 해상도·화면 모드는 누른 순간에만 한 칸 바뀌고(`PressOnly`), 음량·색은 누른 순간 한 칸, 계속 누르고 있으면 `repeatDelay`(0.4초) 뒤 `repeatInterval`(0.1초)마다 한 칸씩 바뀐다(`Repeating`, `ValueAdjuster` Inspector 필드). 시간은 `Time.unscaledTime` 기준이다.
- 수치 설정의 `PlayerPrefs` 저장 형식은 0~1 float으로 통일했다. 키는 `BGMVolume`, `SFXVolume`, `Red`, `Green`, `Blue`이고(`SavedResolution`·`ScreenMode`는 문자열), 값을 읽는 `SoundManager`·`PlayerSound`·`PlayerController2D`·`CustomRoomPlayer`와 형식이 같다. 이전 빌드가 같은 키에 정수로 저장한 값은 처음 불러올 때 변환해서 다시 쓴다.
- `SoundManager`는 `ApplyBGMVolume`과 `ApplySFXVolume`로 적용만 하고 저장은 하지 않는다. `ValueAdjuster.ValueChanged`를 `SettingManager`가 받아 호출하며, UI 클릭음도 효과음 설정을 따른다.
- `MainUIManager`는 화면 전환을 하지 않는다. Main 씬 진입 시 BGM 재생, 커서 잠금, 목표 프레임만 적용한다. `instance`, 화면별 `GameObject` 필드, `UIReset`, `*UIOpen`, `GameQuit`은 제거됐다.
- 레거시 `HostSetting`·`PublicLobbyUI`는 씬 컴포넌트와 스크립트 모두 제거됐다. `RoomType` enum은 `NetworkScripts/RoomType.cs`로 옮겼다.
- 레거시 `ButtonNavigation`·`ButtonNav`도 씬과 스크립트에서 제거됐고 `UI Scripts/Delete` 폴더는 없다. 방향키·게임패드 선택은 각 화면의 `ButtonSelectController` 하나가 맡고, Cancel 처리는 `NavigableScreen`만 구독한다.
- Host 설정은 기존 `SteamRoomManager.HostLobby`에 연결되고, 공개 로비는 loading·empty·error·joining 상태를 구분한다.
- 비공개 참가는 `TMP_InputField` 기반 코드 입력과 기존 참가 로직을 연결한다.
- 기존 UI를 한 번에 제거하지 않고 새 흐름이 확인된 화면부터 교체한다.

## 핵심 흐름

```text
ScreenNavigationButton 또는 NavigableScreen의 Cancel 처리
  → ScreenNavigator
  → UIScreen 파생 화면(MainMenu, ModeSelect, HostRoom, PublicLobby, PrivateJoin, Settings, KeyBinding, NavigableScreen)
  → 기존 SteamRoomManager 및 설정 서비스(SettingManager, KeyRebindingManager)

ValueAdjuster, ResolutionAdjuster, ScreenModeAdjuster(값 증감과 저장)
  → ValueAdjuster.ValueChanged
  → SettingManager(적용)
  → SoundManager 음량, 플레이어 색 미리보기

SettingsScreen.OnShow 또는 OnHide
  → SettingManager.ApplySettings
  → ResolutionAdjuster.ApplyResolution(ScreenModeAdjuster.SelectedMode)(해상도·화면 모드를 한 번에 적용)
  → ScreenModeAdjuster.SaveScreenMode
```

## 검증

- 스크립트와 씬 연결의 컴파일 검증은 완료됐다. Unity `6000.6.3f1` 번들 dotnet으로 `Assembly-CSharp` 빌드가 오류 0건으로 통과한다.
- 중복 legacy listener 감사는 완료했다. 씬과 코드 모두 `MainUIManager`의 화면 전환을 호출하는 곳이 없다.
- 버튼 중복 실행 버그를 플레이 모드에서 재현하고 고쳤다. 수정 전에는 Host·공개 로비·비공개 참가의 Back 클릭이 `HandleBack`을 두 번 실행해 `mode-select`를 건너뛰고 `main-menu`로 갔다. Create 버튼은 Inspector·`HostRoomScreen`·비활성 `HostSetting`의 `Awake`가 각각 등록해 `HostLobby`가 최대 3회, Join 버튼은 2회 호출되는 구조였다.
- 수정 후 세 화면 각각에서 Back 클릭 → `mode-select`, Cancel → `mode-select` → `main-menu`, 히스토리를 비운 뒤 Cancel → `mode-select`를 확인했고 모든 단계에서 `CurrentScreen`과 `activeSelf`·`alpha`·`blocksRaycasts`·`IsVisible`이 일치했다.
- 이중 소유 버그의 런타임 재현을 확인했다. 수정 전에는 `settings`가 Hide 상태일 때 `SettingManager.OpenSettingWindow()`를 호출하면 `activeSelf=true`, `CanvasGroup.alpha=0`, `CurrentScreen=main-menu`로 어긋났다. 씬의 `KeySetting Button` 경로에서는 `key-binding`이 `activeSelf=true`, `alpha=0`, `blocksRaycasts=false`로 열려 화면이 보이지 않고 입력도 받지 못했으며, `Setting UI`는 `activeSelf=false`인데 `IsVisible=true`로 남았다.
- 수정 후 플레이 모드에서 설정 트리 전체를 실제 버튼 `onClick`과 Cancel 입력으로 왕복 검증했다. `main-menu` → `settings` → `key-binding` → `key-binding-keyboard` → (Close) → `key-binding-gamepad` → (Cancel) → `key-binding` → (Close) → `settings` → `settings-graphics-audio` → (Back) → `settings` → `settings-color` → (Cancel) → `settings` → (Cancel) → `main-menu` 13단계 전부에서 `CurrentScreen`과 `activeSelf`·`alpha`·`blocksRaycasts`·`IsVisible`이 일치했고, 나머지 화면은 모두 비활성·`alpha` 0이었다.
- 설정 값 ± 버튼 12개를 플레이 모드에서 실제 `onClick`으로 검증했다. BGM 51, SFX 49로 증감되고 표시 텍스트와 `PlayerPrefs`가 함께 바뀌며, 해상도는 `1600 x 900` → `1600 x 1024` → `1600 x 900`으로 왕복하고, 색은 `150/250/250` → `149/251/251`로 바뀐다. 검증으로 바뀐 값은 원래대로 복원했다.
- 기존 메인 흐름 회귀도 확인했다. `main-menu` ↔ `mode-select` 왕복과 `mode-select`에서 연 `settings`의 Close가 `mode-select`로 복귀한다. 설정의 Close는 고정 복귀가 아니라 히스토리 복귀다.
- 설정 저장 경로를 플레이 모드에서 검증했다. 실제 `Right Button` `onClick`으로 BGM을 51로 올리면 `PlayerPrefs`가 0.510으로 바뀌고 `bgmSource.volume`도 0.510이 된다. 플레이 종료 후 재진입하면 설정 화면을 열지 않아도 `bgmSource.volume`과 `uiSource.volume`이 저장값 0.500으로 적용된다.
- 이전 정수 저장값의 변환도 확인했다. 정수로 저장돼 있던 BGM 50, SFX 50, 색 150/250/250이 각각 0.500, 0.500, 0.588/0.980/0.980 float으로 바뀌어 다시 저장되고, 색 미리보기 `Image.color`가 같은 값이 된다. `PlayerPrefs.GetFloat`은 `SetInt`로 쓰인 키에서 저장값이 아니라 기본값을 돌려주므로, 음수 표식으로 이전 형식을 구분한다.
- 해상도 확정 시점도 검증했다. 하위 창에서 `1600 x 900` → `1600 x 1024`로 바꾸는 동안 `SavedResolution`은 그대로고, Back으로 설정 루트에 돌아오는 순간 `1600x1024`로 저장된다. 플레이 재진입 시 그 값이 유지된다. 검증으로 바꾼 값은 모두 원래대로 복원했다.
- 플레이 모드와 컴파일 모두 `ScreenNavigator`·`UIScreen` 관련 경고·오류가 없다. 남은 콘솔 경고는 vendor `.meta` 버전 경고 등 기존 항목뿐이다.
- 화면 모드를 플레이 모드에서 검증했다. 실제 `Left`/`Right Button` `onClick`으로 `Windowed` ↔ `Fullscreen`이 표시만 바뀌고, Back으로 설정 루트에 돌아오는 순간 `ScreenMode`가 저장된다. `Screen Mode Border`를 선택하고 오른쪽을 7프레임 누르고 있어도 한 번만 바뀌고, 뗐다가 다시 누르면 다시 한 번 바뀐다. 에디터에서는 `Screen.fullScreenMode`가 실제로 바뀌지 않아 창 전환은 빌드에서 수동 확인이 남아 있다. 검증으로 만든 `ScreenMode` 키는 지웠다.
- 누름 유지 입력을 플레이 모드에서 검증했다(일시정지 후 `EditorApplication.Step()`, `InputManager.dir`을 리플렉션으로 고정). 해상도는 오른쪽을 5프레임 누르고 있어도 `1600 x 900` → `1600 x 1024` 한 칸만 바뀌고, 뗐다가 왼쪽을 누르면 다시 한 칸만 돌아온다. SFX는 반복 값을 0.06초/0.04초로 바꿔 확인했고, 누른 프레임에 50 → 51, 0.06초 뒤 52, 이후 0.04초마다 한 칸씩 올랐다. 스텝 모드에서는 한 프레임이 `unscaledTime` 0.02초로 진행된다. 검증으로 바뀐 값은 복원했다.
- 레거시 내비게이션 제거 후 플레이 모드에서 공개 로비의 Cancel 구독자가 3개에서 `PublicLobbyScreen` 1개로 줄었다. Cancel → `mode-select` → `main-menu`, 히스토리 없이 Cancel → `mode-select`, Back 클릭 → `mode-select`를 확인했고, 단계마다 `CurrentScreen`과 `activeSelf`·`alpha`·`blocksRaycasts`·`IsVisible`이 일치했다. 공개 로비 진입 시 Refresh가, `key-binding-gamepad` 진입 시 Close Btn이 선택된다.
- Main, GameRoom, GamePlay, MapEditor 4개 씬 모두 오류 없이 로드된다.
- 마우스·키보드·게임패드 실제 입력 이동과 Steam이 필요한 Host·공개 로비·비공개 참가 왕복은 수동 검증이 남아 있다.

## 남은 작업

- 빌드에서 화면 모드 전환과 해상도 동시 변경이 실제 창에 반영되는지 수동 확인.
- 설정 기본값 복귀 수단이 없다. 호출하는 곳이 없던 `SettingManager.ResetSettings`는 제거했다. 기본값 버튼이 필요해지면 `ValueAdjuster`에 기본값 적용 경로를 다시 두고 화면에 연결한다.
- CP949로 저장된 기존 스크립트의 UTF-8 변환. `ValueAdjuster`·`ResolutionAdjuster`·`SoundManager`는 변환했고 `Assets/01_Scripts`에 15개가 남아 있다.
- Host, 공개 로비, 비공개 참가 화면의 실패 상태 포함 Steam 런타임 검증
- 공개 로비 목록의 `LobbyItem` 참가 버튼은 어떤 선택 목록에도 들어가지 않아 게임패드로 고를 수 없다. 제거한 `ButtonNavigation`도 목록이 비어 있어 원래 없던 경로다. 필요하면 `PublicLobbyScreen`이 항목을 만들 때 `ButtonSelectController.tagetButtonList`에 넣는다.

## 다음 작업

Host, 공개 로비, 비공개 참가의 Steam 런타임 수동 검증.

착수 순서와 각 항목에서 먼저 정할 결정은 `Docs/NEXT_SESSION.md`에 정리돼 있다.
