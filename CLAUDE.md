# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

**Flip Friends** is a 2D multiplayer co-op platformer built with Unity and Mirror Networking. Up to 4 players work together to climb levels, solve puzzles, carry objects/players, and reach finish points. Uses Steam (FizzySteamworks) for lobby management.

## Architecture

### Networking Model (Mirror Framework)
- **Server Authority**: All gameplay logic (movement, physics, collision) runs on server — `MovementHandler.FixedUpdate` has a hard `if (!isServer) return` guard
- **Client-to-Server**: Use `[Command]` for player actions (e.g., `CmdJumpInputDown`, `CmdObjectInteraction`)
- **Server-to-Clients**: Use `[ClientRpc]` for state broadcasts (e.g., `RpcFlipChanged`, `RpcVelocityReset`)
- **State Sync**: Use `[SyncVar(hook = nameof(...))]` for automatic hook callbacks on all clients
- Always check `isServer` / `isOwned` / `isLocalPlayer` before executing context-specific code

### Core Script Architecture

**Network Layer** (`Assets/01_Scripts/NetworkScripts/`):
- `SteamRoomManager.cs` — Steam lobby creation, join codes, Steamworks callbacks; holds `playerName`
- `SlimeRoomManager.cs` — Extends `NetworkRoomManager`; holds `currentStage` (int index into `StageManager.stageMapPrefabs`); handles scene transitions and player reconnection after stage clear
- `CustomRoomPlayer.cs` — Lobby player state (name, color, ready state via `SyncVar`); bridges to `PlayerController2D` after scene load

**Player System** (`Assets/01_Scripts/PlayerScripts/New Scripts/`):
- `PlayerController2D.cs` — Central coordinator; owns all subsystems, handles `[Command]` dispatch, finish state, and damage routing. `Update` (client-side input) calls Commands; `FixedUpdate` (server-side) drives state/animation
- `PlayerInputManager.cs` — New Input System callbacks; exposes input state as properties (`IsJumpPressed`, `MovementInput`, etc.)
- `MovementHandler.cs` — Physics-based movement (server-only `FixedUpdate`); gravity derived from `maxJumpHeight`/`timeToJumpApex`; handles wall jumping, rope climbing, conveyor acceleration, invincibility, and knockback
- `Controller2D.cs` — Raycast collision system; exposes `collisions` struct (`below`, `above`, `left`, `right`, `slidingDownMaxSlope`, `slopeNormal`); tracks `underPlayer` and `onConveyor`
- `RaycastController.cs` — Base for `Controller2D` and `Switch`; manages ray origins and spacing
- `PlayerInteraction.cs` — Pickup/carry/throw for both `PickupObj` and other `PlayerController2D` instances; uses multi-ray box scan on "Pickable" and "Player" layers

**Game Objects** (`Assets/01_Scripts/GameObjScripts/` & `ObstacleScripts/`):
- `PickupObj.cs` — Carryable objects with physics and network sync
- `MovingPlatform.cs` — Lerp-based platforms with RPC position sync
- `BasicTrap.cs` — Base trap class; exposes `knockbackDir` for custom knockback direction
- `RotatingObstacle.cs` — Extends `BasicTrap`
- `Conveyor.cs` — Directional movement surface; `isClockwise` controls direction; integrated via `Controller2D.onConveyor`

**Puzzle System** (`Assets/01_Scripts/PlayingScripts/SwitchScripts/`):
- `Switch.cs` — Abstract base extending `RaycastController`; `SyncVar isActivated`; `DetectPlayer` uses downward raycasts; subclasses override `OnSwitchStateChanged`
- `LayerBasedSwitch.cs` — Trigger-based activation
- `DoorSwitch.cs` — Controls door state via RPC

**Game Management**:
- `GameManager.cs` — Singleton; `FinishCheck()` called by `PlayerController2D` when `isFinish` SyncVar changes; triggers `SlimeRoomManager.ReturnRoomScene()` when all players finish
- `StageManager.cs` — Server-only; reads `SlimeRoomManager.currentStage` in `OnStartServer`, instantiates and `NetworkServer.Spawn`s the stage prefab
- `RespawnHandler.cs` — Layer-based respawn triggers; `SavePoint.cs` tracks ordered save points by `savePointID`

### Player States
`PlayerState` enum (in `PlayerController2D.cs`): `Idle`, `Walk`, `Jump`, `Damaged`, `Attack`, `Climb`, `ClimbIdle`, `Shrink`, `Carried`, `Throw`

### Input System
Uses Unity's New Input System. `PlayerInputManager` captures input via callbacks, passes to `MovementHandler` via Commands, server processes physics, clients receive updates via ClientRpc. `InputManager` (UI layer singleton) exposes `OnSubmitEvent` and `OnMenuEvent` actions.

## Scene Flow

1. `Main.unity` — Main menu / lobby selection
2. `GameRoom.unity` — Room lobby (`CustomRoomPlayer` ready-up, stage selection via `MapSelectionManager`)
3. `GamePlay.unity` — Gameplay; `StageManager` spawns selected stage prefab on server start

After all players enter the finish trigger (`isFinish = true`), `GameManager.StageClear()` calls `SlimeRoomManager.ReturnRoomScene()` which returns to `GameRoom.unity`.

## Key Dependencies

- **Mirror** — Networking framework
- **FizzySteamworks** — Steam transport for Mirror
- **Steamworks.NET** — Steam API
- **DOTween/DOTweenPro** — Tweening animations
- **Universal Render Pipeline (URP)** — 2D rendering
- **New Input System** — Modern input handling

## 코드 작성 원칙

### SOLID 원칙 준수
- **단일 책임 원칙 (SRP)**: 클래스 하나는 하나의 역할만 담당. 예) `MovementHandler`는 이동만, `PlayerInteraction`은 상호작용만 처리
- **개방-폐쇄 원칙 (OCP)**: 기존 코드 수정 없이 확장 가능하도록 설계. 예) 새 장애물은 `BasicTrap`을 상속, 새 스위치는 `Switch`를 상속
- **리스코프 치환 원칙 (LSP)**: 자식 클래스는 부모 클래스를 대체할 수 있어야 함. `RotatingObstacle`은 `BasicTrap`을 완전히 대체 가능해야 함
- **인터페이스 분리 원칙 (ISP)**: 불필요한 의존성을 강제하지 않도록 인터페이스를 작게 유지
- **의존성 역전 원칙 (DIP)**: 구체 구현이 아닌 추상(인터페이스/추상클래스)에 의존. 예) `Switch`의 `OnSwitchStateChanged`는 추상 메서드로 정의

### 클린 코드 원칙 준수
- 메서드는 하나의 일만 수행하고, 20줄을 넘지 않도록 유지
- 매직 넘버 사용 금지 — 상수 또는 Inspector 공개 필드로 정의
- 의미 있는 이름 사용: 약어나 단일 문자 변수 지양 (`i` 같은 루프 인덱스 제외)
- 중복 코드 제거 — 동일한 로직이 두 곳 이상이면 공통 메서드로 추출
- 네트워크 코드에서 `isServer` / `isOwned` 조건 분기는 메서드 진입부에서 조기 반환(early return)으로 처리

### 네이밍 컨벤션
- **클래스 / 메서드**: PascalCase — `MovementHandler`, `OnJumpInputDown`
- **private 필드**: camelCase — `heldObject`, `currentDelay`
- **public 프로퍼티**: PascalCase — `IsCarried`, `CurrentVelocity`
- **`[Command]`**: `Cmd` 접두사 — `CmdJumpInputDown`, `CmdObjectInteraction`
- **`[ClientRpc]`**: `Rpc` 접두사 — `RpcFlipChanged`, `RpcVelocityReset`
- **`[SyncVar]` hook**: `On + 변수명 + Changed` 형태 권장 — `OnNameChanged`, `OnColorChange`
- **추상 이벤트 메서드**: `On` 접두사 — `OnSwitchStateChanged`, `OnStartServer`
- **bool 변수**: `is` / `has` / `can` 접두사 — `isServer`, `isCarried`, `canJump`

### 주석 규칙
- **주석은 반드시 한글로 작성**
- WHY(왜 이렇게 했는지)만 주석으로 달고, WHAT(무엇을 하는지)은 코드 자체가 설명하도록 작성
- 자명한 코드에는 주석 생략

## Development Patterns

### Adding New Features

**New Obstacle**: Extend `BasicTrap`; set `knockbackDir` in the Inspector for non-radial knockback; tag the collider "Trap" or "Enemy" so `PlayerController2D.HandleDamage` picks it up

**New Switch Type**: Extend `Switch` or `LayerBasedSwitch`, override `OnSwitchStateChanged`; `isActivated` is automatically synced via `SyncVar`

**New Player State**: Add to `PlayerState` enum in `PlayerController2D.cs`, update `PlayerStateController` and `PlayerAnimationController`

**Networked State**: Use `[SyncVar(hook = nameof(HookMethod))]`; hook runs on all clients automatically

**New Carryable Object**: Add `PickupObj` component and set layer to "Pickable" so `PlayerInteraction.SearchObject` finds it

### Collision System
Uses raycast-based collision detection (`Controller2D` / `RaycastController`) rather than Rigidbody physics. This provides precise platformer control including coyote time, slope handling, wall sliding/jumping. The `Controller2D.collisions` struct is the authoritative ground-truth for all movement decisions.

### Singletons
`GameManager`, `StageManager`, `SoundManager`, `InputManager` all use singleton pattern. Note: `GameManager` does **not** use `DontDestroyOnLoad` — it is scene-scoped and resets `Instance` per scene.

### Layer/Tag Conventions
- Tags: `"Trap"`, `"Enemy"` → damage; `"Rope"` → climbing; `"Finish"` → stage end; `"Reset"` → respawn; `"Bounce"` / `"Spring"` → velocity modifiers
- Layers: `"Player"` → player detection raycasts; `"Pickable"` → pickup object detection

## Project Settings

- Target: Windows Standalone (1600x900)
- Product Name: "Slime Climb"
- Company: "MNSGStudio"
