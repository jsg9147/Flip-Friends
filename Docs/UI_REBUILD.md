# Main 메뉴 UI 재구축 현재 상태

- 상태: Active
- 문서 정리: 2026-11-08 (실행 검증 날짜는 검증 절 참조)
- 범위: `Main.unity` 화면 전환, 로비 진입 UI, 설정과 키 바인딩

## 현재 상태

- Main 준비 화면에 다섯 번째 체크포인트/구간/소환 맵을 런타임 등록했다. 다섯 버튼의 배치·선택 목록과 지원 안내를 갱신하고 Main 미저장 씬은 보존했다. 플레이 화면의 체크포인트/구간 표지와 소환 버튼/남은 초는 프레임 상태를 표시한다. [장치 구현·검증](Notes/2026-11-08-quantum-devices.md)을 따른다.
- Quantum 고정Reset은1P R·2P Backspace다. 기존 UI 키 재설정/게임패드 저장은 Quantum 게임 입력에 아직 미연결이다.
- `InputManager`가 공통 `UIInputModeController`를 생성한다. 마우스 이동(3px 누적)·클릭·휠은 포인터 모드, 기존 Move/Navigate·Submit·Cancel·Menu 액션은 탐색 모드로 전환한다(방향 입력 임계값 0.5). 포인터 모드는 버튼 포커스를 해제하고 텍스트 입력 포커스는 유지하며, 탐색 모드는 커서와 uGUI 포인터 판정을 쉬고 마지막 유효 선택을 복원한다. 화면 기본 선택과 `ButtonSelectController` 자동 복원도 모드를 따른다. 확정 맵·열린 탭의 표시는 변경하지 않는다.
- 키 설정은 높이 100의 2열 목록이며, 본문·입력값은 기호를 포함한 CC0 Kenney Mini 도트 글꼴과 짙은 청록색을 쓴다. 옅은 배경 위 입력 버튼은 기존 StageControlFrame의 하늘색 프레임이며 호버·선택 시 연두색으로 바뀐다. 스크롤바는 각진 청록 손잡이·옅은 트랙을 쓰고 제목 이미지는 유지한다. 한 줄 표시·자동 크기 조절을 유지하며 Pick Up / Throw, Respawn, Previous/Next Camera, Run, Confirm, Back을 표시한다. 저장값은 바인딩 ID로 행을 찾고, 그룹이 비어 있는 게임패드 Respawn/Menu도 장치 경로로 분류한다.
- 타이틀 메뉴는 Start → Settings → Map Editor → Exit 순서로 화면 배치와 `ButtonSelectController` 선택 목록을 맞춘다. 런타임 생성되는 Map Editor도 Exit 바로 앞에 배치한다.
- GameRoom 스테이지 선택은 `StageMapTabs`와 명시적으로 등록한 `ScreenNavigator`의 기본/커스텀 탭을 쓴다. 기존 STAGE 이미지·픽셀 숫자·청록 프레임을 유지하고, 7×3 그리드와 커스텀 스크롤 목록이 같은 콘텐츠 영역을 공유한다. 선택 정보·상태·시작 버튼은 고정 하단에 둔다.
- 커스텀 탭은 `StageCustomMapItem` 전용 프리팹과 두께가 고정되는 `StageControlFrame`을 쓴다. `MapListUI.isTabbedLayout`에서는 임시 조작 버튼을 생성하지 않고, 맵 선택 후에도 탭을 유지한다. 빈 목록 안내와 `ScrollViewController`의 선택 추적을 연결했다.
- 열린 탭은 연두색 배경과 밑줄로 계속 표시하고, 버튼의 입력 포커스·마우스 호버는 별도 주황색 바깥 테두리로 표시한다. 탭 배경은 Button의 색상 전환 대상에서 분리했다.
- 기본/커스텀 맵의 `MapChoiceFeedback`은 동기화된 맵 종류·ID와 `SelectionChanged`를 따라 확정 맵에 연두색 배경·선택됨 배지를 유지한다. 입력 포커스·호버는 독립 주황색 테두리이며, 커스텀 목록 재생성 시에도 맵 ID로 표시를 복원한다.
- Main Start는 `quantum-prepare`로 전환해 지원5종 맵 선택 → 오프라인 2인 → QuantumPlay → 완료/메뉴 종료 → Main·재진입을 실행한다. 준비 화면은 `ScreenNavigator`·기존 취소 입력을 사용하며 Mirror 방 플레이어/서버 상태가 필요 없다. 미지원 콘텐츠 위치·원인과 출시 설정 미완료를 실패 UI로 표시하고 취소·재시도를 지원한다. `기존 Steam 게임 (Mirror)`·MapEditor·친구 초대/로비는 기존 경로로 남아 있다.
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
- 공개 로비의 `LobbyItem` 참가 버튼은 생성 시 `ButtonSelectController`의 Back 앞에 들어가고 목록을 비울 때 빠진다. 방향키·게임패드 순서는 Refresh → 로비 목록 → Back이다.
- 공개 로비의 `LobbyContent`는 `ScrollRect`(세로, Clamped)·`RectMask2D`를 갖고, `Content`는 위쪽 고정 앵커에 `ContentSizeFitter`로 항목 수만큼 늘어난다. 6개를 넘으면 스크롤된다.
- `ScrollViewController`는 공개 로비와 키 바인딩 창 2개가 같이 쓴다. 선택이 바뀐 순간에만, 선택 항목(Content 바로 아래 자식)이 보이는 영역을 벗어났을 때 필요한 만큼만 스크롤한다. 매 프레임 가운데로 맞추던 이전 방식은 마우스 휠 스크롤을 되돌려서 바꿨다.
- 비공개 참가는 `TMP_InputField` 기반 코드 입력과 기존 참가 로직을 연결한다.
- 기존 UI를 한 번에 제거하지 않고 새 흐름이 확인된 화면부터 교체한다.
- GameRoom에는 로드 실패 알림 패널(`StageLoadFailureNotice`)이 있다. 화면 전환이 아닌 겹쳐 뜨는 알림이라 `ScreenNavigator`를 쓰지 않는다. 동작은 `Docs/CUSTOM_MAP_NETWORK.md`에 있다.

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

- Quantum 제품5: Main 지원 맵5종·장치 플레이/완료/재진입·취소/실패/중복/씬왕복·잔존과 TMP overflow0을 확인했다. [공통 검증](QUANTUM_MIGRATION.md#검증), [실행·캡처 기록](Notes/2026-11-08-quantum-devices.md).
- 2026-10월 Mirror UI: 화면/설정 트리 왕복·단일 버튼 콜백·포인터/탐색 전환·스크롤·선택 표시·설정 저장/복원·정수 저장값 마이그레이션을 검사했다. 당시 수치/입력 주입/실패 재현은 [과거 상세 검증](Notes/2026-11-08-documentation-reference-history.md)에 보존한다.
- 사용자 Windows 빌드·Steam 로비/패드 실조작 확인은 당시 Mirror 경로의 결과다. Quantum 게임패드/키 재설정과 실제 다인 플레이 검증으로 간주하지 않는다.

## 남은 작업

- 설정 기본값 복귀 수단이 없다. 호출하는 곳이 없던 `SettingManager.ResetSettings`는 제거했다. 기본값 버튼이 필요해지면 `ValueAdjuster`에 기본값 적용 경로를 다시 두고 화면에 연결한다.
- 공개 로비 목록에는 스크롤바가 없다. 마우스 휠과 방향키·게임패드로만 스크롤된다.

## 다음 작업

제품 Quantum 준비/플레이 화면의 실제 두 사람·마우스/게임패드 조작을 확인하고, 이동/스테이지 이식 뒤 Steam 로비·초대의 실제 진입점을 교체한다. 기존 설정·GameRoom의 실조작/키 저장 복원도 별도 회귀로 남긴다. 새 스테이지 선택 UI를 실제 Steam 방에서 열고 방장 맵 선택·시작, 참가자 선택 정보 표시와 게임패드 탭/목록 이동을 확인한다.