# 다음 세션 착수 큐

- 상태: Active
- 최종 갱신: 2026-10-01
- 목적: 다음 세션이 같은 조사를 반복하지 않도록, 남은 작업의 착수 순서와 이미 확인된 사실·먼저 정할 결정만 모아 둔다.
- 각 항목이 무엇이고 지금 구현이 어디까지 됐는지는 `Docs/UI_REBUILD.md`의 `현재 상태`와 `남은 작업`이 기준이다. 이 파일은 순서와 판단 재료만 담고 상태를 중복 기록하지 않는다.
- 항목을 끝내면 결과를 `Docs/UI_REBUILD.md`에 반영하고 이 파일에서 그 항목을 지운다. 큐가 비면 이 파일을 삭제한다.

## 시작 전 체크

1. `CLAUDE.md` 지침대로 `PROJECT_DOCUMENTATION.md` → `AGENTS.md` → `Docs/UI_REBUILD.md`를 읽고, 이 파일에서 할 항목을 고른다.
2. Unity MCP 연결을 `mcpforunity://editor/state`의 `data.advice.ready_for_tools`로 확인한다. 연결돼 있으면 플레이 모드로 검증하고, 끊겨 있으면 컴파일만 확인한다.
3. 컴파일 확인은 에디터가 프로젝트를 열고 있어도 된다.

   ```powershell
   & "C:\Program Files\Unity\Hub\Editor\6000.6.3f1\Editor\Data\DotNetSdk\dotnet.exe" build Assembly-CSharp.csproj
   ```

4. 커밋할 때 주의: 이 저장소에 git user 정보가 설정돼 있지 않아 그냥 `git commit`하면 실패한다. 기존 커밋과 같은 작성자를 쓰려면 `git -c user.name="JoSeunggeun" -c user.email="jsg9147@naver.com" commit ...` 형태로 지정한다.
5. 화면 전환을 건드렸으면 플레이 모드에서 단계마다 `ScreenNavigator.CurrentScreen`과 모든 `UIScreen`의 `activeSelf`·`CanvasGroup.alpha`·`blocksRaycasts`·`IsVisible`이 서로 맞는지 교차 확인한다. 이중 소유 버그를 잡아낸 방법이 이것이고, 같은 종류의 버그는 `activeSelf`만 보면 놓친다.

## 1. 설정 저장 경로 복구 (최우선)

**먼저 정할 것**: 설정 값 저장을 `ValueAdjuster`의 즉시 저장으로 단일화할지, `SettingManager.SaveSettings`의 일괄 저장으로 단일화할지. 해상도는 즉시 적용이 위험하니 적용 시점(Apply 버튼 신설 vs `SettingsScreen.OnHide`)도 같이 정한다.

확인된 사실:

- `ApplySettings`와 `ResetSettings`를 호출하는 곳이 씬·프리팹·코드 어디에도 없다. 정의한 `SettingManager.cs`만 매치된다. 그래서 해상도는 ±로 표시만 바뀌고 실제 적용·저장이 되지 않는다.
- `SaveSettings`는 `ScreenRed`/`ScreenGreen`/`ScreenBlue`로 쓰는데 `LoadSettings`는 `Red`/`Green`/`Blue`로 읽어 색이 저장되지 않는다.
- `ValueAdjuster`는 값이 바뀔 때마다 자기 `key`(`BGMVolume`, `SFXVolume`, `Red`, `Green`, `Blue`)로 즉시 저장한다. 즉 음량과 색은 이미 저장되고 있고 `SaveSettings`의 해당 쓰기는 중복이다.
- `ResolutionAdjuster`는 `SavedResolution` 키를 쓰고 `ApplyResolution`에서만 저장한다.
- 권장안: 즉시 저장으로 단일화하고 `SaveSettings`에서 음량·색 중복 쓰기를 없앤다. 해상도만 `ApplySettings`로 확정하고, 그 호출 지점을 위 결정에 따라 둔다.
- 대상 파일: `Assets/01_Scripts/Setting/SettingManager.cs`, `ValueAdjuster.cs`, `ResolutionAdjuster.cs`, 적용 시점을 화면에 두면 `UI Scripts/Rebuild/SettingsScreen.cs`.
- 검증: 플레이 모드에서 값 변경 → 플레이 종료 → 재진입 시 값이 유지되는지. 코드상 `ResolutionAdjuster.LoadResolution`은 기본 해상도가 목록에 없으면 `currentIndex`가 -1이 되어 `UpdateResolutionText`에서 예외가 날 수 있으니 이때 함께 확인한다.

## 2. MainUIManager legacy 경로 제거

- 씬에서 `MainUIManager`와 `SettingManager`를 직접 호출하는 `onClick`은 이제 0개다. 마지막 하나였던 `Setting UI/Frame/Close Button`을 Back으로 교체했다. 따라서 `UIReset`과 화면별 `GameObject` 필드 5개(`mainUI`=Main UI, `gameModeUI`=GameMode UI, `hostUI`=Host UI, `publicGameUI`=Public Lobby UI, `privateJoinUI`=Private Join UI)는 제거할 수 있다.
- 단, `GameModeUIOpen`은 코드 5곳에서 아직 호출된다. `UI Scripts/HostSetting.cs:118`, `UI Scripts/PublicLobbyUI.cs:79`, `Rebuild/HostRoomScreen.cs:184`, `Rebuild/PrivateJoinScreen.cs:140`, `Rebuild/PublicLobbyScreen.cs:169`.
- **먼저 정할 것**: 이 5곳을 `ScreenNavigator.Open("mode-select")`로 바꿀지, `GameModeUIOpen` 브리지를 남길지. Rebuild 화면 3개는 Cancel을 `Back()`이 아니라 `GameModeUIOpen`으로 처리하고 있어 히스토리가 비어도 `mode-select`로 간다. 이 동작을 유지할지 Back으로 통일할지가 3번과 함께 걸려 있다.

## 3. Cancel 처리 NavigableScreen 통합

- `ModeSelectScreen`은 `navigator.Back()`을 쓰고, `HostRoomScreen`·`PublicLobbyScreen`·`PrivateJoinScreen`은 `GameModeUIOpen()`을 쓴다. 구독·해제 코드만 같고 동작이 달라 단순 치환이 아니다. 2번의 결정과 묶어서 처리한다.
- `NavigableScreen`에는 `TryHandleCancel` 같은 훅이 없다. 화면별로 다른 Cancel 동작을 유지하려면 가상 메서드를 하나 두고 기본 구현을 `Back()`으로 하면 된다.

## 4. 레거시 UI 컴포넌트 정리

- `Canvas/Public Lobby UI`에는 새 `PublicLobbyScreen`과 레거시 `PublicLobbyUI`가 함께 붙어 있고, `Canvas/Host UI`에는 `HostRoomScreen`과 레거시 `HostSetting`이 함께 붙어 있다. 둘 다 `OnCancelEvent`를 구독하는 코드를 갖고 있다.
- `ButtonNavigation`은 2개다. `Canvas/Public Lobby UI`(`beforeUI`=GameMode UI, `uiElements` 2개)와 `Canvas/Public Lobby UI/LobbyContent`(`beforeUI`=null이라 `ReturnUI`가 무동작). `ButtonNav`은 `Canvas/Gamepad Setting/GamepadKey bind Scroll View ` 1개뿐이다.
- `ButtonNavigation.ReturnUI`는 `beforeUI.SetActive(true)` + 자기 비활성으로 내비게이터를 우회한다. 플레이 모드에서 확인한 현재 동작은 정상인데, 뒤이어 실행되는 `PublicLobbyScreen.HandleCancel`의 `Open`이 상태를 덮어쓰기 때문이다. 구독 순서에 의존하는 구조라 제거 대상이다.
- 공개 로비에서 Cancel 구독자는 실제로 `ButtonNavigation.ReturnUI` 2개와 `PublicLobbyScreen.HandleCancel`이었다. `PublicLobbyUI.CancelBtnEvent`는 구독되지 않았는데 `OnEnable`이 `async void`로 Steam 목록 갱신을 먼저 await하기 때문이다. Steam 상태에 따라 구독 여부가 달라지므로 6번에서 Steam을 켜고 한 번 더 확인한다.

## 5. Screen Mode 전환 신규 구현

- `Graphics and Audio Window/Setting Groups/Screen Mode Group/Screen Mode Text (TMP)`의 `Left Button`·`Right Button` `onClick`이 삭제된 `SettingsMenu.ChangeFullscreenMode`를 가리켜 `m_Target: {fileID: 0}`으로 죽어 있다. 키보드·게임패드 경로도 없어 복구가 아니라 신규 구현이다.
- `ResolutionAdjuster`와 같은 모양으로 표시 텍스트·`PlayerPrefs` 저장·`Screen.fullScreenMode` 적용을 담당하는 어댑터를 만들고 ± 버튼 2개를 연결한다. 저장 방식은 1번에서 정한 것을 그대로 따른다.

## 6. Steam 런타임 수동 검증

- Host 생성, 공개 로비 목록의 loading·empty·error·joining, 비공개 코드 참가, 각 실패 상태. MCP로 자동화할 수 없어 수동이다.
- 같이 확인할 것: 4번의 `PublicLobbyUI` Cancel 구독 여부.

## 7. CP949 스크립트 UTF-8 변환

- `Assets/01_Scripts`에 18개 남아 있다. `GameManager`, `SoundManager`, `RopeCreatorNetwork`, `InputManager`, `ScrollViewController`, `KeyRebindingManager`, `GameRoomUI`, `Switch`, `LayerBasedSwitch`, `SavePoint`, `CameraController`, `PlayerSound`, `SteamLobbyInfo`, `RotatingObstacle`, `ObjSummonSwitch`, `PlayerSummonSwitch`, `Delete/ButtonNav`, `Delete/ButtonNavigation`.
- 목록은 다시 뽑을 수 있다.

  ```bash
  python -c "
  import os
  for root,_,fs in os.walk('Assets/01_Scripts'):
      for f in fs:
          if f.endswith('.cs'):
              p=os.path.join(root,f)
              try: open(p,'rb').read().decode('utf-8')
              except UnicodeDecodeError: print(p)
  "
  ```

- 변환은 `cp949`로 읽어 `utf-8`로 쓰고 CRLF를 유지한다. 기능 변경이 없으니 다른 이유로 그 파일을 열 때 함께 바꾸거나, 변환만 하는 커밋으로 분리한다.
