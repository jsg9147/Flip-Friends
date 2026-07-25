# Flip Friends Rebuild Plan

## Current Decision

The safest rebuild direction is to replace the menu/UI layer while preserving proven game systems.

This project does not need a full rewrite from zero. The old menu structure is brittle, but the existing Steam, Mirror, input action, sound, prefab, and gameplay work should be treated as reusable assets unless a specific piece is proven broken.

## First Priority

Start with a new main-menu architecture.

The current `Main.unity` contains many UI panels in one scene and uses direct panel toggling through `MainUIManager`. That made sense while the project was growing, but it now creates fragile state transitions, duplicated input handling, and hard-to-debug focus behavior.

The first rebuild milestone should be:

1. Create a clean UI navigation layer.
2. Rebuild the main menu screens around mouse-first UI.
3. Reconnect existing room creation, lobby list, private join, settings, and key rebinding logic one screen at a time.

## Preserve

- `Assets/InputSystem_Actions.inputactions`
- Steam and Mirror room/lobby logic, especially `SteamRoomManager`
- `SoundManager`
- Existing art, prefabs, fonts, and UI assets that still fit the new direction
- Existing gameplay scripts unless they block the new menu flow
- Existing key rebinding concept, but not necessarily the current UI implementation

## Rewrite Or Replace

- `MainUIManager` direct panel toggle flow
- `ButtonSelectController` manual menu navigation
- UI scripts that read directional input in `Update()`
- Private join digit-spinner-only input
- Current settings screen flow
- Current key rebinding screen layout and conflict handling

## Proposed New Structure

Use these concepts for the rebuilt menu:

- `ScreenNavigator`: owns open/back transitions between menu screens.
- `UIScreen`: base component for screen enter/exit behavior.
- `MainMenuScreen`: first screen with play/settings/quit.
- `ModeSelectScreen`: public/private/host choices.
- `HostRoomScreen`: room type, player count, create room.
- `PublicLobbyScreen`: refreshable lobby list with loading, empty, error, and joining states.
- `PrivateJoinScreen`: direct text input for lobby code, with optional gamepad keypad later.
- `SettingsScreen`: audio, graphics, controls entry point.
- `KeyBindingScreen`: keyboard/gamepad bindings, reset, conflict feedback.

## UX Direction

Mouse input is allowed, so the rebuild should be mouse-first and gamepad-compatible later.

Use Unity's built-in UI controls wherever possible:

- `Button` for commands
- `TMP_InputField` for private room code
- `Slider` for volume
- `Toggle` or segmented buttons for binary options
- `Dropdown` or stepper controls for resolution/window mode
- `ScrollRect` for lobby list and key bindings

Avoid rebuilding basic UI navigation manually unless there is a clear gameplay reason.

## Immediate Work Plan

1. Add a new UI architecture folder under `Assets/01_Scripts/UI Scripts/Rebuild`.
2. Implement `UIScreen` and `ScreenNavigator`.
3. Connect `Main.unity` to the new navigation layer without removing old gameplay/network systems.
4. Build the new main screen and mode-select screen.
5. Connect host room creation using existing `SteamRoomManager.HostLobby`.
6. Rebuild public lobby list with loading, empty, error, refresh, and join states.
7. Replace private join digit controls with `TMP_InputField`.
8. Rebuild settings and key binding screens.
9. Remove or retire old menu scripts only after the new flow works.

## Progress So Far

Completed:

- Created `Assets/01_Scripts/UI Scripts/Rebuild`.
- Added `UIScreen.cs`.
- Added `ScreenNavigator.cs`.
- Added `MainMenuScreen.cs`.
- Added `ModeSelectScreen.cs`.
- Added `ScreenNavigationButton.cs`.
- Updated `ScreenNavigator` so it can auto-discover `UIScreen` components even if scene serialization references are incomplete.
- Connected `Main.unity` to the new menu layer in-place.
- Added `ScreenNavigator` to the existing `MainUIManager` GameObject.
- Added `CanvasGroup` plus screen components to these existing panels:
  - `Main UI`
  - `GameMode UI`
  - `Host UI`
  - `Public Lobby UI`
  - `Private Join UI`
  - `Setting UI`
  - `Key Rebinding UI`
- Replaced the generic `UIScreen` on `Main UI` with `MainMenuScreen`.
- Replaced the generic `UIScreen` on `GameMode UI` with `ModeSelectScreen`.
- Set `MainMenuScreen` screen id to `main-menu`.
- Set `ModeSelectScreen` screen id to `mode-select`.
- Set `MainMenuScreen` default selected object to `Start Button`.
- Set `ModeSelectScreen` default selected object to `Host Button`.
- Set `ScreenNavigator.initialScreen` to `Main UI`.
- Wired these buttons through `ScreenNavigationButton`:
  - `Start Button` -> `mode-select`
  - `Setting Button` -> `settings`
  - `Exit Button` -> quit
  - `Host Button` -> `host-room`
  - `Public Button` -> `public-lobby`
  - `Private Button` -> `private-join`
  - `Back Button` in `GameMode UI` -> `Back()`
- Added cancel/back handling to `ModeSelectScreen` through `InputManager.OnCancelEvent`.

Not done yet:

- `Host UI`, `Public Lobby UI`, `Private Join UI`, `Setting UI`, and `Key Rebinding UI` still use generic `UIScreen` only.
- Existing `MainUIManager` direct open methods still exist and are still present in the scene.
- Existing buttons may still also have legacy `onClick` listeners from `MainUIManager`; this has not yet been cleaned up.
- Mouse-first flow is partially connected, but full runtime playthrough verification was not completed.
- Keyboard/gamepad navigation is still mainly relying on the old setup.
- Public lobby state handling (`loading`, `empty`, `error`, `joining`) has not been rebuilt yet.
- Private join has not been converted to `TMP_InputField` yet.
- Settings and key rebinding screens have not been structurally rebuilt yet.

## Current Risks

- The git worktree already has many modified files. Do not clean, reset, or revert unrelated files without explicit approval.
- `Main.unity` is now intentionally modified to host the new navigation layer. Continue carefully and avoid broad scene edits that could disturb unrelated objects.
- DOTween and Unity package files show many changes. Treat those as external/editor-generated unless the task specifically targets them.
- `MainUIManager.KeySettingUIOpen()` currently calls `keySettingUI.SetActive(false)`, so the key settings entry flow is suspicious.
- Manual UI navigation is spread across `InputManager`, `ButtonSelectController`, `HostSetting`, and `PrivateJoinUI`.
- `ScreenNavigator.screens` still shows null serialized entries in inspector/resource output, so the current setup relies on `autoDiscoverScreens = true`.
- Because legacy listeners were not removed yet, some buttons may currently have both old and new navigation paths attached.

## Next Session Prompt

Continue from `REBUILD_PLAN.md`.

Goal: rebuild the main menu/UI layer for Flip Friends while preserving existing Steam/Mirror/gameplay systems.

Current state:

- `Assets/01_Scripts/UI Scripts/Rebuild` already exists.
- `UIScreen`, `ScreenNavigator`, `MainMenuScreen`, `ModeSelectScreen`, and `ScreenNavigationButton` already exist.
- `Main.unity` already has `ScreenNavigator` attached to `MainUIManager`.
- `Main UI` and `GameMode UI` are already connected to the new flow.
- `Start Button` opens `mode-select`, and `GameMode UI` back/cancel returns to the previous screen.

Next task:

1. Audit `Main UI` and `GameMode UI` button listeners in `Main.unity` and remove or neutralize any redundant legacy `MainUIManager` listeners only where the new navigation path already replaces them.
2. Create dedicated screen scripts for the next panels, starting with `HostRoomScreen`, `PublicLobbyScreen`, and `PrivateJoinScreen`.
3. Move `Host UI`, `Public Lobby UI`, and `Private Join UI` from generic `UIScreen` to those dedicated classes.
4. Connect `HostRoomScreen` to existing `SteamRoomManager` host logic without changing Steam/Mirror core systems.
5. Rebuild `PublicLobbyScreen` around explicit states: `loading`, `empty`, `error`, `joining`.
6. Replace the old digit-based private join flow with a `TMP_InputField` design, but preserve existing join logic until the new UI is wired.

Constraints:

- Do not reset or revert unrelated modified files.
- Preserve `InputSystem_Actions.inputactions`, `SteamRoomManager`, `SoundManager`, and gameplay systems.
- Keep using `Main.unity` carefully; it is already the active integration scene now.
- Prefer incremental changes and verify Unity compile state after each script change.
- Do not remove old menu scripts entirely until the replacement flow is confirmed working.

## Open Questions

- Should the rebuilt menu live in a new scene, or should `Main.unity` be duplicated and cleaned?
- Should the first playable target be mouse-only, or mouse plus keyboard/gamepad from the start?
- Should the visual style stay close to the current UI, or should the menu get a fresh presentation pass?
- Should settings be saved through `PlayerPrefs`, ScriptableObject-backed defaults, or a small settings service wrapping `PlayerPrefs`?
