# 2026-11-15 Mirror 원본 장치 수치

Mirror 제거 직전(`238f335`) 원본 프리팹에 붙어 있던 장치 스크립트의 직렬화 값. 스크립트를 지운 뒤 프리팹은 지형·스프라이트 참고용으로만 남는다. 공식 맵을 Quantum 작성 컴포넌트로 다시 만들 때 이 값을 참고한다. 참조 필드는 대상 오브젝트 이름이다.

## Box.prefab

| 경로 | 스크립트 | 위치 | 값 |
|---|---|---|---|
| Box | PickupObj | (5, 0) | offsetApply=false; collisionMask=mask3392; maxSlopeAngle=45; gravity=9.8; velocity=(0, 0); wallSlideSpeedMax=3 |

## ExitPanel.prefab

| 경로 | 스크립트 | 위치 | 값 |
|---|---|---|---|
| ExitPanel | ButtonSelectController | (0, 0) | tagetButtonList=[Back Button, Exit Button]; inputDelay=0.2 |
| ExitPanel | ExitPanel | (0, 0) |  |

## KeyBind Prefab.prefab

| 경로 | 스크립트 | 위치 | 값 |
|---|---|---|---|
| KeyBind Prefab | KeyBindItem | (0, 0) | antionNameText=ActionName; keyBindText=Text (TMP); keyBindBtn=KeyBindButton |

## LobbyItem.prefab

| 경로 | 스크립트 | 위치 | 값 |
|---|---|---|---|
| LobbyItem | LobbyItem | (0, 0) | ownerNameText=OwnerText; currentMemberText=Current Member Text (TMP); lobbyStateText=StateText |

## MapEditor/MapEditorCannon.prefab

| 경로 | 스크립트 | 위치 | 값 |
|---|---|---|---|
| MapEditorCannon | CannonShooter | (0, 0) | projectilePrefab=CannonBall; firstShootDelay=0; shootInterval=2; projectileSpeed=10; maxProjectileDistance=20; shootDirection=(1, 0) |

## MapEditor/PlayerSpawnPoint.prefab

| 경로 | 스크립트 | 위치 | 값 |
|---|---|---|---|
| PlayerSpawnPoint | PlayerSpawnPoint | (0, 0) | markerRenderer=PlayerSpawnPoint |

## Network/MapDataNetworkSync.prefab

| 경로 | 스크립트 | 위치 | 값 |
|---|---|---|---|
| MapDataNetworkSync | MapDataNetworkSync | (0, 0) |  |

## Obstacle/Box Switch Prefab.prefab

| 경로 | 스크립트 | 위치 | 값 |
|---|---|---|---|
| Box Switch Prefab/Box Switch Set | ObjSummonSwitch | (0, 0) | summonLocation=Box Respawn; cooldownTime=5; targetObjTrans=Box; defaultSprite=Switch_0; pressedSprite=Switch_1 |
| Box Switch Prefab/Box | PickupObj | (0, 0) | offsetApply=false; collisionMask=mask3520; maxSlopeAngle=45; gravity=9.8; velocity=(0, 0); wallSlideSpeedMax=3 |

## Obstacle/Cannon.prefab

| 경로 | 스크립트 | 위치 | 값 |
|---|---|---|---|
| Cannon | CannonShooter | (0, 0) | projectilePrefab=CannonBall; firstShootDelay=0; shootInterval=2; projectileSpeed=10; maxProjectileDistance=20; shootDirection=(1, 0) |

## Obstacle/CannonBall.prefab

| 경로 | 스크립트 | 위치 | 값 |
|---|---|---|---|
| CannonBall | CannonProjectile | (0.6, 0) |  |

## Obstacle/Conveyor belt.prefab

| 경로 | 스크립트 | 위치 | 값 |
|---|---|---|---|
| Conveyor belt | Conveyor | (0, 0) | beltAnimators=[Left, Middle, Right] |

## Obstacle/Move Trap Round.prefab

| 경로 | 스크립트 | 위치 | 값 |
|---|---|---|---|
| Move Trap Round | MovingTrap | (0, 0) | knockbackDir=(0, 0); endPosition=(0, 0); speed=2; isRotate=false; rotationSpeed=180 |

## Obstacle/Move Trap.prefab

| 경로 | 스크립트 | 위치 | 값 |
|---|---|---|---|
| Move Trap | MovingTrap | (0, 0) | knockbackDir=(0, 0); endPosition=(0, 0); speed=2; isRotate=false; rotationSpeed=180 |

## Player.prefab

| 경로 | 스크립트 | 위치 | 값 |
|---|---|---|---|
| Player | PlayerAnimationController | (0, 0) | animator=Player; spriteRenderer=null; minOneShotDuration=0.25; damageFlashInterval=0.08 |
| Player | PlayerInputManager | (0, 0) | controller=null |
| Player | Controller2D | (0, 0) | offsetApply=false; collisionMask=mask108992; maxSlopeAngle=30; coyoteTimeDuration=0.2; objCollider=Obj Collider; onConveyor=null |
| Player | PlayerController2D | (0, 0) | nameText=Name Text (TMP); readySprite=Ready sprite; playerName="No Name"; isFinish=false |
| Player | MovementHandler | (0, 0) | maxJumpHeight=2.2; minJumpHeight=1.2; runJumpHeight=2; timeToJumpApex=0.3; moveSpeed=4; runSpeed=5.5; conveyorSpeed=1; conveyorAccelerationSpeed=1; wallJumpClimb=(10, 25); wallJumpOff=(13, 27); wallLeap=(16, 25); wallSlideSpeedMax=3; wallStickTime=0.25; wallSlideTime=0; bounceForce=20; springJumpForce=50; damagedMove=(20, 20); invincibilityDuration=1 |
| Player | PlayerStateController | (0, 0) |  |
| Player | PlayerInteraction | (0, 0) | throwForce=6; catchedCollider=Obj Collider; detectionLayer=mask19904; heldPos=(0, 0.95, 0); playerThrowVelocity=(7, 12); thrownUncontrollableDuration=0.4; carryLockDuration=1.5; throwDealy=0.15 |
| Player | PlayerSound | (0, 0) | jumpSound=JumpSound; shirinkSound=ShrinkSound; enterSound=FiniishSound ; exitSound=ExitSound |
| Player | ClientMover | (0, 0) |  |
| Player | ServerMover | (0, 0) |  |
| Player | ThrownPlayerSmoother | (0, 0) | blendDuration=0.3 |

## RoomPlayer.prefab

| 경로 | 스크립트 | 위치 | 값 |
|---|---|---|---|
| RoomPlayer | CustomRoomPlayer | (0, 0) | showRoomGUI=true; readyToBegin=false; index=0; playerName="No Name" |

## Stage/Stage 1.prefab

| 경로 | 스크립트 | 위치 | 값 |
|---|---|---|---|
| Stage 1/Y Move 1 | DynamicObj | (0, 0) | xMoveValue=0; yMoveValue=1 |
| Stage 1/MoveTraps/Move Trap | MovingTrap | (12.5, 28.5) | knockbackDir=(0, 0); endPosition=(-8, 0); speed=5; isRotate=true; rotationSpeed=1000 |
| Stage 1/MoveTraps/Move Trap (1) | MovingTrap | (4.5, 32.5) | knockbackDir=(0, 0); endPosition=(-8, 0); speed=5; isRotate=true; rotationSpeed=1000 |
| Stage 1/MoveTraps/Move Trap (2) | MovingTrap | (6.5, 41.5) | knockbackDir=(0, 0); endPosition=(-8, 0); speed=5; isRotate=true; rotationSpeed=1000 |
| Stage 1/MoveTraps/Move Trap (3) | MovingTrap | (0.5, 44.5) | knockbackDir=(0, 0); endPosition=(-8, 0); speed=5; isRotate=true; rotationSpeed=1000 |

## Stage/Stage 10.prefab

| 경로 | 스크립트 | 위치 | 값 |
|---|---|---|---|
| Stage 10/Door/PlayerDoorSwitch (2) | DoorSwitch | (80.5, 25.5) | offsetApply=false; collisionMask=mask0; onSwitchSprite=Switch_1; offSwitchSprite=Switch_0; activationLayer=mask256; door=Door |
| Stage 10/MoveTraps/Move Trap (1) | MovingTrap | (46.5, -0.5) | knockbackDir=(0, 0); endPosition=(0, 0); speed=0; isRotate=true; rotationSpeed=1000 |
| Stage 10/MoveTraps/Move Trap (2) | MovingTrap | (50.5, -0.5) | knockbackDir=(0, 0); endPosition=(0, 0); speed=0; isRotate=true; rotationSpeed=1000 |
| Stage 10/MoveTraps/Move Trap (3) | MovingTrap | (54.5, -0.5) | knockbackDir=(0, 0); endPosition=(0, 0); speed=0; isRotate=true; rotationSpeed=1000 |
| Stage 10/MoveTraps/Move Trap (4) | MovingTrap | (62.8, 14.4) | knockbackDir=(0, 0); endPosition=(15, 0); speed=2; isRotate=true; rotationSpeed=1000 |
| Stage 10/MoveTraps/Move Trap (5) | MovingTrap | (62.8, 16.98) | knockbackDir=(0, 0); endPosition=(10, 0); speed=3; isRotate=true; rotationSpeed=1000 |
| Stage 10/MoveTraps/Move Trap (6) | MovingTrap | (65.2, 29.5) | knockbackDir=(0, 0); endPosition=(10, 0); speed=4; isRotate=true; rotationSpeed=1000 |
| Stage 10/RotateOpstacles/RotateObstacle (1) | RotatingObstacle | (68.5, 6.5) | knockbackDir=(0, 0); isCounterclockwise=true; rotationSpeed=70; random=true; randomRange=10 |
| Stage 10/RotateOpstacles/RotateObstacle (4) | RotatingObstacle | (68.5, 19) | knockbackDir=(0, 0); isCounterclockwise=true; rotationSpeed=70; random=true; randomRange=10 |
| Stage 10/RotateOpstacles/RotateObstacle (5) | RotatingObstacle | (73.62, 23.8) | knockbackDir=(0, 0); isCounterclockwise=true; rotationSpeed=70; random=true; randomRange=15 |
| Stage 10/RotateOpstacles/RotateObstacle (3) | RotatingObstacle | (65.56, 13.36) | knockbackDir=(0, 0); isCounterclockwise=true; rotationSpeed=90; random=true; randomRange=10 |
| Stage 10/RotateOpstacles/RotateObstacle (6) | RotatingObstacle | (71.5, 3.5) | knockbackDir=(0, 0); isCounterclockwise=true; rotationSpeed=30; random=true; randomRange=0 |
| Stage 10/RotateOpstacles/RotateObstacle (2) | RotatingObstacle | (73.4, 1.2) | knockbackDir=(0, 0); isCounterclockwise=true; rotationSpeed=60; random=true; randomRange=10 |
| Stage 10/Grid/BoxResetTile | RespawnHandler | (3, 1) | targetLayers=mask2048; resetPoint=RespawnPoint; onlyBoxReset=true |
| Stage 10/Grid/BoxResetTile (1) | RespawnHandler | (3, 1) | targetLayers=mask2048; resetPoint=RespawnPoint (1); onlyBoxReset=true |
| Stage 10/Boxes/Box Switch Prefab/Box Switch Set | ObjSummonSwitch | (0, -1.5) | summonLocation=Box Respawn; cooldownTime=5; targetObjTrans=Box; defaultSprite=Switch_0; pressedSprite=Switch_1 |
| Stage 10/Boxes/Box Switch Prefab/Box | PickupObj | (7, 0) | offsetApply=false; collisionMask=mask3520; maxSlopeAngle=45; gravity=9.8; velocity=(0, 0); wallSlideSpeedMax=3 |
| Stage 10/Cannons/Canon | CannonShooter | (41.5, -0.5) | projectilePrefab=CannonBall; firstShootDelay=0; shootInterval=2; projectileSpeed=10; maxProjectileDistance=20; shootDirection=(-1, 0) |
| Stage 10/Cannons/Canon (1) | CannonShooter | (41.5, -1.5) | projectilePrefab=CannonBall; firstShootDelay=0; shootInterval=2; projectileSpeed=5; maxProjectileDistance=20; shootDirection=(-1, 0) |
| Stage 10/Cannons/Canon (2) | CannonShooter | (78.4, 22.5) | projectilePrefab=CannonBall; firstShootDelay=0; shootInterval=2; projectileSpeed=5; maxProjectileDistance=20; shootDirection=(-1, 0) |

## Stage/Stage 11.prefab

| 경로 | 스크립트 | 위치 | 값 |
|---|---|---|---|
| Stage 11/MoveTraps/Move Trap (1) | MovingTrap | (-7, -8.5) | knockbackDir=(0, 0); endPosition=(14, 0); speed=7; isRotate=true; rotationSpeed=1000 |
| Stage 11/MoveTraps/Move Trap (2) | MovingTrap | (-3, -20.5) | knockbackDir=(0, 0); endPosition=(6, 0); speed=5; isRotate=true; rotationSpeed=1000 |
| Stage 11/MoveTraps/Move Trap (3) | MovingTrap | (-3.5, -94.5) | knockbackDir=(0, 0); endPosition=(7, 0); speed=5; isRotate=true; rotationSpeed=1000 |
| Stage 11/MoveTraps/Move Trap (4) | MovingTrap | (-3.5, -111.8) | knockbackDir=(0, 0); endPosition=(7, 0); speed=4.2; isRotate=true; rotationSpeed=1000 |
| Stage 11/MoveTraps/Move Trap (5) | MovingTrap | (0, -129.7) | knockbackDir=(0, 0); endPosition=(0, 0); speed=0; isRotate=true; rotationSpeed=1000 |
| Stage 11/RotateTraps/RotateTrap | RotatingObstacle | (3, -17) | knockbackDir=(1, 0); isCounterclockwise=false; rotationSpeed=70; random=false; randomRange=0 |
| Stage 11/RotateTraps/RotateTrap (1) | RotatingObstacle | (-0.1, -121.7) | knockbackDir=(-1, 0); isCounterclockwise=false; rotationSpeed=80; random=false; randomRange=0 |
| Stage 11/Grid/ResetTile | RespawnHandler | (3, 1) | targetLayers=mask2048; resetPoint=RespawnPoint; onlyBoxReset=false |
| Stage 11/Grid/ResetTile (1) | RespawnHandler | (3, 1) | targetLayers=mask2048; resetPoint=RespawnPoint (1); onlyBoxReset=false |
| Stage 11/Grid/ResetTile (2) | RespawnHandler | (0, 0) | targetLayers=mask2048; resetPoint=RespawnPoint (2); onlyBoxReset=false |
| Stage 11/Cannons/Canon | CannonShooter | (7.5, -26.5) | projectilePrefab=CannonBall; firstShootDelay=0; shootInterval=2; projectileSpeed=11; maxProjectileDistance=20; shootDirection=(-1, 0) |
| Stage 11/Cannons/Canon (1) | CannonShooter | (-7.5, -25.5) | projectilePrefab=CannonBall; firstShootDelay=0; shootInterval=2; projectileSpeed=11; maxProjectileDistance=20; shootDirection=(1, 0) |
| Stage 11/Cannons/Canon (2) | CannonShooter | (-7.5, -99.5) | projectilePrefab=CannonBall; firstShootDelay=0; shootInterval=2; projectileSpeed=11; maxProjectileDistance=20; shootDirection=(1, 0) |
| Stage 11/OnOffManager | OnOffManager | (0, 0) | switches=[OnOff Switch, OnOff Switch (1), OnOff Switch (2), OnOff Switch (3), OnOff Switch (4), OnOff Switch (5), OnOff Switch (6), OnOff Switch (7), OnOff Switch (8), OnOff Switch (9), OnOff Switch (10)]; conveyorBelts=[]; activeSwitchGround=OnSwitchGround; inactiveSwitchGround=OffSwitchGround; autoToggleEnabled=false; autoToggleInterval=3 |
| Stage 11/OnOffManager/OnOff Switch | OnOffSwitch | (3, -2.5) | offsetApply=false; collisionMask=mask2048; onSwitchSprite=OnOff_0; offSwitchSprite=OnOff_1; onOffManager=OnOffManager; toggleCooldown=0.5 |
| Stage 11/OnOffManager/OnOff Switch (1) | OnOffSwitch | (-2.5, -7) | offsetApply=false; collisionMask=mask2048; onSwitchSprite=OnOff_0; offSwitchSprite=OnOff_1; onOffManager=OnOffManager; toggleCooldown=0.5 |
| Stage 11/OnOffManager/OnOff Switch (2) | OnOffSwitch | (3, -11) | offsetApply=false; collisionMask=mask2048; onSwitchSprite=OnOff_0; offSwitchSprite=OnOff_1; onOffManager=OnOffManager; toggleCooldown=0.5 |
| Stage 11/OnOffManager/OnOff Switch (3) | OnOffSwitch | (0, -15) | offsetApply=false; collisionMask=mask2048; onSwitchSprite=OnOff_0; offSwitchSprite=OnOff_1; onOffManager=OnOffManager; toggleCooldown=0.5 |
| Stage 11/OnOffManager/OnOff Switch (4) | OnOffSwitch | (-2.5, -19) | offsetApply=false; collisionMask=mask2048; onSwitchSprite=OnOff_0; offSwitchSprite=OnOff_1; onOffManager=OnOffManager; toggleCooldown=0.5 |
| Stage 11/OnOffManager/OnOff Switch (5) | OnOffSwitch | (0, -23.5) | offsetApply=false; collisionMask=mask2048; onSwitchSprite=OnOff_0; offSwitchSprite=OnOff_1; onOffManager=OnOffManager; toggleCooldown=0.5 |
| Stage 11/OnOffManager/OnOff Switch (6) | OnOffSwitch | (-1, -82.47) | offsetApply=false; collisionMask=mask2048; onSwitchSprite=OnOff_0; offSwitchSprite=OnOff_1; onOffManager=OnOffManager; toggleCooldown=0.5 |
| Stage 11/OnOffManager/OnOff Switch (7) | OnOffSwitch | (2.5, -87.5) | offsetApply=false; collisionMask=mask2048; onSwitchSprite=OnOff_0; offSwitchSprite=OnOff_1; onOffManager=OnOffManager; toggleCooldown=0.5 |
| Stage 11/OnOffManager/OnOff Switch (8) | OnOffSwitch | (-2.5, -92.5) | offsetApply=false; collisionMask=mask2048; onSwitchSprite=OnOff_0; offSwitchSprite=OnOff_1; onOffManager=OnOffManager; toggleCooldown=0.5 |
| Stage 11/OnOffManager/OnOff Switch (9) | OnOffSwitch | (2.5, -97.5) | offsetApply=false; collisionMask=mask2048; onSwitchSprite=OnOff_0; offSwitchSprite=OnOff_1; onOffManager=OnOffManager; toggleCooldown=0.5 |
| Stage 11/OnOffManager/OnOff Switch (10) | OnOffSwitch | (2.5, -119) | offsetApply=false; collisionMask=mask2048; onSwitchSprite=OnOff_0; offSwitchSprite=OnOff_1; onOffManager=OnOffManager; toggleCooldown=0.5 |
| Stage 11/MoveObstacles/Player Detected Platform | PlayerDetectPlatform | (-3, -13.1) | offsetApply=false; collisionMask=mask2304; previewColor=FF0000FF; passengerMask=mask2048; localWaypoints=[(0, 0, 0), (15, 0, 0)]; speed=1; cyclic=false; isRepeat=false; originalPositionDelay=0; waitTime=0; easeAmount=0.2; isPassengerTransportEnabled=true; initialWaitTime=0; disappearAfterComplete=false; disableColliderWhenFalling=false; spriteRenderer=Player Detected Platform |

## Stage/Stage 12.prefab

| 경로 | 스크립트 | 위치 | 값 |
|---|---|---|---|
| Stage 12/MoveTraps/Move Trap (7) | MovingTrap | (20.5, 11.6) | knockbackDir=(0, 0); endPosition=(7.17, 0); speed=7; isRotate=true; rotationSpeed=1000 |
| Stage 12/MoveTraps/Move Trap (8) | MovingTrap | (11.5, 16.5) | knockbackDir=(0, 0); endPosition=(7.17, 0); speed=8; isRotate=true; rotationSpeed=1000 |
| Stage 12/MoveTraps/Move Trap (1) | MovingTrap | (47.5, 24.2) | knockbackDir=(0, 0); endPosition=(7, 0); speed=8; isRotate=true; rotationSpeed=1000 |
| Stage 12/MoveTraps/Move Trap (10) | MovingTrap | (57.5, 22.5) | knockbackDir=(0, 0); endPosition=(8, 0); speed=8; isRotate=true; rotationSpeed=1000 |
| Stage 12/MoveTraps/Move Trap (11) | MovingTrap | (57.5, 17.5) | knockbackDir=(0, 0); endPosition=(8, 0); speed=2.5; isRotate=true; rotationSpeed=1000 |
| Stage 12/MoveTraps/Move Trap (9) | MovingTrap | (47.5, 17.3) | knockbackDir=(0, 0); endPosition=(7, 0); speed=10; isRotate=true; rotationSpeed=1000 |
| Stage 12/MoveTraps/Move Trap (2) | MovingTrap | (56.5, 12.5) | knockbackDir=(0, 0); endPosition=(9, 0); speed=5; isRotate=true; rotationSpeed=1000 |
| Stage 12/MoveTraps/Move Trap (3) | MovingTrap | (57.5, 3.5) | knockbackDir=(0, 0); endPosition=(8, 0); speed=1.5; isRotate=true; rotationSpeed=1000 |
| Stage 12/MoveTraps/Move Trap (4) | MovingTrap | (66.5, -1.5) | knockbackDir=(0, 0); endPosition=(10, 0); speed=9; isRotate=true; rotationSpeed=1000 |
| Stage 12/MoveTraps/Move Trap (5) | MovingTrap | (76.5, -1.5) | knockbackDir=(0, 0); endPosition=(10, 0); speed=9; isRotate=true; rotationSpeed=1000 |
| Stage 12/MoveTraps/Move Trap (6) | MovingTrap | (86.5, -1.5) | knockbackDir=(0, 0); endPosition=(10, 0); speed=9; isRotate=true; rotationSpeed=1000 |
| Stage 12/Grid/ResetTile | RespawnHandler | (0, 0) | targetLayers=mask2048; resetPoint=RespawnPoint (1); onlyBoxReset=false |
| Stage 12/Grid/ResetTile (1) | RespawnHandler | (0, 0) | targetLayers=mask2048; resetPoint=RespawnPoint (2); onlyBoxReset=false |
| Stage 12/Grid/BoxResetTile | RespawnHandler | (3, 1) | targetLayers=mask2048; resetPoint=RespawnPoint (0); onlyBoxReset=true |
| Stage 12/Grid/BoxResetTile (1) | RespawnHandler | (3, 1) | targetLayers=mask2048; resetPoint=RespawnPoint (2); onlyBoxReset=true |
| Stage 12/LagerBars/LagerBar_0 | PlatformController | (14.5, 0) | offsetApply=false; collisionMask=mask0; previewColor=FF0000FF; passengerMask=mask0; localWaypoints=[(0, 0, 0), (0, 6, 0)]; speed=0.3; cyclic=true; isRepeat=false; originalPositionDelay=0; waitTime=0; easeAmount=0.2; isPassengerTransportEnabled=false |
| Stage 12/LagerBars/LagerBar_0 (1) | PlatformController | (14.5, 13) | offsetApply=false; collisionMask=mask0; previewColor=FF0000FF; passengerMask=mask0; localWaypoints=[(0, 0, 0), (0, -4.5, 0)]; speed=0.4; cyclic=true; isRepeat=false; originalPositionDelay=0; waitTime=0; easeAmount=0.2; isPassengerTransportEnabled=false |
| Stage 12/Door/PlayerDoorSwitch (2) | DoorSwitch | (40.5, 11.63) | offsetApply=false; collisionMask=mask0; onSwitchSprite=PlayerSwitch_1; offSwitchSprite=PlayerSwitch_0; activationLayer=mask2048; door=Door |
| Stage 12/Door (1)/PlayerDoorSwitch (2) | DoorSwitch | (100.5, -0.5) | offsetApply=false; collisionMask=mask0; onSwitchSprite=Switch_1; offSwitchSprite=Switch_0; activationLayer=mask256; door=Door (1) |
| Stage 12/BounceBalls/bouncy ball | MovingTrap | (46.5, 27.69) | knockbackDir=(0, 0); endPosition=(9, 0); speed=12; isRotate=true; rotationSpeed=180 |
| Stage 12/BounceBalls/bouncy ball (4) | MovingTrap | (57.2, 25) | knockbackDir=(0, 0); endPosition=(9, 0); speed=10; isRotate=true; rotationSpeed=180 |
| Stage 12/BounceBalls/bouncy ball (1) | MovingTrap | (55.5, 20.66) | knockbackDir=(0, 0); endPosition=(-9, 0); speed=4; isRotate=true; rotationSpeed=180 |
| Stage 12/BounceBalls/bouncy ball (2) | MovingTrap | (66, 15.3) | knockbackDir=(0, 0); endPosition=(-9, 0); speed=6.7; isRotate=true; rotationSpeed=180 |
| Stage 12/OnOffManager | OnOffManager | (0, 0) | switches=[OnOff Switch, OnOff Switch (3), OnOff Switch (5), OnOff Switch (7), OnOff Switch (2), OnOff Switch (4), OnOff Switch (6), OnOff Switch (1)]; conveyorBelts=[]; activeSwitchGround=OnSwitchGround; inactiveSwitchGround=OffSwitchGround; autoToggleEnabled=false; autoToggleInterval=3 |
| Stage 12/OnOffManager/OnOff Switch | OnOffSwitch | (63.5, 31.5) | offsetApply=false; collisionMask=mask2048; onSwitchSprite=OnOff_0; offSwitchSprite=OnOff_1; onOffManager=OnOffManager; toggleCooldown=0.5 |
| Stage 12/OnOffManager/OnOff Switch (3) | OnOffSwitch | (63.5, 23.5) | offsetApply=false; collisionMask=mask2048; onSwitchSprite=OnOff_0; offSwitchSprite=OnOff_1; onOffManager=OnOffManager; toggleCooldown=0.5 |
| Stage 12/OnOffManager/OnOff Switch (5) | OnOffSwitch | (63.5, 15.5) | offsetApply=false; collisionMask=mask2048; onSwitchSprite=OnOff_0; offSwitchSprite=OnOff_1; onOffManager=OnOffManager; toggleCooldown=0.5 |
| Stage 12/OnOffManager/OnOff Switch (7) | OnOffSwitch | (63.5, 7.5) | offsetApply=false; collisionMask=mask2048; onSwitchSprite=OnOff_0; offSwitchSprite=OnOff_1; onOffManager=OnOffManager; toggleCooldown=0.5 |
| Stage 12/OnOffManager/OnOff Switch (2) | OnOffSwitch | (59.5, 27.5) | offsetApply=false; collisionMask=mask2048; onSwitchSprite=OnOff_0; offSwitchSprite=OnOff_1; onOffManager=OnOffManager; toggleCooldown=0.5 |
| Stage 12/OnOffManager/OnOff Switch (4) | OnOffSwitch | (59.5, 19.5) | offsetApply=false; collisionMask=mask2048; onSwitchSprite=OnOff_0; offSwitchSprite=OnOff_1; onOffManager=OnOffManager; toggleCooldown=0.5 |
| Stage 12/OnOffManager/OnOff Switch (6) | OnOffSwitch | (59.5, 11.5) | offsetApply=false; collisionMask=mask2048; onSwitchSprite=OnOff_0; offSwitchSprite=OnOff_1; onOffManager=OnOffManager; toggleCooldown=0.5 |
| Stage 12/OnOffManager/OnOff Switch (1) | OnOffSwitch | (54.5, 1.5) | offsetApply=false; collisionMask=mask2048; onSwitchSprite=OnOff_0; offSwitchSprite=OnOff_1; onOffManager=OnOffManager; toggleCooldown=0.5 |
| Stage 12/Box Switch Prefab/Box Switch Set | ObjSummonSwitch | (-2, -1.5) | summonLocation=Box Respawn; cooldownTime=5; targetObjTrans=Box; defaultSprite=Switch_0; pressedSprite=Switch_1 |
| Stage 12/Box Switch Prefab/Box | PickupObj | (27.5, 18) | offsetApply=false; collisionMask=mask3520; maxSlopeAngle=45; gravity=9.8; velocity=(0, 0); wallSlideSpeedMax=3 |

## Stage/Stage 13.prefab

| 경로 | 스크립트 | 위치 | 값 |
|---|---|---|---|
| Stage 13/OnOffManager | OnOffManager | (0, 0) | switches=[OnOff Switch, OnOff Switch (1)]; conveyorBelts=[]; activeSwitchGround=OnSwitchGround; inactiveSwitchGround=OffSwitchGround; autoToggleEnabled=false; autoToggleInterval=3 |
| Stage 13/OnOffManager/OnOff Switch | OnOffSwitch | (-19.5, 26.5) | offsetApply=false; collisionMask=mask2048; onSwitchSprite=OnOff_0; offSwitchSprite=OnOff_1; onOffManager=OnOffManager; toggleCooldown=0.5 |
| Stage 13/OnOffManager/OnOff Switch (1) | OnOffSwitch | (19.5, 42.5) | offsetApply=false; collisionMask=mask2048; onSwitchSprite=OnOff_0; offSwitchSprite=OnOff_1; onOffManager=OnOffManager; toggleCooldown=0.5 |
| Stage 13/Fall Playtforms/Fall Platforms | PlayerDetectPlatform | (0, -1) | offsetApply=false; collisionMask=mask2304; previewColor=FF0000FF; passengerMask=mask2048; localWaypoints=[(0, 0, 0), (0, -5, 0)]; speed=1; cyclic=false; isRepeat=false; originalPositionDelay=0.5; waitTime=0.3; easeAmount=0.2; isPassengerTransportEnabled=true; initialWaitTime=0.5; disappearAfterComplete=false; disableColliderWhenFalling=true; spriteRenderer=Sprite |
| Stage 13/Fall Playtforms/Fall Platforms (1) | PlayerDetectPlatform | (5, 1) | offsetApply=false; collisionMask=mask2304; previewColor=FF0000FF; passengerMask=mask2048; localWaypoints=[(0, 0, 0), (0, -5, 0)]; speed=1; cyclic=false; isRepeat=false; originalPositionDelay=0.5; waitTime=0.3; easeAmount=0.2; isPassengerTransportEnabled=true; initialWaitTime=0.5; disappearAfterComplete=false; disableColliderWhenFalling=true; spriteRenderer=Sprite |
| Stage 13/Fall Playtforms/Fall Platforms (2) | PlayerDetectPlatform | (10, 3) | offsetApply=false; collisionMask=mask2304; previewColor=FF0000FF; passengerMask=mask2048; localWaypoints=[(0, 0, 0), (0, -5, 0)]; speed=1; cyclic=false; isRepeat=false; originalPositionDelay=0.5; waitTime=0.3; easeAmount=0.2; isPassengerTransportEnabled=true; initialWaitTime=0.5; disappearAfterComplete=false; disableColliderWhenFalling=true; spriteRenderer=Sprite |
| Stage 13/Fall Playtforms/Fall Platforms (3) | PlayerDetectPlatform | (15, 5) | offsetApply=false; collisionMask=mask2304; previewColor=FF0000FF; passengerMask=mask2048; localWaypoints=[(0, 0, 0), (0, -5, 0)]; speed=1; cyclic=false; isRepeat=false; originalPositionDelay=0.5; waitTime=0.3; easeAmount=0.2; isPassengerTransportEnabled=true; initialWaitTime=0.5; disappearAfterComplete=false; disableColliderWhenFalling=true; spriteRenderer=Sprite |
| Stage 13/Fall Playtforms/Fall Platforms (4) | PlayerDetectPlatform | (20, 7) | offsetApply=false; collisionMask=mask2304; previewColor=FF0000FF; passengerMask=mask2048; localWaypoints=[(0, 0, 0), (0, -5, 0)]; speed=1; cyclic=false; isRepeat=false; originalPositionDelay=0.5; waitTime=0.3; easeAmount=0.2; isPassengerTransportEnabled=true; initialWaitTime=0.5; disappearAfterComplete=false; disableColliderWhenFalling=true; spriteRenderer=Sprite |
| Stage 13/Fall Playtforms/Fall Platforms (5) | PlayerDetectPlatform | (20, 8.5) | offsetApply=false; collisionMask=mask2304; previewColor=FF0000FF; passengerMask=mask2048; localWaypoints=[(0, 0, 0), (0, -5, 0)]; speed=1; cyclic=false; isRepeat=false; originalPositionDelay=0.5; waitTime=0.3; easeAmount=0.2; isPassengerTransportEnabled=true; initialWaitTime=0.5; disappearAfterComplete=false; disableColliderWhenFalling=true; spriteRenderer=Sprite |
| Stage 13/Fall Playtforms/Fall Platforms (6) | PlayerDetectPlatform | (15, 10.5) | offsetApply=false; collisionMask=mask2304; previewColor=FF0000FF; passengerMask=mask2048; localWaypoints=[(0, 0, 0), (0, -5, 0)]; speed=1; cyclic=false; isRepeat=false; originalPositionDelay=0.5; waitTime=0.3; easeAmount=0.2; isPassengerTransportEnabled=true; initialWaitTime=0.5; disappearAfterComplete=false; disableColliderWhenFalling=true; spriteRenderer=Sprite |
| Stage 13/Fall Playtforms/Fall Platforms (7) | PlayerDetectPlatform | (10, 12.5) | offsetApply=false; collisionMask=mask2304; previewColor=FF0000FF; passengerMask=mask2048; localWaypoints=[(0, 0, 0), (0, -5, 0)]; speed=1; cyclic=false; isRepeat=false; originalPositionDelay=0.5; waitTime=0.3; easeAmount=0.2; isPassengerTransportEnabled=true; initialWaitTime=0.5; disappearAfterComplete=false; disableColliderWhenFalling=true; spriteRenderer=Sprite |
| Stage 13/Fall Playtforms/Fall Platforms (8) | PlayerDetectPlatform | (5, 14.5) | offsetApply=false; collisionMask=mask2304; previewColor=FF0000FF; passengerMask=mask2048; localWaypoints=[(0, 0, 0), (0, -5, 0)]; speed=1; cyclic=false; isRepeat=false; originalPositionDelay=0.5; waitTime=0.3; easeAmount=0.2; isPassengerTransportEnabled=true; initialWaitTime=0.5; disappearAfterComplete=false; disableColliderWhenFalling=true; spriteRenderer=Sprite |
| Stage 13/Fall Playtforms/Fall Platforms (9) | PlayerDetectPlatform | (0, 16.5) | offsetApply=false; collisionMask=mask2304; previewColor=FF0000FF; passengerMask=mask2048; localWaypoints=[(0, 0, 0), (0, -5, 0)]; speed=1; cyclic=false; isRepeat=false; originalPositionDelay=0.5; waitTime=0.3; easeAmount=0.2; isPassengerTransportEnabled=true; initialWaitTime=0.5; disappearAfterComplete=false; disableColliderWhenFalling=true; spriteRenderer=Sprite |
| Stage 13/Fall Playtforms/Fall Platforms (10) | PlayerDetectPlatform | (-5, 18.5) | offsetApply=false; collisionMask=mask2304; previewColor=FF0000FF; passengerMask=mask2048; localWaypoints=[(0, 0, 0), (0, -5, 0)]; speed=1; cyclic=false; isRepeat=false; originalPositionDelay=0.5; waitTime=0.3; easeAmount=0.2; isPassengerTransportEnabled=true; initialWaitTime=0.5; disappearAfterComplete=false; disableColliderWhenFalling=true; spriteRenderer=Sprite |
| Stage 13/Fall Playtforms/Fall Platforms (11) | PlayerDetectPlatform | (-10, 20.5) | offsetApply=false; collisionMask=mask2304; previewColor=FF0000FF; passengerMask=mask2048; localWaypoints=[(0, 0, 0), (0, -5, 0)]; speed=1; cyclic=false; isRepeat=false; originalPositionDelay=0.5; waitTime=0.3; easeAmount=0.2; isPassengerTransportEnabled=true; initialWaitTime=0.5; disappearAfterComplete=false; disableColliderWhenFalling=true; spriteRenderer=Sprite |
| Stage 13/Fall Playtforms/Fall Platforms (12) | PlayerDetectPlatform | (-15, 22.5) | offsetApply=false; collisionMask=mask2304; previewColor=FF0000FF; passengerMask=mask2048; localWaypoints=[(0, 0, 0), (0, -5, 0)]; speed=1; cyclic=false; isRepeat=false; originalPositionDelay=0.5; waitTime=0.3; easeAmount=0.2; isPassengerTransportEnabled=true; initialWaitTime=0.5; disappearAfterComplete=false; disableColliderWhenFalling=true; spriteRenderer=Sprite |
| Stage 13/Fall Playtforms/Fall Platforms (13) | PlayerDetectPlatform | (-15, 25.5) | offsetApply=false; collisionMask=mask2304; previewColor=FF0000FF; passengerMask=mask2048; localWaypoints=[(0, 0, 0), (0, -5, 0)]; speed=1; cyclic=false; isRepeat=false; originalPositionDelay=0.5; waitTime=0.3; easeAmount=0.2; isPassengerTransportEnabled=true; initialWaitTime=0.5; disappearAfterComplete=false; disableColliderWhenFalling=true; spriteRenderer=Sprite |
| Stage 13/Fall Playtforms/Fall Platforms (14) | PlayerDetectPlatform | (-10, 27.5) | offsetApply=false; collisionMask=mask2304; previewColor=FF0000FF; passengerMask=mask2048; localWaypoints=[(0, 0, 0), (0, -5, 0)]; speed=1; cyclic=false; isRepeat=false; originalPositionDelay=0.5; waitTime=0.3; easeAmount=0.2; isPassengerTransportEnabled=true; initialWaitTime=0.5; disappearAfterComplete=false; disableColliderWhenFalling=true; spriteRenderer=Sprite |
| Stage 13/Fall Playtforms/Fall Platforms (15) | PlayerDetectPlatform | (-5, 29.5) | offsetApply=false; collisionMask=mask2304; previewColor=FF0000FF; passengerMask=mask2048; localWaypoints=[(0, 0, 0), (0, -5, 0)]; speed=1; cyclic=false; isRepeat=false; originalPositionDelay=0.5; waitTime=0.3; easeAmount=0.2; isPassengerTransportEnabled=true; initialWaitTime=0.5; disappearAfterComplete=false; disableColliderWhenFalling=true; spriteRenderer=Sprite |
| Stage 13/Fall Playtforms/Fall Platforms (16) | PlayerDetectPlatform | (0, 31.5) | offsetApply=false; collisionMask=mask2304; previewColor=FF0000FF; passengerMask=mask2048; localWaypoints=[(0, 0, 0), (0, -5, 0)]; speed=1; cyclic=false; isRepeat=false; originalPositionDelay=0.5; waitTime=0.3; easeAmount=0.2; isPassengerTransportEnabled=true; initialWaitTime=0.5; disappearAfterComplete=false; disableColliderWhenFalling=true; spriteRenderer=Sprite |
| Stage 13/Fall Playtforms/Fall Platforms (17) | PlayerDetectPlatform | (5, 33.5) | offsetApply=false; collisionMask=mask2304; previewColor=FF0000FF; passengerMask=mask2048; localWaypoints=[(0, 0, 0), (0, -5, 0)]; speed=1; cyclic=false; isRepeat=false; originalPositionDelay=0.5; waitTime=0.3; easeAmount=0.2; isPassengerTransportEnabled=true; initialWaitTime=0.5; disappearAfterComplete=false; disableColliderWhenFalling=true; spriteRenderer=Sprite |
| Stage 13/Fall Playtforms/Fall Platforms (18) | PlayerDetectPlatform | (10, 35.5) | offsetApply=false; collisionMask=mask2304; previewColor=FF0000FF; passengerMask=mask2048; localWaypoints=[(0, 0, 0), (0, -5, 0)]; speed=1; cyclic=false; isRepeat=false; originalPositionDelay=0.5; waitTime=0.3; easeAmount=0.2; isPassengerTransportEnabled=true; initialWaitTime=0.5; disappearAfterComplete=false; disableColliderWhenFalling=true; spriteRenderer=Sprite |
| Stage 13/Fall Playtforms/Fall Platforms (19) | PlayerDetectPlatform | (15, 37.5) | offsetApply=false; collisionMask=mask2304; previewColor=FF0000FF; passengerMask=mask2048; localWaypoints=[(0, 0, 0), (0, -5, 0)]; speed=1; cyclic=false; isRepeat=false; originalPositionDelay=0.5; waitTime=0.3; easeAmount=0.2; isPassengerTransportEnabled=true; initialWaitTime=0.5; disappearAfterComplete=false; disableColliderWhenFalling=true; spriteRenderer=Sprite |

## Stage/Stage 14.prefab

| 경로 | 스크립트 | 위치 | 값 |
|---|---|---|---|
| Stage 14/MoveTraps/Move Trap | MovingTrap | (0.5, 13.3) | knockbackDir=(0, 0); endPosition=(-18, 0); speed=7; isRotate=true; rotationSpeed=1000 |
| Stage 14/MoveTraps/Move Trap (1) | MovingTrap | (2.5, 17.39) | knockbackDir=(0, 0); endPosition=(-19.5, 0); speed=7; isRotate=true; rotationSpeed=1000 |
| Stage 14/MoveTraps/Move Trap (2) | MovingTrap | (2.5, 17.39) | knockbackDir=(0, 0); endPosition=(14.5, 0); speed=7; isRotate=true; rotationSpeed=1000 |
| Stage 14/MoveTraps/Move Trap (3) | MovingTrap | (11.5, 22.5) | knockbackDir=(0, 0); endPosition=(5, 0); speed=3; isRotate=true; rotationSpeed=1000 |
| Stage 14/MoveTraps/Move Trap (4) | MovingTrap | (6.5, 24) | knockbackDir=(0, 0); endPosition=(5, 0); speed=3; isRotate=true; rotationSpeed=1000 |
| Stage 14/MoveTraps/Move Trap (5) | MovingTrap | (6, 24) | knockbackDir=(0, 0); endPosition=(-30, 9.5); speed=5; isRotate=true; rotationSpeed=1000 |
| Stage 14/OnOffManager | OnOffManager | (0, 0) | switches=[OnOff Switch, OnOff Switch (1), OnOff Switch (2), OnOff Switch (3)]; conveyorBelts=[]; activeSwitchGround=OnSwitchGround; inactiveSwitchGround=OffSwitchGround; autoToggleEnabled=false; autoToggleInterval=3 |
| Stage 14/OnOffManager/OnOff Switch | OnOffSwitch | (-10, 3) | offsetApply=false; collisionMask=mask2048; onSwitchSprite=OnOff_0; offSwitchSprite=OnOff_1; onOffManager=OnOffManager; toggleCooldown=0.5 |
| Stage 14/OnOffManager/OnOff Switch (1) | OnOffSwitch | (19.5, 6.5) | offsetApply=false; collisionMask=mask2048; onSwitchSprite=OnOff_0; offSwitchSprite=OnOff_1; onOffManager=OnOffManager; toggleCooldown=0.5 |
| Stage 14/OnOffManager/OnOff Switch (2) | OnOffSwitch | (2.5, 14.5) | offsetApply=false; collisionMask=mask2048; onSwitchSprite=OnOff_0; offSwitchSprite=OnOff_1; onOffManager=OnOffManager; toggleCooldown=0.5 |
| Stage 14/OnOffManager/OnOff Switch (3) | OnOffSwitch | (-19.5, 18) | offsetApply=false; collisionMask=mask2048; onSwitchSprite=OnOff_0; offSwitchSprite=OnOff_1; onOffManager=OnOffManager; toggleCooldown=0.5 |
| Stage 14/Fall Playtforms/Fall Platforms | PlayerDetectPlatform | (0, -2) | offsetApply=false; collisionMask=mask2304; previewColor=FF0000FF; passengerMask=mask2048; localWaypoints=[(0, 0, 0), (0, -5, 0)]; speed=1; cyclic=false; isRepeat=false; originalPositionDelay=0.5; waitTime=0.3; easeAmount=0.2; isPassengerTransportEnabled=true; initialWaitTime=0.5; disappearAfterComplete=false; disableColliderWhenFalling=true; spriteRenderer=Sprite |
| Stage 14/Fall Playtforms/Fall Platforms (1) | PlayerDetectPlatform | (-5, -0.5) | offsetApply=false; collisionMask=mask2304; previewColor=FF0000FF; passengerMask=mask2048; localWaypoints=[(0, 0, 0), (0, -5, 0)]; speed=1; cyclic=false; isRepeat=false; originalPositionDelay=0.5; waitTime=0.3; easeAmount=0.2; isPassengerTransportEnabled=true; initialWaitTime=0.5; disappearAfterComplete=false; disableColliderWhenFalling=true; spriteRenderer=Sprite |
| Stage 14/Fall Playtforms/Fall Platforms (2) | PlayerDetectPlatform | (5, -0.5) | offsetApply=false; collisionMask=mask2304; previewColor=FF0000FF; passengerMask=mask2048; localWaypoints=[(0, 0, 0), (0, -5, 0)]; speed=1; cyclic=false; isRepeat=false; originalPositionDelay=0.5; waitTime=0.3; easeAmount=0.2; isPassengerTransportEnabled=true; initialWaitTime=0.5; disappearAfterComplete=false; disableColliderWhenFalling=true; spriteRenderer=Sprite |
| Stage 14/Fall Playtforms/Fall Platforms (3) | PlayerDetectPlatform | (-10, 1) | offsetApply=false; collisionMask=mask2304; previewColor=FF0000FF; passengerMask=mask2048; localWaypoints=[(0, 0, 0), (0, -5, 0)]; speed=1; cyclic=false; isRepeat=false; originalPositionDelay=0.5; waitTime=0.3; easeAmount=0.2; isPassengerTransportEnabled=true; initialWaitTime=0.5; disappearAfterComplete=false; disableColliderWhenFalling=true; spriteRenderer=Sprite |
| Stage 14/Fall Playtforms/Fall Platforms (4) | PlayerDetectPlatform | (10, 1) | offsetApply=false; collisionMask=mask2304; previewColor=FF0000FF; passengerMask=mask2048; localWaypoints=[(0, 0, 0), (0, -5, 0)]; speed=1; cyclic=false; isRepeat=false; originalPositionDelay=0.5; waitTime=0.3; easeAmount=0.2; isPassengerTransportEnabled=true; initialWaitTime=0.5; disappearAfterComplete=false; disableColliderWhenFalling=true; spriteRenderer=Sprite |
| Stage 14/Fall Playtforms/Fall Platforms (5) | PlayerDetectPlatform | (15, 2.5) | offsetApply=false; collisionMask=mask2304; previewColor=FF0000FF; passengerMask=mask2048; localWaypoints=[(0, 0, 0), (0, -5, 0)]; speed=1; cyclic=false; isRepeat=false; originalPositionDelay=0.5; waitTime=0.3; easeAmount=0.2; isPassengerTransportEnabled=true; initialWaitTime=0.5; disappearAfterComplete=false; disableColliderWhenFalling=true; spriteRenderer=Sprite |
| Stage 14/Fall Playtforms/Fall Platforms (6) | PlayerDetectPlatform | (15, 5) | offsetApply=false; collisionMask=mask2304; previewColor=FF0000FF; passengerMask=mask2048; localWaypoints=[(0, 0, 0), (0, -5, 0)]; speed=1; cyclic=false; isRepeat=false; originalPositionDelay=0.5; waitTime=0.3; easeAmount=0.2; isPassengerTransportEnabled=true; initialWaitTime=0.5; disappearAfterComplete=false; disableColliderWhenFalling=true; spriteRenderer=Sprite |
| Stage 14/Fall Playtforms/Fall Platforms (7) | PlayerDetectPlatform | (10, 7) | offsetApply=false; collisionMask=mask2304; previewColor=FF0000FF; passengerMask=mask2048; localWaypoints=[(0, 0, 0), (0, -5, 0)]; speed=1; cyclic=false; isRepeat=false; originalPositionDelay=0.5; waitTime=0.3; easeAmount=0.2; isPassengerTransportEnabled=true; initialWaitTime=0.5; disappearAfterComplete=false; disableColliderWhenFalling=true; spriteRenderer=Sprite |
| Stage 14/Fall Playtforms/Fall Platforms (8) | PlayerDetectPlatform | (-15, 17) | offsetApply=false; collisionMask=mask2304; previewColor=FF0000FF; passengerMask=mask2048; localWaypoints=[(0, 0, 0), (0, -5, 0)]; speed=1; cyclic=false; isRepeat=false; originalPositionDelay=0.5; waitTime=0.3; easeAmount=0.2; isPassengerTransportEnabled=true; initialWaitTime=0.5; disappearAfterComplete=false; disableColliderWhenFalling=true; spriteRenderer=Sprite |
| Stage 14/Fall Playtforms/Fall Platforms (9) | PlayerDetectPlatform | (-10, 17) | offsetApply=false; collisionMask=mask2304; previewColor=FF0000FF; passengerMask=mask2048; localWaypoints=[(0, 0, 0), (0, -5, 0)]; speed=1; cyclic=false; isRepeat=false; originalPositionDelay=0.5; waitTime=0.3; easeAmount=0.2; isPassengerTransportEnabled=true; initialWaitTime=0.5; disappearAfterComplete=false; disableColliderWhenFalling=true; spriteRenderer=Sprite |
| Stage 14/Fall Playtforms/Fall Platforms (10) | PlayerDetectPlatform | (-5, 17) | offsetApply=false; collisionMask=mask2304; previewColor=FF0000FF; passengerMask=mask2048; localWaypoints=[(0, 0, 0), (0, -5, 0)]; speed=1; cyclic=false; isRepeat=false; originalPositionDelay=0.5; waitTime=0.3; easeAmount=0.2; isPassengerTransportEnabled=true; initialWaitTime=0.5; disappearAfterComplete=false; disableColliderWhenFalling=true; spriteRenderer=Sprite |
| Stage 14/Fall Playtforms/Fall Platforms (11) | PlayerDetectPlatform | (0, 17) | offsetApply=false; collisionMask=mask2304; previewColor=FF0000FF; passengerMask=mask2048; localWaypoints=[(0, 0, 0), (0, -5, 0)]; speed=1; cyclic=false; isRepeat=false; originalPositionDelay=0.5; waitTime=0.3; easeAmount=0.2; isPassengerTransportEnabled=true; initialWaitTime=0.5; disappearAfterComplete=false; disableColliderWhenFalling=true; spriteRenderer=Sprite |
| Stage 14/Fall Playtforms/Fall Platforms (12) | PlayerDetectPlatform | (5, 17) | offsetApply=false; collisionMask=mask2304; previewColor=FF0000FF; passengerMask=mask2048; localWaypoints=[(0, 0, 0), (0, -5, 0)]; speed=1; cyclic=false; isRepeat=false; originalPositionDelay=0.5; waitTime=0.3; easeAmount=0.2; isPassengerTransportEnabled=true; initialWaitTime=0.5; disappearAfterComplete=false; disableColliderWhenFalling=true; spriteRenderer=Sprite |
| Stage 14/Fall Playtforms/Fall Platforms (13) | PlayerDetectPlatform | (10, 17) | offsetApply=false; collisionMask=mask2304; previewColor=FF0000FF; passengerMask=mask2048; localWaypoints=[(0, 0, 0), (0, -5, 0)]; speed=1; cyclic=false; isRepeat=false; originalPositionDelay=0.5; waitTime=0.3; easeAmount=0.2; isPassengerTransportEnabled=true; initialWaitTime=0.5; disappearAfterComplete=false; disableColliderWhenFalling=true; spriteRenderer=Sprite |
| Stage 14/Fall Playtforms/Fall Platforms (14) | PlayerDetectPlatform | (15, 17) | offsetApply=false; collisionMask=mask2304; previewColor=FF0000FF; passengerMask=mask2048; localWaypoints=[(0, 0, 0), (0, -5, 0)]; speed=1; cyclic=false; isRepeat=false; originalPositionDelay=0.5; waitTime=0.3; easeAmount=0.2; isPassengerTransportEnabled=true; initialWaitTime=0.5; disappearAfterComplete=false; disableColliderWhenFalling=true; spriteRenderer=Sprite |
| Stage 14/Fall Playtforms/Fall Platforms (15) | PlayerDetectPlatform | (14, 22) | offsetApply=false; collisionMask=mask2304; previewColor=FF0000FF; passengerMask=mask2048; localWaypoints=[(0, 0, 0), (0, -5, 0)]; speed=1; cyclic=false; isRepeat=false; originalPositionDelay=0.5; waitTime=0.3; easeAmount=0.2; isPassengerTransportEnabled=true; initialWaitTime=0.5; disappearAfterComplete=false; disableColliderWhenFalling=true; spriteRenderer=Sprite |
| Stage 14/Fall Playtforms/Fall Platforms (16) | PlayerDetectPlatform | (9, 23.5) | offsetApply=false; collisionMask=mask2304; previewColor=FF0000FF; passengerMask=mask2048; localWaypoints=[(0, 0, 0), (0, -5, 0)]; speed=1; cyclic=false; isRepeat=false; originalPositionDelay=0.5; waitTime=0.3; easeAmount=0.2; isPassengerTransportEnabled=true; initialWaitTime=0.5; disappearAfterComplete=false; disableColliderWhenFalling=true; spriteRenderer=Sprite |
| Stage 14/Fall Playtforms/Fall Platforms (17) | PlayerDetectPlatform | (4, 25) | offsetApply=false; collisionMask=mask2304; previewColor=FF0000FF; passengerMask=mask2048; localWaypoints=[(0, 0, 0), (0, -5, 0)]; speed=1; cyclic=false; isRepeat=false; originalPositionDelay=0.5; waitTime=0.3; easeAmount=0.2; isPassengerTransportEnabled=true; initialWaitTime=0.5; disappearAfterComplete=false; disableColliderWhenFalling=true; spriteRenderer=Sprite |
| Stage 14/Fall Playtforms/Fall Platforms (18) | PlayerDetectPlatform | (-1, 26.5) | offsetApply=false; collisionMask=mask2304; previewColor=FF0000FF; passengerMask=mask2048; localWaypoints=[(0, 0, 0), (0, -5, 0)]; speed=1; cyclic=false; isRepeat=false; originalPositionDelay=0.5; waitTime=0.3; easeAmount=0.2; isPassengerTransportEnabled=true; initialWaitTime=0.5; disappearAfterComplete=false; disableColliderWhenFalling=true; spriteRenderer=Sprite |
| Stage 14/Fall Playtforms/Fall Platforms (19) | PlayerDetectPlatform | (-6, 28) | offsetApply=false; collisionMask=mask2304; previewColor=FF0000FF; passengerMask=mask2048; localWaypoints=[(0, 0, 0), (0, -5, 0)]; speed=1; cyclic=false; isRepeat=false; originalPositionDelay=0.5; waitTime=0.3; easeAmount=0.2; isPassengerTransportEnabled=true; initialWaitTime=0.5; disappearAfterComplete=false; disableColliderWhenFalling=true; spriteRenderer=Sprite |
| Stage 14/Fall Playtforms/Fall Platforms (20) | PlayerDetectPlatform | (-11, 29.5) | offsetApply=false; collisionMask=mask2304; previewColor=FF0000FF; passengerMask=mask2048; localWaypoints=[(0, 0, 0), (0, -5, 0)]; speed=1; cyclic=false; isRepeat=false; originalPositionDelay=0.5; waitTime=0.3; easeAmount=0.2; isPassengerTransportEnabled=true; initialWaitTime=0.5; disappearAfterComplete=false; disableColliderWhenFalling=true; spriteRenderer=Sprite |
| Stage 14/Fall Playtforms/Fall Platforms (21) | PlayerDetectPlatform | (-16, 31) | offsetApply=false; collisionMask=mask2304; previewColor=FF0000FF; passengerMask=mask2048; localWaypoints=[(0, 0, 0), (0, -5, 0)]; speed=1; cyclic=false; isRepeat=false; originalPositionDelay=0.5; waitTime=0.3; easeAmount=0.2; isPassengerTransportEnabled=true; initialWaitTime=0.5; disappearAfterComplete=false; disableColliderWhenFalling=true; spriteRenderer=Sprite |

## Stage/Stage 15.prefab

| 경로 | 스크립트 | 위치 | 값 |
|---|---|---|---|
| Stage 15/MoveTraps/Move Trap | MovingTrap | (40, 12.5) | knockbackDir=(0, 0); endPosition=(19, 0); speed=7; isRotate=true; rotationSpeed=1000 |
| Stage 15/MoveTraps/Move Trap (1) | MovingTrap | (59, 12.5) | knockbackDir=(0, 0); endPosition=(19, 0); speed=8; isRotate=true; rotationSpeed=1000 |
| Stage 15/MoveTraps/Move Trap (2) | MovingTrap | (78, 14) | knockbackDir=(0, 0); endPosition=(-19, 0); speed=8; isRotate=true; rotationSpeed=1000 |
| Stage 15/MoveTraps/Move Trap (3) | MovingTrap | (106.5, 12.4) | knockbackDir=(0, 0); endPosition=(7, 0); speed=8; isRotate=true; rotationSpeed=1000 |
| Stage 15/MoveTraps/Move Trap (4) | MovingTrap | (117.5, 18.5) | knockbackDir=(0, 0); endPosition=(25, 0); speed=8; isRotate=true; rotationSpeed=1000 |
| Stage 15/Fall Playtforms/Fall Platforms | PlayerDetectPlatform | (13.5, 8) | offsetApply=false; collisionMask=mask2304; previewColor=FF0000FF; passengerMask=mask2048; localWaypoints=[(0, 0, 0), (0, -5, 0)]; speed=1; cyclic=false; isRepeat=false; originalPositionDelay=0.5; waitTime=0.3; easeAmount=0.2; isPassengerTransportEnabled=true; initialWaitTime=0.5; disappearAfterComplete=false; disableColliderWhenFalling=true; spriteRenderer=Sprite |
| Stage 15/Fall Playtforms/Fall Platforms (1) | PlayerDetectPlatform | (18.5, 8) | offsetApply=false; collisionMask=mask2304; previewColor=FF0000FF; passengerMask=mask2048; localWaypoints=[(0, 0, 0), (0, -5, 0)]; speed=1; cyclic=false; isRepeat=false; originalPositionDelay=0.5; waitTime=0.3; easeAmount=0.2; isPassengerTransportEnabled=true; initialWaitTime=0.5; disappearAfterComplete=false; disableColliderWhenFalling=true; spriteRenderer=Sprite |
| Stage 15/Fall Playtforms/Fall Platforms (2) | PlayerDetectPlatform | (41.5, 11.8) | offsetApply=false; collisionMask=mask2304; previewColor=FF0000FF; passengerMask=mask2048; localWaypoints=[(0, 0, 0), (0, -5, 0)]; speed=1; cyclic=false; isRepeat=false; originalPositionDelay=0.5; waitTime=0.3; easeAmount=0.2; isPassengerTransportEnabled=true; initialWaitTime=0.5; disappearAfterComplete=false; disableColliderWhenFalling=true; spriteRenderer=Sprite |
| Stage 15/Fall Playtforms/Fall Platforms (3) | PlayerDetectPlatform | (48.5, 11.8) | offsetApply=false; collisionMask=mask2304; previewColor=FF0000FF; passengerMask=mask2048; localWaypoints=[(0, 0, 0), (0, -5, 0)]; speed=1; cyclic=false; isRepeat=false; originalPositionDelay=0.5; waitTime=0.3; easeAmount=0.2; isPassengerTransportEnabled=true; initialWaitTime=0.5; disappearAfterComplete=false; disableColliderWhenFalling=true; spriteRenderer=Sprite |
| Stage 15/Fall Playtforms/Fall Platforms (4) | PlayerDetectPlatform | (55.5, 11.8) | offsetApply=false; collisionMask=mask2304; previewColor=FF0000FF; passengerMask=mask2048; localWaypoints=[(0, 0, 0), (0, -5, 0)]; speed=1; cyclic=false; isRepeat=false; originalPositionDelay=0.5; waitTime=0.3; easeAmount=0.2; isPassengerTransportEnabled=true; initialWaitTime=0.5; disappearAfterComplete=false; disableColliderWhenFalling=true; spriteRenderer=Sprite |
| Stage 15/Fall Playtforms/Fall Platforms (5) | PlayerDetectPlatform | (62.5, 11.8) | offsetApply=false; collisionMask=mask2304; previewColor=FF0000FF; passengerMask=mask2048; localWaypoints=[(0, 0, 0), (0, -5, 0)]; speed=1; cyclic=false; isRepeat=false; originalPositionDelay=0.5; waitTime=0.3; easeAmount=0.2; isPassengerTransportEnabled=true; initialWaitTime=0.5; disappearAfterComplete=false; disableColliderWhenFalling=true; spriteRenderer=Sprite |
| Stage 15/Fall Playtforms/Fall Platforms (6) | PlayerDetectPlatform | (69.5, 11.8) | offsetApply=false; collisionMask=mask2304; previewColor=FF0000FF; passengerMask=mask2048; localWaypoints=[(0, 0, 0), (0, -5, 0)]; speed=1; cyclic=false; isRepeat=false; originalPositionDelay=0.5; waitTime=0.3; easeAmount=0.2; isPassengerTransportEnabled=true; initialWaitTime=0.5; disappearAfterComplete=false; disableColliderWhenFalling=true; spriteRenderer=Sprite |
| Stage 15/Fall Playtforms/Fall Platforms (7) | PlayerDetectPlatform | (76.5, 11.8) | offsetApply=false; collisionMask=mask2304; previewColor=FF0000FF; passengerMask=mask2048; localWaypoints=[(0, 0, 0), (0, -5, 0)]; speed=1; cyclic=false; isRepeat=false; originalPositionDelay=0.5; waitTime=0.3; easeAmount=0.2; isPassengerTransportEnabled=true; initialWaitTime=0.5; disappearAfterComplete=false; disableColliderWhenFalling=true; spriteRenderer=Sprite |
| Stage 15/ResetPoints/Point (0) | SavePoint | (35.5, 12.55) | savePointID=0 |
| Stage 15/ResetPoints/Point (1) | SavePoint | (93.5, 16.6) | savePointID=1 |
| Stage 15/Door Groups/Switch Door (0)/PlayerDoorSwitch (2) | DoorSwitch | (82.5, 9.5) | offsetApply=false; collisionMask=mask0; onSwitchSprite=PlayerSwitch_1; offSwitchSprite=PlayerSwitch_0; activationLayer=mask2048; door=Door |
| Stage 15/Door Groups/Switch Door (1)/PlayerDoorSwitch (2) | DoorSwitch | (85.5, 21.5) | offsetApply=false; collisionMask=mask0; onSwitchSprite=PlayerSwitch_1; offSwitchSprite=PlayerSwitch_0; activationLayer=mask2048; door=Door |
| Stage 15/Cannons/Canon | CannonShooter | (32.38, 4.48) | projectilePrefab=CannonBall; firstShootDelay=0; shootInterval=2; projectileSpeed=10; maxProjectileDistance=20; shootDirection=(-1, 0) |
| Stage 15/Cannons/Canon (1) | CannonShooter | (33.5, 19.5) | projectilePrefab=CannonBall; firstShootDelay=0; shootInterval=2; projectileSpeed=5; maxProjectileDistance=20; shootDirection=(1, 0) |
| Stage 15/Cannons/Canon (2) | CannonShooter | (33.5, 20.5) | projectilePrefab=CannonBall; firstShootDelay=0; shootInterval=2; projectileSpeed=5; maxProjectileDistance=20; shootDirection=(1, 0) |
| Stage 15/Cannons/Canon (3) | CannonShooter | (33.5, 21.5) | projectilePrefab=CannonBall; firstShootDelay=0; shootInterval=2; projectileSpeed=5; maxProjectileDistance=20; shootDirection=(1, 0) |
| Stage 15/Cannons/Canon (4) | CannonShooter | (33.5, 22.5) | projectilePrefab=CannonBall; firstShootDelay=0; shootInterval=2; projectileSpeed=5; maxProjectileDistance=20; shootDirection=(1, 0) |
| Stage 15/Cannons/Canon (5) | CannonShooter | (33.5, 23.5) | projectilePrefab=CannonBall; firstShootDelay=0; shootInterval=2; projectileSpeed=5; maxProjectileDistance=20; shootDirection=(1, 0) |
| Stage 15/Cannons/Canon (6) | CannonShooter | (33.5, 24.5) | projectilePrefab=CannonBall; firstShootDelay=0; shootInterval=2; projectileSpeed=5; maxProjectileDistance=20; shootDirection=(1, 0) |
| Stage 15/Cannons/Canon (7) | CannonShooter | (33.5, 25.5) | projectilePrefab=CannonBall; firstShootDelay=0; shootInterval=2; projectileSpeed=5; maxProjectileDistance=20; shootDirection=(1, 0) |
| Stage 15/MovePlatforms/PlayerDetect Platfroms/Platform | PlayerDetectPlatform | (99.5, 16.5) | offsetApply=true; collisionMask=mask2048; previewColor=FF0000FF; passengerMask=mask2048; localWaypoints=[(0, 0, 0), (20, 0, 0), (25, 5, 0), (30, 0, 0), (35, 5, 0), (40, 0, 0)]; speed=4; cyclic=false; isRepeat=true; originalPositionDelay=1; waitTime=0.5; easeAmount=0.2; isPassengerTransportEnabled=true; initialWaitTime=0; disappearAfterComplete=false; disableColliderWhenFalling=false; spriteRenderer=Platform |
| Stage 15/MovePlatforms/PlayerDetect Platfroms/Platform Reset | DetectPlatformResetBtn | (0, 0) | offsetApply=false; collisionMask=mask2048; onSwitchSprite=Switch_1; offSwitchSprite=Switch_0; activationLayer=mask2048; detectPlatform=Platform |

## Stage/Stage 16.prefab

| 경로 | 스크립트 | 위치 | 값 |
|---|---|---|---|
| Stage 16/MoveTraps/Move Trap | MovingTrap | (19, 10.5) | knockbackDir=(0, 0); endPosition=(20, 0); speed=9; isRotate=true; rotationSpeed=1000 |
| Stage 16/MoveTraps/Move Trap (1) | MovingTrap | (16, 2.2) | knockbackDir=(0, 0); endPosition=(21, 0); speed=8; isRotate=true; rotationSpeed=1000 |
| Stage 16/MoveTraps/Move Trap (2) | MovingTrap | (78, 14) | knockbackDir=(0, 0); endPosition=(-19, 0); speed=7; isRotate=true; rotationSpeed=1000 |
| Stage 16/MoveTraps/Move Trap (3) | MovingTrap | (59, 16) | knockbackDir=(0, 0); endPosition=(25, 0); speed=8; isRotate=true; rotationSpeed=1000 |
| Stage 16/MoveTraps/Move Trap (4) | MovingTrap | (72, 19) | knockbackDir=(0, 0); endPosition=(10, 0); speed=5; isRotate=true; rotationSpeed=1000 |
| Stage 16/MoveTraps/Move Trap (5) | MovingTrap | (57.7, 3) | knockbackDir=(0, 0); endPosition=(10, 0); speed=5; isRotate=true; rotationSpeed=1000 |
| Stage 16/MoveTraps/Move Trap (6) | MovingTrap | (53.5, 1.2) | knockbackDir=(0, 0); endPosition=(18, 0); speed=5; isRotate=true; rotationSpeed=1000 |
| Stage 16/Save Points/Point (0) | SavePoint | (50.52, 2.51) | savePointID=0 |
| Stage 16/Save Points/Point (1) | SavePoint | (87.5, 20.5) | savePointID=1 |
| Stage 16/Save Points/Point (2) | SavePoint | (87.53, 3.5) | savePointID=1 |
| Stage 16/OnOffManager | OnOffManager | (0, 0) | switches=[OnOff Switch, OnOff Switch (1), OnOff Switch (2), OnOff Switch (3), OnOff Switch (4), OnOff Switch (5)]; conveyorBelts=[]; activeSwitchGround=OnSwitchGround; inactiveSwitchGround=OffSwitchGround; autoToggleEnabled=false; autoToggleInterval=3 |
| Stage 16/OnOffManager/OnOff Switch | OnOffSwitch | (22.5, 12.5) | offsetApply=false; collisionMask=mask2048; onSwitchSprite=OnOff_0; offSwitchSprite=OnOff_1; onOffManager=OnOffManager; toggleCooldown=0.5 |
| Stage 16/OnOffManager/OnOff Switch (1) | OnOffSwitch | (27.5, 12.5) | offsetApply=false; collisionMask=mask2048; onSwitchSprite=OnOff_0; offSwitchSprite=OnOff_1; onOffManager=OnOffManager; toggleCooldown=0.5 |
| Stage 16/OnOffManager/OnOff Switch (2) | OnOffSwitch | (32.5, 12.5) | offsetApply=false; collisionMask=mask2048; onSwitchSprite=OnOff_0; offSwitchSprite=OnOff_1; onOffManager=OnOffManager; toggleCooldown=0.5 |
| Stage 16/OnOffManager/OnOff Switch (3) | OnOffSwitch | (37.5, 12.5) | offsetApply=false; collisionMask=mask2048; onSwitchSprite=OnOff_0; offSwitchSprite=OnOff_1; onOffManager=OnOffManager; toggleCooldown=0.5 |
| Stage 16/OnOffManager/OnOff Switch (4) | OnOffSwitch | (62.5, 5) | offsetApply=false; collisionMask=mask2048; onSwitchSprite=OnOff_0; offSwitchSprite=OnOff_1; onOffManager=OnOffManager; toggleCooldown=0.5 |
| Stage 16/OnOffManager/OnOff Switch (5) | OnOffSwitch | (99, 22) | offsetApply=false; collisionMask=mask2048; onSwitchSprite=OnOff_0; offSwitchSprite=OnOff_1; onOffManager=OnOffManager; toggleCooldown=0.5 |
| Stage 16/MovePlatforms/PlayerDetect Platfroms/Platform | PlayerDetectPlatform | (19, 10) | offsetApply=true; collisionMask=mask2048; previewColor=FF0000FF; passengerMask=mask2048; localWaypoints=[(0, 0, 0), (20, 0, 0)]; speed=4; cyclic=false; isRepeat=true; originalPositionDelay=1; waitTime=0.5; easeAmount=0.2; isPassengerTransportEnabled=true; initialWaitTime=0; disappearAfterComplete=false; disableColliderWhenFalling=false; spriteRenderer=Platform |
| Stage 16/MovePlatforms/PlayerDetect Platfroms/Platform Reset | DetectPlatformResetBtn | (13.49, 8.5) | offsetApply=false; collisionMask=mask2048; onSwitchSprite=Switch_1; offSwitchSprite=Switch_0; activationLayer=mask2048; detectPlatform=Platform |
| Stage 16/Fall Playtforms/Fall Platforms | PlayerDetectPlatform | (16, 2) | offsetApply=false; collisionMask=mask2304; previewColor=FF0000FF; passengerMask=mask2048; localWaypoints=[(0, 0, 0), (0, -5, 0)]; speed=1; cyclic=false; isRepeat=false; originalPositionDelay=0.5; waitTime=1; easeAmount=0.2; isPassengerTransportEnabled=true; initialWaitTime=0.5; disappearAfterComplete=false; disableColliderWhenFalling=true; spriteRenderer=Sprite |
| Stage 16/Fall Playtforms/Fall Platforms (1) | PlayerDetectPlatform | (23, 2) | offsetApply=false; collisionMask=mask2304; previewColor=FF0000FF; passengerMask=mask2048; localWaypoints=[(0, 0, 0), (0, -5, 0)]; speed=1; cyclic=false; isRepeat=false; originalPositionDelay=0.5; waitTime=1; easeAmount=0.2; isPassengerTransportEnabled=true; initialWaitTime=0.5; disappearAfterComplete=false; disableColliderWhenFalling=true; spriteRenderer=Sprite |
| Stage 16/Fall Playtforms/Fall Platforms (2) | PlayerDetectPlatform | (30, 2) | offsetApply=false; collisionMask=mask2304; previewColor=FF0000FF; passengerMask=mask2048; localWaypoints=[(0, 0, 0), (0, -5, 0)]; speed=1; cyclic=false; isRepeat=false; originalPositionDelay=0.5; waitTime=1; easeAmount=0.2; isPassengerTransportEnabled=true; initialWaitTime=0.5; disappearAfterComplete=false; disableColliderWhenFalling=true; spriteRenderer=Sprite |
| Stage 16/Fall Playtforms/Fall Platforms (3) | PlayerDetectPlatform | (37, 2) | offsetApply=false; collisionMask=mask2304; previewColor=FF0000FF; passengerMask=mask2048; localWaypoints=[(0, 0, 0), (0, -5, 0)]; speed=1; cyclic=false; isRepeat=false; originalPositionDelay=0.5; waitTime=1; easeAmount=0.2; isPassengerTransportEnabled=true; initialWaitTime=0.5; disappearAfterComplete=false; disableColliderWhenFalling=true; spriteRenderer=Sprite |
| Stage 16/Fall Playtforms/Fall Platforms (4) | PlayerDetectPlatform | (81, 18) | offsetApply=false; collisionMask=mask2304; previewColor=FF0000FF; passengerMask=mask2048; localWaypoints=[(0, 0, 0), (0, -5, 0)]; speed=1; cyclic=false; isRepeat=false; originalPositionDelay=0.5; waitTime=0.5; easeAmount=0.2; isPassengerTransportEnabled=true; initialWaitTime=0.5; disappearAfterComplete=false; disableColliderWhenFalling=true; spriteRenderer=Sprite |
| Stage 16/Fall Playtforms/Fall Platforms (5) | PlayerDetectPlatform | (57.5, 2.5) | offsetApply=false; collisionMask=mask2304; previewColor=FF0000FF; passengerMask=mask2048; localWaypoints=[(0, 0, 0), (0, -3, 0)]; speed=2; cyclic=false; isRepeat=false; originalPositionDelay=0.3; waitTime=1; easeAmount=0.2; isPassengerTransportEnabled=true; initialWaitTime=0.5; disappearAfterComplete=false; disableColliderWhenFalling=true; spriteRenderer=Sprite |
| Stage 16/Fall Playtforms/Fall Platforms (6) | PlayerDetectPlatform | (62.5, 2.5) | offsetApply=false; collisionMask=mask2304; previewColor=FF0000FF; passengerMask=mask2048; localWaypoints=[(0, 0, 0), (0, -3, 0)]; speed=2; cyclic=false; isRepeat=false; originalPositionDelay=0.3; waitTime=1; easeAmount=0.2; isPassengerTransportEnabled=true; initialWaitTime=0.5; disappearAfterComplete=false; disableColliderWhenFalling=true; spriteRenderer=Sprite |
| Stage 16/Fall Playtforms/Fall Platforms (7) | PlayerDetectPlatform | (67.5, 2.5) | offsetApply=false; collisionMask=mask2304; previewColor=FF0000FF; passengerMask=mask2048; localWaypoints=[(0, 0, 0), (0, -3, 0)]; speed=2; cyclic=false; isRepeat=false; originalPositionDelay=0.3; waitTime=1; easeAmount=0.2; isPassengerTransportEnabled=true; initialWaitTime=0.5; disappearAfterComplete=false; disableColliderWhenFalling=true; spriteRenderer=Sprite |
| Stage 16/Fall Playtforms/Fall Platforms (8) | PlayerDetectPlatform | (57.5, 1) | offsetApply=false; collisionMask=mask2304; previewColor=FF0000FF; passengerMask=mask2048; localWaypoints=[(0, 0, 0), (0, -3, 0)]; speed=2; cyclic=false; isRepeat=false; originalPositionDelay=0.3; waitTime=1; easeAmount=0.2; isPassengerTransportEnabled=true; initialWaitTime=0.5; disappearAfterComplete=false; disableColliderWhenFalling=true; spriteRenderer=Sprite |
| Stage 16/Fall Playtforms/Fall Platforms (9) | PlayerDetectPlatform | (62.5, 1) | offsetApply=false; collisionMask=mask2304; previewColor=FF0000FF; passengerMask=mask2048; localWaypoints=[(0, 0, 0), (0, -3, 0)]; speed=2; cyclic=false; isRepeat=false; originalPositionDelay=0.3; waitTime=1; easeAmount=0.2; isPassengerTransportEnabled=true; initialWaitTime=0.5; disappearAfterComplete=false; disableColliderWhenFalling=true; spriteRenderer=Sprite |
| Stage 16/Fall Playtforms/Fall Platforms (10) | PlayerDetectPlatform | (67.5, 1) | offsetApply=false; collisionMask=mask2304; previewColor=FF0000FF; passengerMask=mask2048; localWaypoints=[(0, 0, 0), (0, -3, 0)]; speed=2; cyclic=false; isRepeat=false; originalPositionDelay=0.3; waitTime=1; easeAmount=0.2; isPassengerTransportEnabled=true; initialWaitTime=0.5; disappearAfterComplete=false; disableColliderWhenFalling=true; spriteRenderer=Sprite |
| Stage 16/Fall Playtforms/Fall Platforms (11) | PlayerDetectPlatform | (94, 19.5) | offsetApply=false; collisionMask=mask2304; previewColor=FF0000FF; passengerMask=mask2048; localWaypoints=[(0, 0, 0), (0, -3, 0)]; speed=2; cyclic=false; isRepeat=false; originalPositionDelay=0.3; waitTime=1; easeAmount=0.2; isPassengerTransportEnabled=true; initialWaitTime=0.5; disappearAfterComplete=false; disableColliderWhenFalling=true; spriteRenderer=Sprite |
| Stage 16/Fall Playtforms/Fall Platforms (12) | PlayerDetectPlatform | (99, 19.5) | offsetApply=false; collisionMask=mask2304; previewColor=FF0000FF; passengerMask=mask2048; localWaypoints=[(0, 0, 0), (0, -3, 0)]; speed=2; cyclic=false; isRepeat=false; originalPositionDelay=0.3; waitTime=1; easeAmount=0.2; isPassengerTransportEnabled=true; initialWaitTime=0.5; disappearAfterComplete=false; disableColliderWhenFalling=true; spriteRenderer=Sprite |
| Stage 16/Fall Playtforms/Fall Platforms (13) | PlayerDetectPlatform | (104, 19.5) | offsetApply=false; collisionMask=mask2304; previewColor=FF0000FF; passengerMask=mask2048; localWaypoints=[(0, 0, 0), (0, -3, 0)]; speed=2; cyclic=false; isRepeat=false; originalPositionDelay=0.3; waitTime=1; easeAmount=0.2; isPassengerTransportEnabled=true; initialWaitTime=0.5; disappearAfterComplete=false; disableColliderWhenFalling=true; spriteRenderer=Sprite |
| Stage 16/Fall Playtforms/Fall Platforms (14) | PlayerDetectPlatform | (94, 18) | offsetApply=false; collisionMask=mask2304; previewColor=FF0000FF; passengerMask=mask2048; localWaypoints=[(0, 0, 0), (0, -3, 0)]; speed=2; cyclic=false; isRepeat=false; originalPositionDelay=0.3; waitTime=1; easeAmount=0.2; isPassengerTransportEnabled=true; initialWaitTime=0.5; disappearAfterComplete=false; disableColliderWhenFalling=true; spriteRenderer=Sprite |
| Stage 16/Fall Playtforms/Fall Platforms (15) | PlayerDetectPlatform | (99, 18) | offsetApply=false; collisionMask=mask2304; previewColor=FF0000FF; passengerMask=mask2048; localWaypoints=[(0, 0, 0), (0, -3, 0)]; speed=2; cyclic=false; isRepeat=false; originalPositionDelay=0.3; waitTime=1; easeAmount=0.2; isPassengerTransportEnabled=true; initialWaitTime=0.5; disappearAfterComplete=false; disableColliderWhenFalling=true; spriteRenderer=Sprite |
| Stage 16/Fall Playtforms/Fall Platforms (16) | PlayerDetectPlatform | (104, 18) | offsetApply=false; collisionMask=mask2304; previewColor=FF0000FF; passengerMask=mask2048; localWaypoints=[(0, 0, 0), (0, -3, 0)]; speed=2; cyclic=false; isRepeat=false; originalPositionDelay=0.3; waitTime=1; easeAmount=0.2; isPassengerTransportEnabled=true; initialWaitTime=0.5; disappearAfterComplete=false; disableColliderWhenFalling=true; spriteRenderer=Sprite |
| Stage 16/Door Groups/Switch Door (0)/PlayerDoorSwitch (2) | DoorSwitch | (46.5, 2.5) | offsetApply=false; collisionMask=mask0; onSwitchSprite=PlayerSwitch_1; offSwitchSprite=PlayerSwitch_0; activationLayer=mask2048; door=Door |
| Stage 16/Door Groups/Switch Door (1)/PlayerDoorSwitch (2) | DoorSwitch | (73.5, 21.5) | offsetApply=false; collisionMask=mask0; onSwitchSprite=PlayerSwitch_1; offSwitchSprite=PlayerSwitch_0; activationLayer=mask2048; door=Door |
| Stage 16/Door Groups/Switch Door (2)/PlayerDoorSwitch (2) | DoorSwitch | (109.5, 7.5) | offsetApply=false; collisionMask=mask0; onSwitchSprite=PlayerSwitch_1; offSwitchSprite=PlayerSwitch_0; activationLayer=mask2048; door=Door |

## Stage/Stage 17.prefab

| 경로 | 스크립트 | 위치 | 값 |
|---|---|---|---|
| Stage 17/RotateOpstacles/RotateObstacle (1) | RotatingObstacle | (108.5, 15.5) | knockbackDir=(-1, 0); isCounterclockwise=false; rotationSpeed=50; random=false; randomRange=0 |
| Stage 17/Save Points/Point (0) | SavePoint | (37.5, 4.6) | savePointID=0 |
| Stage 17/Cannons/Canon | CannonShooter | (33.5, 12.8) | projectilePrefab=CannonBall; firstShootDelay=0; shootInterval=2; projectileSpeed=10; maxProjectileDistance=20; shootDirection=(-1, 0) |
| Stage 17/MoveTraps/Move Trap | MovingTrap | (13.5, 0.5) | knockbackDir=(0, 0); endPosition=(19, 0); speed=7; isRotate=true; rotationSpeed=1000 |
| Stage 17/MoveTraps/Move Trap (3) | MovingTrap | (13.5, 9.5) | knockbackDir=(0, 0); endPosition=(12, 0); speed=7; isRotate=true; rotationSpeed=1000 |
| Stage 17/MoveTraps/Move Trap (1) | MovingTrap | (32.5, 2.5) | knockbackDir=(0, 0); endPosition=(-19, 0); speed=7; isRotate=true; rotationSpeed=1000 |
| Stage 17/MoveTraps/Move Trap (2) | MovingTrap | (29.5, 2.5) | knockbackDir=(0, 0); endPosition=(0, 5); speed=5; isRotate=true; rotationSpeed=1000 |
| Stage 17/MoveTraps/Move Trap (4) | MovingTrap | (60, 5.5) | knockbackDir=(0, 0); endPosition=(5, 0); speed=5; isRotate=true; rotationSpeed=1000 |
| Stage 17/MoveTraps/Move Trap (5) | MovingTrap | (65.5, 13) | knockbackDir=(0, 0); endPosition=(6, 0); speed=5; isRotate=true; rotationSpeed=1000 |
| Stage 17/MoveTraps/Move Trap (6) | MovingTrap | (96, 9.5) | knockbackDir=(0, 0); endPosition=(8, 0); speed=5; isRotate=true; rotationSpeed=1000 |
| Stage 17/Door Groups/Switch Door (0)/PlayerDoorSwitch (2) | DoorSwitch | (35.5, 4.5) | offsetApply=false; collisionMask=mask0; onSwitchSprite=PlayerSwitch_1; offSwitchSprite=PlayerSwitch_0; activationLayer=mask2048; door=Door |
| Stage 17/Door Groups/Switch Door (1)/PlayerDoorSwitch (2) | DoorSwitch | (96.5, 16.5) | offsetApply=false; collisionMask=mask0; onSwitchSprite=PlayerSwitch_1; offSwitchSprite=PlayerSwitch_0; activationLayer=mask2048; door=Door |
| Stage 17/MovePlatforms/PlayerDetect Platfroms/Platform | PlayerDetectPlatform | (15.5, 11.5) | offsetApply=true; collisionMask=mask2048; previewColor=FF0000FF; passengerMask=mask2048; localWaypoints=[(0, 0, 0), (8, 0, 0), (8, -3, 0), (0, -3, 0), (0, 0, 0)]; speed=4; cyclic=false; isRepeat=true; originalPositionDelay=1; waitTime=0.5; easeAmount=0.2; isPassengerTransportEnabled=true; initialWaitTime=0; disappearAfterComplete=false; disableColliderWhenFalling=false; spriteRenderer=Platform |
| Stage 17/MovePlatforms/PlayerDetect Platfroms/Platform Reset | DetectPlatformResetBtn | (12, 14.5) | offsetApply=false; collisionMask=mask2048; onSwitchSprite=Switch_1; offSwitchSprite=Switch_0; activationLayer=mask2048; detectPlatform=Platform |
| Stage 17/MovePlatforms/PlayerDetect Platfroms (1)/Platform | PlayerDetectPlatform | (44.5, 3.5) | offsetApply=true; collisionMask=mask2048; previewColor=FF0000FF; passengerMask=mask2048; localWaypoints=[(0, 0, 0), (18, 0, 0), (18, 5, 0), (55, 5, 0), (80, 11, 0)]; speed=4; cyclic=false; isRepeat=true; originalPositionDelay=1; waitTime=0.5; easeAmount=0.2; isPassengerTransportEnabled=true; initialWaitTime=0; disappearAfterComplete=false; disableColliderWhenFalling=false; spriteRenderer=Platform |
| Stage 17/MovePlatforms/PlayerDetect Platfroms (1)/Platform Reset | DetectPlatformResetBtn | (40.5, 4.5) | offsetApply=false; collisionMask=mask2048; onSwitchSprite=Switch_1; offSwitchSprite=Switch_0; activationLayer=mask2048; detectPlatform=Platform |
| Stage 17/OnOffManager | OnOffManager | (0, 0) | switches=[OnOff Switch, OnOff Switch (1), OnOff Switch (2), OnOff Switch (3)]; conveyorBelts=[]; activeSwitchGround=OnSwitchGround; inactiveSwitchGround=OffSwitchGround; autoToggleEnabled=false; autoToggleInterval=3 |
| Stage 17/OnOffManager/OnOff Switch | OnOffSwitch | (19.78, 13.97) | offsetApply=false; collisionMask=mask2048; onSwitchSprite=OnOff_0; offSwitchSprite=OnOff_1; onOffManager=OnOffManager; toggleCooldown=0.5 |
| Stage 17/OnOffManager/OnOff Switch (1) | OnOffSwitch | (55.5, 10.5) | offsetApply=false; collisionMask=mask2048; onSwitchSprite=OnOff_0; offSwitchSprite=OnOff_1; onOffManager=OnOffManager; toggleCooldown=0.5 |
| Stage 17/OnOffManager/OnOff Switch (2) | OnOffSwitch | (68.5, 15.5) | offsetApply=false; collisionMask=mask2048; onSwitchSprite=OnOff_0; offSwitchSprite=OnOff_1; onOffManager=OnOffManager; toggleCooldown=0.5 |
| Stage 17/OnOffManager/OnOff Switch (3) | OnOffSwitch | (104.5, 18) | offsetApply=false; collisionMask=mask2048; onSwitchSprite=OnOff_0; offSwitchSprite=OnOff_1; onOffManager=OnOffManager; toggleCooldown=0.5 |
| Stage 17/Fall Playtforms/Fall Platforms | PlayerDetectPlatform | (80, 10.5) | offsetApply=false; collisionMask=mask2304; previewColor=FF0000FF; passengerMask=mask2048; localWaypoints=[(0, 0, 0), (0, -5, 0)]; speed=1; cyclic=false; isRepeat=false; originalPositionDelay=0.5; waitTime=0.3; easeAmount=0.2; isPassengerTransportEnabled=true; initialWaitTime=0.5; disappearAfterComplete=false; disableColliderWhenFalling=true; spriteRenderer=Sprite |
| Stage 17/Fall Playtforms/Fall Platforms (1) | PlayerDetectPlatform | (86, 12.5) | offsetApply=false; collisionMask=mask2304; previewColor=FF0000FF; passengerMask=mask2048; localWaypoints=[(0, 0, 0), (0, -5, 0)]; speed=1; cyclic=false; isRepeat=false; originalPositionDelay=0.5; waitTime=0.3; easeAmount=0.2; isPassengerTransportEnabled=true; initialWaitTime=0.5; disappearAfterComplete=false; disableColliderWhenFalling=true; spriteRenderer=Sprite |
| Stage 17/Fall Playtforms/Fall Platforms (2) | PlayerDetectPlatform | (92, 14.5) | offsetApply=false; collisionMask=mask2304; previewColor=FF0000FF; passengerMask=mask2048; localWaypoints=[(0, 0, 0), (0, -5, 0)]; speed=1; cyclic=false; isRepeat=false; originalPositionDelay=0.5; waitTime=0.3; easeAmount=0.2; isPassengerTransportEnabled=true; initialWaitTime=0.5; disappearAfterComplete=false; disableColliderWhenFalling=true; spriteRenderer=Sprite |

## Stage/Stage 18.prefab

| 경로 | 스크립트 | 위치 | 값 |
|---|---|---|---|
| Stage 18/MoveTraps/Move Trap | MovingTrap | (57.5, 1) | knockbackDir=(0, 0); endPosition=(20, 0); speed=7; isRotate=true; rotationSpeed=1000 |
| Stage 18/MoveTraps/Move Trap (1) | MovingTrap | (112.5, 11.5) | knockbackDir=(0, 0); endPosition=(-10, 0); speed=7; isRotate=true; rotationSpeed=1000 |
| Stage 18/MoveTraps/Move Trap (2) | MovingTrap | (112.5, 18.3) | knockbackDir=(0, 0); endPosition=(-10, 3); speed=7; isRotate=true; rotationSpeed=1000 |
| Stage 18/MoveTraps/Move Trap (3) | MovingTrap | (94.5, 18.3) | knockbackDir=(0, 0); endPosition=(8, 5); speed=7; isRotate=true; rotationSpeed=1000 |
| Stage 18/Fall Playtforms/Fall Platforms | PlayerDetectPlatform | (76, -3.5) | offsetApply=false; collisionMask=mask2304; previewColor=FF0000FF; passengerMask=mask2048; localWaypoints=[(0, 0, 0), (0, -5, 0)]; speed=1; cyclic=false; isRepeat=false; originalPositionDelay=0.5; waitTime=0.3; easeAmount=0.2; isPassengerTransportEnabled=true; initialWaitTime=0.5; disappearAfterComplete=false; disableColliderWhenFalling=true; spriteRenderer=Sprite |
| Stage 18/Fall Playtforms/Fall Platforms (1) | PlayerDetectPlatform | (81, -2.5) | offsetApply=false; collisionMask=mask2304; previewColor=FF0000FF; passengerMask=mask2048; localWaypoints=[(0, 0, 0), (0, -5, 0)]; speed=1; cyclic=false; isRepeat=false; originalPositionDelay=0.5; waitTime=0.3; easeAmount=0.2; isPassengerTransportEnabled=true; initialWaitTime=0.5; disappearAfterComplete=false; disableColliderWhenFalling=true; spriteRenderer=Sprite |
| Stage 18/Fall Playtforms/Fall Platforms (2) | PlayerDetectPlatform | (93.02, 6.7) | offsetApply=false; collisionMask=mask2304; previewColor=FF0000FF; passengerMask=mask2048; localWaypoints=[(0, 0, 0), (0, -5, 0)]; speed=1; cyclic=false; isRepeat=false; originalPositionDelay=0.5; waitTime=0.3; easeAmount=0.2; isPassengerTransportEnabled=true; initialWaitTime=0.5; disappearAfterComplete=false; disableColliderWhenFalling=true; spriteRenderer=Sprite |
| Stage 18/Fall Playtforms/Fall Platforms (3) | PlayerDetectPlatform | (99.1, 6.7) | offsetApply=false; collisionMask=mask2304; previewColor=FF0000FF; passengerMask=mask2048; localWaypoints=[(0, 0, 0), (0, -5, 0)]; speed=1; cyclic=false; isRepeat=false; originalPositionDelay=0.5; waitTime=0.3; easeAmount=0.2; isPassengerTransportEnabled=true; initialWaitTime=0.5; disappearAfterComplete=false; disableColliderWhenFalling=true; spriteRenderer=Sprite |
| Stage 18/Fall Playtforms/Fall Platforms (4) | PlayerDetectPlatform | (108.69, 10.75) | offsetApply=false; collisionMask=mask2304; previewColor=FF0000FF; passengerMask=mask2048; localWaypoints=[(0, 0, 0), (0, -5, 0)]; speed=1; cyclic=false; isRepeat=false; originalPositionDelay=0.5; waitTime=0.3; easeAmount=0.2; isPassengerTransportEnabled=true; initialWaitTime=0.5; disappearAfterComplete=false; disableColliderWhenFalling=true; spriteRenderer=Sprite |
| Stage 18/Door Groups/Switch Door (0)/PlayerDoorSwitch (2) | DoorSwitch | (53.55, 1.5) | offsetApply=false; collisionMask=mask0; onSwitchSprite=PlayerSwitch_1; offSwitchSprite=PlayerSwitch_0; activationLayer=mask2048; door=Door |
| Stage 18/Door Groups/Switch Door (1)/PlayerDoorSwitch (2) | DoorSwitch | (83.5, 0.5) | offsetApply=false; collisionMask=mask0; onSwitchSprite=PlayerSwitch_1; offSwitchSprite=PlayerSwitch_0; activationLayer=mask2048; door=Door |
| Stage 18/Cannons/Canon | CannonShooter | (82.5, -1.5) | projectilePrefab=CannonBall; firstShootDelay=0; shootInterval=2; projectileSpeed=10; maxProjectileDistance=20; shootDirection=(-1, 0) |
| Stage 18/Cannons/Canon (1) | CannonShooter | (87.62, 7.53) | projectilePrefab=CannonBall; firstShootDelay=0; shootInterval=2; projectileSpeed=10; maxProjectileDistance=20; shootDirection=(1, 0) |
| Stage 18/OnOffManager | OnOffManager | (0, 0) | switches=[OnOff Switch, OnOff Switch (1), OnOff Switch (2), OnOff Switch (3), OnOff Switch (4), OnOff Switch (5), OnOff Switch (6), OnOff Switch (7)]; conveyorBelts=[Conveyor belt, Conveyor belt (1), Conveyor belt (2), Conveyor belt (3), Conveyor belt (4)]; activeSwitchGround=null; inactiveSwitchGround=null; autoToggleEnabled=false; autoToggleInterval=3 |
| Stage 18/OnOffManager/OnOff Switch | OnOffSwitch | (13.5, 4) | offsetApply=false; collisionMask=mask2048; onSwitchSprite=OnOff_0; offSwitchSprite=OnOff_1; onOffManager=OnOffManager; toggleCooldown=0.5 |
| Stage 18/OnOffManager/OnOff Switch (1) | OnOffSwitch | (56.5, 4) | offsetApply=false; collisionMask=mask2048; onSwitchSprite=OnOff_0; offSwitchSprite=OnOff_1; onOffManager=OnOffManager; toggleCooldown=0.5 |
| Stage 18/OnOffManager/OnOff Switch (2) | OnOffSwitch | (61, 4) | offsetApply=false; collisionMask=mask2048; onSwitchSprite=OnOff_0; offSwitchSprite=OnOff_1; onOffManager=OnOffManager; toggleCooldown=0.5 |
| Stage 18/OnOffManager/OnOff Switch (3) | OnOffSwitch | (64, 4) | offsetApply=false; collisionMask=mask2048; onSwitchSprite=OnOff_0; offSwitchSprite=OnOff_1; onOffManager=OnOffManager; toggleCooldown=0.5 |
| Stage 18/OnOffManager/OnOff Switch (4) | OnOffSwitch | (67, 4) | offsetApply=false; collisionMask=mask2048; onSwitchSprite=OnOff_0; offSwitchSprite=OnOff_1; onOffManager=OnOffManager; toggleCooldown=0.5 |
| Stage 18/OnOffManager/OnOff Switch (5) | OnOffSwitch | (75, 4) | offsetApply=false; collisionMask=mask2048; onSwitchSprite=OnOff_0; offSwitchSprite=OnOff_1; onOffManager=OnOffManager; toggleCooldown=0.5 |
| Stage 18/OnOffManager/OnOff Switch (6) | OnOffSwitch | (60.5, 15.5) | offsetApply=false; collisionMask=mask2048; onSwitchSprite=OnOff_0; offSwitchSprite=OnOff_1; onOffManager=OnOffManager; toggleCooldown=0.5 |
| Stage 18/OnOffManager/OnOff Switch (7) | OnOffSwitch | (99.97, 14.56) | offsetApply=false; collisionMask=mask2048; onSwitchSprite=OnOff_0; offSwitchSprite=OnOff_1; onOffManager=OnOffManager; toggleCooldown=0.5 |
| Stage 18/SavePoints/Point (0) | SavePoint | (45.5, 1.6) | savePointID=0 |
| Stage 18/SavePoints/Point (1) | SavePoint | (85.5, 0.6) | savePointID=0 |
| Stage 18/Belts/Conveyor belt | Conveyor | (17.5, 0.5) | beltAnimators=[Left, Middle, Middle (1), Middle (2), Middle (3), Middle (4), Right] |
| Stage 18/Belts/Conveyor belt (1) | Conveyor | (57.5, 11.74) | beltAnimators=[Left, Middle, Middle (1), Middle (2), Middle (3), Middle (4), Right] |
| Stage 18/Belts/Conveyor belt (2) | Conveyor | (103.5, 6.35) | beltAnimators=[Left, Middle, Middle (1), Right] |
| Stage 18/Belts/Conveyor belt (3) | Conveyor | (101.4, 11.47) | beltAnimators=[Left, Middle, Middle (1), Right] |
| Stage 18/Belts/Conveyor belt (4) | Conveyor | (95.5, 11.47) | beltAnimators=[Left, Middle, Middle (1), Right] |

## Stage/Stage 19.prefab

| 경로 | 스크립트 | 위치 | 값 |
|---|---|---|---|
| Stage 19/Box Switch Prefab/Box Switch Set | ObjSummonSwitch | (17.58, 45.6) | summonLocation=Box Respawn; cooldownTime=5; targetObjTrans=Box; defaultSprite=Switch_0; pressedSprite=Switch_1 |
| Stage 19/Box Switch Prefab/Box | PickupObj | (14.5, 46.5) | offsetApply=false; collisionMask=mask3520; maxSlopeAngle=45; gravity=9.8; velocity=(0, 0); wallSlideSpeedMax=3 |
| Stage 19/Door Groups/Switch Door (0)/PlayerDoorSwitch (2) | DoorSwitch | (14.5, 35.5) | offsetApply=false; collisionMask=mask0; onSwitchSprite=PlayerSwitch_1; offSwitchSprite=PlayerSwitch_0; activationLayer=mask2048; door=Door |
| Stage 19/OnOffManager | OnOffManager | (0, 0) | switches=[OnOff Switch, OnOff Switch (1), OnOff Switch (2), OnOff Switch (3), OnOff Switch (4), OnOff Switch (5), OnOff Switch (6), OnOff Switch (7), OnOff Switch (8), OnOff Switch (9), OnOff Switch (10), OnOff Switch (11)]; conveyorBelts=[Conveyor belt, Conveyor belt (1), Conveyor belt (2), Conveyor belt (3), Conveyor belt (4), Conveyor belt (5), Conveyor belt (6), Conveyor belt (7)]; activeSwitchGround=null; inactiveSwitchGround=null; autoToggleEnabled=false; autoToggleInterval=3 |
| Stage 19/OnOffManager/OnOff Switch | OnOffSwitch | (17, 5.5) | offsetApply=false; collisionMask=mask2048; onSwitchSprite=OnOff_0; offSwitchSprite=OnOff_1; onOffManager=OnOffManager; toggleCooldown=0.5 |
| Stage 19/OnOffManager/OnOff Switch (1) | OnOffSwitch | (-13.5, 40.5) | offsetApply=false; collisionMask=mask2048; onSwitchSprite=OnOff_0; offSwitchSprite=OnOff_1; onOffManager=OnOffManager; toggleCooldown=0.5 |
| Stage 19/OnOffManager/OnOff Switch (2) | OnOffSwitch | (-11.5, 41) | offsetApply=false; collisionMask=mask2048; onSwitchSprite=OnOff_0; offSwitchSprite=OnOff_1; onOffManager=OnOffManager; toggleCooldown=0.5 |
| Stage 19/OnOffManager/OnOff Switch (3) | OnOffSwitch | (-9.5, 41) | offsetApply=false; collisionMask=mask2048; onSwitchSprite=OnOff_0; offSwitchSprite=OnOff_1; onOffManager=OnOffManager; toggleCooldown=0.5 |
| Stage 19/OnOffManager/OnOff Switch (4) | OnOffSwitch | (-7.5, 41.61) | offsetApply=false; collisionMask=mask2048; onSwitchSprite=OnOff_0; offSwitchSprite=OnOff_1; onOffManager=OnOffManager; toggleCooldown=0.5 |
| Stage 19/OnOffManager/OnOff Switch (5) | OnOffSwitch | (-5.5, 42.11) | offsetApply=false; collisionMask=mask2048; onSwitchSprite=OnOff_0; offSwitchSprite=OnOff_1; onOffManager=OnOffManager; toggleCooldown=0.5 |
| Stage 19/OnOffManager/OnOff Switch (6) | OnOffSwitch | (-3.5, 42.5) | offsetApply=false; collisionMask=mask2048; onSwitchSprite=OnOff_0; offSwitchSprite=OnOff_1; onOffManager=OnOffManager; toggleCooldown=0.5 |
| Stage 19/OnOffManager/OnOff Switch (7) | OnOffSwitch | (-1.5, 42.5) | offsetApply=false; collisionMask=mask2048; onSwitchSprite=OnOff_0; offSwitchSprite=OnOff_1; onOffManager=OnOffManager; toggleCooldown=0.5 |
| Stage 19/OnOffManager/OnOff Switch (8) | OnOffSwitch | (0.5, 43) | offsetApply=false; collisionMask=mask2048; onSwitchSprite=OnOff_0; offSwitchSprite=OnOff_1; onOffManager=OnOffManager; toggleCooldown=0.5 |
| Stage 19/OnOffManager/OnOff Switch (9) | OnOffSwitch | (2.5, 43) | offsetApply=false; collisionMask=mask2048; onSwitchSprite=OnOff_0; offSwitchSprite=OnOff_1; onOffManager=OnOffManager; toggleCooldown=0.5 |
| Stage 19/OnOffManager/OnOff Switch (10) | OnOffSwitch | (4.5, 43.5) | offsetApply=false; collisionMask=mask2048; onSwitchSprite=OnOff_0; offSwitchSprite=OnOff_1; onOffManager=OnOffManager; toggleCooldown=0.5 |
| Stage 19/OnOffManager/OnOff Switch (11) | OnOffSwitch | (6.5, 43.5) | offsetApply=false; collisionMask=mask2048; onSwitchSprite=OnOff_0; offSwitchSprite=OnOff_1; onOffManager=OnOffManager; toggleCooldown=0.5 |
| Stage 19/Fall Playtforms/Fall Platforms | PlayerDetectPlatform | (5, -1) | offsetApply=false; collisionMask=mask2304; previewColor=FF0000FF; passengerMask=mask2048; localWaypoints=[(0, 0, 0), (0, -5, 0)]; speed=1; cyclic=false; isRepeat=false; originalPositionDelay=0.5; waitTime=0.3; easeAmount=0.2; isPassengerTransportEnabled=true; initialWaitTime=0.5; disappearAfterComplete=false; disableColliderWhenFalling=true; spriteRenderer=Sprite |
| Stage 19/Fall Playtforms/Fall Platforms (1) | PlayerDetectPlatform | (9.5, 1) | offsetApply=false; collisionMask=mask2304; previewColor=FF0000FF; passengerMask=mask2048; localWaypoints=[(0, 0, 0), (0, -5, 0)]; speed=1; cyclic=false; isRepeat=false; originalPositionDelay=0.5; waitTime=0.3; easeAmount=0.2; isPassengerTransportEnabled=true; initialWaitTime=0.5; disappearAfterComplete=false; disableColliderWhenFalling=true; spriteRenderer=Sprite |
| Stage 19/Fall Playtforms/Fall Platforms (2) | PlayerDetectPlatform | (4, 5.5) | offsetApply=false; collisionMask=mask2304; previewColor=FF0000FF; passengerMask=mask2048; localWaypoints=[(0, 0, 0), (0, -5, 0)]; speed=1; cyclic=false; isRepeat=false; originalPositionDelay=0.5; waitTime=0.3; easeAmount=0.2; isPassengerTransportEnabled=true; initialWaitTime=0.5; disappearAfterComplete=false; disableColliderWhenFalling=true; spriteRenderer=Sprite |
| Stage 19/Fall Playtforms/Fall Platforms (3) | PlayerDetectPlatform | (-14.9, 7) | offsetApply=false; collisionMask=mask2304; previewColor=FF0000FF; passengerMask=mask2048; localWaypoints=[(0, 0, 0), (0, -5, 0)]; speed=1; cyclic=false; isRepeat=false; originalPositionDelay=0.5; waitTime=0.3; easeAmount=0.2; isPassengerTransportEnabled=true; initialWaitTime=0.5; disappearAfterComplete=false; disableColliderWhenFalling=true; spriteRenderer=Sprite |
| Stage 19/Fall Playtforms/Fall Platforms (4) | PlayerDetectPlatform | (14.01, 21) | offsetApply=false; collisionMask=mask2304; previewColor=FF0000FF; passengerMask=mask2048; localWaypoints=[(0, 0, 0), (0, -5, 0)]; speed=1; cyclic=false; isRepeat=false; originalPositionDelay=0.5; waitTime=0.3; easeAmount=0.2; isPassengerTransportEnabled=true; initialWaitTime=0.5; disappearAfterComplete=false; disableColliderWhenFalling=true; spriteRenderer=Sprite |
| Stage 19/Fall Playtforms/Fall Platforms (5) | PlayerDetectPlatform | (14.01, 22.65) | offsetApply=false; collisionMask=mask2304; previewColor=FF0000FF; passengerMask=mask2048; localWaypoints=[(0, 0, 0), (0, -5, 0)]; speed=1; cyclic=false; isRepeat=false; originalPositionDelay=0.5; waitTime=0.3; easeAmount=0.2; isPassengerTransportEnabled=true; initialWaitTime=0.5; disappearAfterComplete=false; disableColliderWhenFalling=true; spriteRenderer=Sprite |
| Stage 19/MoveTraps/Move Trap | MovingTrap | (10.73, 24.2) | knockbackDir=(0, 0); endPosition=(-8, 0); speed=5; isRotate=true; rotationSpeed=1000 |
| Stage 19/MoveTraps/Move Trap (1) | MovingTrap | (3.5, 25.38) | knockbackDir=(0, 0); endPosition=(-8, 0); speed=6; isRotate=true; rotationSpeed=1000 |
| Stage 19/MoveTraps/Move Trap (2) | MovingTrap | (-5.68, 25.38) | knockbackDir=(0, 0); endPosition=(-8, 1); speed=7; isRotate=true; rotationSpeed=1000 |
| Stage 19/MoveTraps/Move Trap (3) | MovingTrap | (15.21, 50.3) | knockbackDir=(0, 0); endPosition=(-28, 2); speed=7; isRotate=true; rotationSpeed=1000 |
| Stage 19/MoveTraps/Move Trap (4) | MovingTrap | (14.99, 56.45) | knockbackDir=(0, 0); endPosition=(-13, -10); speed=7; isRotate=true; rotationSpeed=1000 |
| Stage 19/MoveTraps/Move Trap (5) | MovingTrap | (-1.07, 54.48) | knockbackDir=(0, 0); endPosition=(-10, 3); speed=7; isRotate=true; rotationSpeed=1000 |
| Stage 19/MoveTraps/Move Trap (6) | MovingTrap | (16.5, 59.32) | knockbackDir=(0, 0); endPosition=(-29, 0); speed=7; isRotate=true; rotationSpeed=1000 |
| Stage 19/SavePoints/Point (0) | SavePoint | (45.5, 1.6) | savePointID=0 |
| Stage 19/SavePoints/Point (1) | SavePoint | (85.5, 0.6) | savePointID=0 |
| Stage 19/Belts/Conveyor belt | Conveyor | (14.31, 2.46) | beltAnimators=[Left, Middle, Middle (1), Right] |
| Stage 19/Belts/Conveyor belt (1) | Conveyor | (-4.65, 7) | beltAnimators=[Left, Middle, Middle (1), Right] |
| Stage 19/Belts/Conveyor belt (2) | Conveyor | (-14.5, 37.5) | beltAnimators=[Left, Middle, Right] |
| Stage 19/Belts/Conveyor belt (3) | Conveyor | (-10.5, 38) | beltAnimators=[Left, Middle, Right] |
| Stage 19/Belts/Conveyor belt (4) | Conveyor | (-6.5, 38.5) | beltAnimators=[Left, Middle, Right] |
| Stage 19/Belts/Conveyor belt (5) | Conveyor | (-2.5, 39) | beltAnimators=[Left, Middle, Right] |
| Stage 19/Belts/Conveyor belt (6) | Conveyor | (1.5, 39.5) | beltAnimators=[Left, Middle, Right] |
| Stage 19/Belts/Conveyor belt (7) | Conveyor | (5.5, 40) | beltAnimators=[Left, Middle, Right] |

## Stage/Stage 2.prefab

| 경로 | 스크립트 | 위치 | 값 |
|---|---|---|---|
| Stage 2/Y Move 1 | DynamicObj | (0, 0) | xMoveValue=0; yMoveValue=1 |
| Stage 2/MoveTraps/Move Trap | MovingTrap | (5.5, 13.5) | knockbackDir=(0, 0); endPosition=(0, 8); speed=5; isRotate=true; rotationSpeed=1000 |
| Stage 2/MoveTraps/Move Trap (1) | MovingTrap | (-2.5, 13.5) | knockbackDir=(0, 0); endPosition=(0, 8); speed=5; isRotate=true; rotationSpeed=1000 |
| Stage 2/MoveTraps/Move Trap (2) | MovingTrap | (13.5, 28) | knockbackDir=(0, 0); endPosition=(-7, 0); speed=3; isRotate=true; rotationSpeed=1000 |
| Stage 2/MoveTraps/Move Trap (3) | MovingTrap | (3.5, 28) | knockbackDir=(0, 0); endPosition=(-7, 0); speed=2.5; isRotate=true; rotationSpeed=1000 |
| Stage 2/MoveTraps/Move Trap (4) | MovingTrap | (-6.5, 28) | knockbackDir=(0, 0); endPosition=(-7, 0); speed=4; isRotate=true; rotationSpeed=1000 |

## Stage/Stage 20.prefab

| 경로 | 스크립트 | 위치 | 값 |
|---|---|---|---|
| Stage 20/SavePoints/Point (0) | SavePoint | (115.5, 1.6) | savePointID=0 |
| Stage 20/Cannons/Canon | CannonShooter | (76.5, 2.5) | projectilePrefab=CannonBall; firstShootDelay=0; shootInterval=2; projectileSpeed=10; maxProjectileDistance=20; shootDirection=(1, 0) |
| Stage 20/Cannons/Canon (1) | CannonShooter | (90, 12.3) | projectilePrefab=CannonBall; firstShootDelay=0; shootInterval=3; projectileSpeed=8; maxProjectileDistance=20; shootDirection=(0, -1) |
| Stage 20/Cannons/Canon (2) | CannonShooter | (91, 12.3) | projectilePrefab=CannonBall; firstShootDelay=0.2; shootInterval=3; projectileSpeed=8; maxProjectileDistance=20; shootDirection=(0, -1) |
| Stage 20/Cannons/Canon (3) | CannonShooter | (92, 12.3) | projectilePrefab=CannonBall; firstShootDelay=0.4; shootInterval=3; projectileSpeed=8; maxProjectileDistance=20; shootDirection=(0, -1) |
| Stage 20/Cannons/Canon (4) | CannonShooter | (93, 12.3) | projectilePrefab=CannonBall; firstShootDelay=0.6; shootInterval=3; projectileSpeed=8; maxProjectileDistance=20; shootDirection=(0, -1) |
| Stage 20/Cannons/Canon (5) | CannonShooter | (94, 12.3) | projectilePrefab=CannonBall; firstShootDelay=0.8; shootInterval=3; projectileSpeed=8; maxProjectileDistance=20; shootDirection=(0, -1) |
| Stage 20/Cannons/Canon (6) | CannonShooter | (95, 12.3) | projectilePrefab=CannonBall; firstShootDelay=1; shootInterval=3; projectileSpeed=8; maxProjectileDistance=20; shootDirection=(0, -1) |
| Stage 20/Cannons/Canon (7) | CannonShooter | (96, 12.3) | projectilePrefab=CannonBall; firstShootDelay=1.2; shootInterval=3; projectileSpeed=8; maxProjectileDistance=20; shootDirection=(0, -1) |
| Stage 20/Cannons/Canon (8) | CannonShooter | (97, 12.3) | projectilePrefab=CannonBall; firstShootDelay=1.4; shootInterval=3; projectileSpeed=8; maxProjectileDistance=20; shootDirection=(0, -1) |
| Stage 20/Cannons/Canon (9) | CannonShooter | (98, 12.3) | projectilePrefab=CannonBall; firstShootDelay=1.6; shootInterval=3; projectileSpeed=8; maxProjectileDistance=20; shootDirection=(0, -1) |
| Stage 20/Cannons/Canon (10) | CannonShooter | (99, 12.3) | projectilePrefab=CannonBall; firstShootDelay=1.8; shootInterval=3; projectileSpeed=8; maxProjectileDistance=20; shootDirection=(0, -1) |
| Stage 20/Cannons/Canon (11) | CannonShooter | (100, 12.3) | projectilePrefab=CannonBall; firstShootDelay=2; shootInterval=3; projectileSpeed=8; maxProjectileDistance=20; shootDirection=(0, -1) |
| Stage 20/Cannons/Canon (12) | CannonShooter | (101, 12.3) | projectilePrefab=CannonBall; firstShootDelay=2.2; shootInterval=3; projectileSpeed=8; maxProjectileDistance=20; shootDirection=(0, -1) |
| Stage 20/Cannons/Canon (13) | CannonShooter | (102, 12.3) | projectilePrefab=CannonBall; firstShootDelay=2.4; shootInterval=3; projectileSpeed=8; maxProjectileDistance=20; shootDirection=(0, -1) |
| Stage 20/Cannons/Canon (14) | CannonShooter | (103, 12.3) | projectilePrefab=CannonBall; firstShootDelay=2.6; shootInterval=3; projectileSpeed=8; maxProjectileDistance=20; shootDirection=(0, -1) |
| Stage 20/Cannons/Canon (15) | CannonShooter | (137.5, -14.5) | projectilePrefab=CannonBall; firstShootDelay=2.6; shootInterval=3; projectileSpeed=8; maxProjectileDistance=25; shootDirection=(-1, 0) |
| Stage 20/Cannons/Canon (16) | CannonShooter | (137.5, -12.5) | projectilePrefab=CannonBall; firstShootDelay=2.6; shootInterval=3; projectileSpeed=8; maxProjectileDistance=25; shootDirection=(-1, 0) |
| Stage 20/Cannons/Canon (17) | CannonShooter | (137.5, -10.5) | projectilePrefab=CannonBall; firstShootDelay=2.6; shootInterval=3; projectileSpeed=8; maxProjectileDistance=5; shootDirection=(-1, 0) |
| Stage 20/Cannons/Canon (18) | CannonShooter | (137.5, -8.5) | projectilePrefab=CannonBall; firstShootDelay=2.6; shootInterval=3; projectileSpeed=8; maxProjectileDistance=5; shootDirection=(-1, 0) |
| Stage 20/Cannons/Canon (19) | CannonShooter | (137.5, -6.5) | projectilePrefab=CannonBall; firstShootDelay=2.6; shootInterval=3; projectileSpeed=8; maxProjectileDistance=5; shootDirection=(-1, 0) |
| Stage 20/Cannons/Canon (20) | CannonShooter | (161.5, -3.5) | projectilePrefab=CannonBall; firstShootDelay=2.6; shootInterval=3; projectileSpeed=8; maxProjectileDistance=15; shootDirection=(-1, 0) |
| Stage 20/Cannons/Canon (21) | CannonShooter | (161.5, -1.5) | projectilePrefab=CannonBall; firstShootDelay=2.6; shootInterval=3; projectileSpeed=8; maxProjectileDistance=15; shootDirection=(1, 0) |
| Stage 20/Auto OnOffManager | OnOffManager | (0, 0) | switches=[OnOff Switch]; conveyorBelts=[Conveyor belt, Conveyor belt (1), Conveyor belt (2), Conveyor belt (3), Conveyor belt (4), Conveyor belt (5), Conveyor belt (6), Conveyor belt (7), Conveyor belt (8)]; activeSwitchGround=null; inactiveSwitchGround=null; autoToggleEnabled=true; autoToggleInterval=3 |
| Stage 20/Auto OnOffManager/OnOff Switch | OnOffSwitch | (0, 50) | offsetApply=false; collisionMask=mask2048; onSwitchSprite=OnOff_0; offSwitchSprite=OnOff_1; onOffManager=Auto OnOffManager; toggleCooldown=0.5 |
| Stage 20/OnOffManager | OnOffManager | (0, 0) | switches=[OnOff Switch]; conveyorBelts=[Conveyor belt (9)]; activeSwitchGround=null; inactiveSwitchGround=null; autoToggleEnabled=true; autoToggleInterval=3 |
| Stage 20/OnOffManager/OnOff Switch | OnOffSwitch | (0, 50) | offsetApply=false; collisionMask=mask2048; onSwitchSprite=OnOff_0; offSwitchSprite=OnOff_1; onOffManager=OnOffManager; toggleCooldown=0.5 |
| Stage 20/Belts/Conveyor belt | Conveyor | (10, 0) | beltAnimators=[Left, Middle, Middle (1), Right] |
| Stage 20/Belts/Conveyor belt (1) | Conveyor | (17, 0) | beltAnimators=[Left, Middle, Middle (1), Right] |
| Stage 20/Belts/Conveyor belt (2) | Conveyor | (27, 1) | beltAnimators=[Left, Middle, Middle (1), Right] |
| Stage 20/Belts/Conveyor belt (3) | Conveyor | (38, 0) | beltAnimators=[Left, Middle, Middle (1), Right] |
| Stage 20/Belts/Conveyor belt (4) | Conveyor | (45, 2) | beltAnimators=[Left, Middle, Middle (1), Right] |
| Stage 20/Belts/Conveyor belt (5) | Conveyor | (52, 0) | beltAnimators=[Left, Middle, Middle (1), Right] |
| Stage 20/Belts/Conveyor belt (6) | Conveyor | (61, 2) | beltAnimators=[Left, Middle, Middle (1), Right] |
| Stage 20/Belts/Conveyor belt (7) | Conveyor | (70, 4) | beltAnimators=[Left, Middle, Middle (1), Right] |
| Stage 20/Belts/Conveyor belt (8) | Conveyor | (90, 1) | beltAnimators=[Left, Middle, Middle (1), Right] |
| Stage 20/MoveTraps/Move Trap | MovingTrap | (55, 1) | knockbackDir=(0, 0); endPosition=(-17, 0); speed=10; isRotate=true; rotationSpeed=1000 |
| Stage 20/MoveTraps/Move Trap (1) | PlatformController | (120.5, -2) | offsetApply=false; collisionMask=mask0; previewColor=FF0081FF; passengerMask=mask0; localWaypoints=[(0, 0, 0), (8, 0, 0), (8, -8, 0), (0, -8, 0)]; speed=8; cyclic=false; isRepeat=true; originalPositionDelay=0; waitTime=0; easeAmount=0.2; isPassengerTransportEnabled=false |
| Stage 20/MoveTraps/Move Trap (2) | MovingTrap | (151.65, 0) | knockbackDir=(0, 0); endPosition=(0, -10); speed=10; isRotate=true; rotationSpeed=1000 |
| Stage 20/MoveTraps/Move Trap (3) | PlatformController | (156.5, 2) | offsetApply=false; collisionMask=mask0; previewColor=FF0081FF; passengerMask=mask0; localWaypoints=[(0, 0, 0), (8, 0, 0), (8, -8, 0), (0, -8, 0)]; speed=8; cyclic=false; isRepeat=true; originalPositionDelay=0; waitTime=0; easeAmount=0.2; isPassengerTransportEnabled=false |
| Stage 20/MoveTraps/Move Trap (4) | PlatformController | (173, 2) | offsetApply=false; collisionMask=mask0; previewColor=FF0081FF; passengerMask=mask0; localWaypoints=[(0, 0, 0), (8, 0, 0), (8, -8, 0), (0, -8, 0)]; speed=8; cyclic=false; isRepeat=true; originalPositionDelay=0; waitTime=0; easeAmount=0.2; isPassengerTransportEnabled=false |
| Stage 20/Moving Belt/Conveyor belt (9) | Conveyor | (145, -5.61) | beltAnimators=[Left, Middle, Middle (1)] |
| Stage 20/Moving Belt/Conveyor belt (9) | PlayerDetectPlatform | (145, -5.61) | offsetApply=false; collisionMask=mask2304; previewColor=4FFF00FF; passengerMask=mask2048; localWaypoints=[(0, 0, 0), (12.5, 0, 0), (12.5, 5, 0), (25, 5, 0), (19, 0, 0), (25, 0, 0), (25, 5, 0), (40, 5, 0)]; speed=0; cyclic=false; isRepeat=false; originalPositionDelay=0; waitTime=0; easeAmount=0; isPassengerTransportEnabled=true; initialWaitTime=0; disappearAfterComplete=false; disableColliderWhenFalling=false; spriteRenderer=null |

## Stage/Stage 21.prefab

| 경로 | 스크립트 | 위치 | 값 |
|---|---|---|---|
| Stage 21/Boxes/Box Switch Prefab/Box Switch Set | ObjSummonSwitch | (2, -1.5) | summonLocation=Box Respawn; cooldownTime=5; targetObjTrans=Box; defaultSprite=Switch_0; pressedSprite=Switch_1 |
| Stage 21/Boxes/Box Switch Prefab/Box | PickupObj | (9, 0) | offsetApply=false; collisionMask=mask3520; maxSlopeAngle=45; gravity=9.8; velocity=(0, 0); wallSlideSpeedMax=3 |
| Stage 21/SavePoints/Point (0) | SavePoint | (0, 0) | savePointID=0 |
| Stage 21/Cannons/Canon | CannonShooter | (76.5, 2.5) | projectilePrefab=CannonBall; firstShootDelay=0; shootInterval=2; projectileSpeed=10; maxProjectileDistance=20; shootDirection=(1, 0) |
| Stage 21/Auto OnOffManager | OnOffManager | (0, 0) | switches=[OnOff Switch]; conveyorBelts=[Conveyor belt, Conveyor belt (1), Conveyor belt (2)]; activeSwitchGround=null; inactiveSwitchGround=null; autoToggleEnabled=true; autoToggleInterval=3 |
| Stage 21/Auto OnOffManager/OnOff Switch | OnOffSwitch | (0, 50) | offsetApply=false; collisionMask=mask2048; onSwitchSprite=OnOff_0; offSwitchSprite=OnOff_1; onOffManager=Auto OnOffManager; toggleCooldown=0.5 |
| Stage 21/OnOffManager | OnOffManager | (0, 0) | switches=[OnOff Switch, OnOff Switch]; conveyorBelts=[Conveyor belt (6)]; activeSwitchGround=null; inactiveSwitchGround=null; autoToggleEnabled=true; autoToggleInterval=3 |
| Stage 21/OnOffManager/OnOff Switch | OnOffSwitch | (0, 50) | offsetApply=false; collisionMask=mask2048; onSwitchSprite=OnOff_0; offSwitchSprite=OnOff_1; onOffManager=OnOffManager; toggleCooldown=0.5 |
| Stage 21/Belts/Conveyor belt | Conveyor | (5, -1.5) | beltAnimators=[Left, Middle, Middle (1), Right] |
| Stage 21/Belts/Conveyor belt (1) | Conveyor | (-5, -0.5) | beltAnimators=[Left, Middle, Middle (1), Right] |
| Stage 21/Belts/Conveyor belt (2) | Conveyor | (-15, 0.5) | beltAnimators=[Left, Middle, Middle (1), Right] |
| Stage 21/MoveTraps/Move Trap | MovingTrap | (0, 12.79) | knockbackDir=(0, 0); endPosition=(-16, 0); speed=7; isRotate=true; rotationSpeed=1000 |
| Stage 21/OnOffSwitch on MovingBelt/Conveyor belt (6) | Conveyor | (-17, 5.5) | beltAnimators=[Left, Middle, Middle (1), Middle (2), Right] |
| Stage 21/OnOffSwitch on MovingBelt/Conveyor belt (6) | PlayerDetectPlatform | (-17, 5.5) | offsetApply=false; collisionMask=mask0; previewColor=FF0000FF; passengerMask=mask2048; localWaypoints=[(0, 0, 0), (0, 5, 0), (7, 15, 0)]; speed=4; cyclic=false; isRepeat=false; originalPositionDelay=0; waitTime=0; easeAmount=0; isPassengerTransportEnabled=true; initialWaitTime=0; disappearAfterComplete=false; disableColliderWhenFalling=false; spriteRenderer=null |
| Stage 21/OnOffSwitch on MovingBelt/Conveyor belt (6)/OnOff Switch | OnOffSwitch | (-15, 8.5) | offsetApply=false; collisionMask=mask2048; onSwitchSprite=OnOff_0; offSwitchSprite=OnOff_1; onOffManager=OnOffManager; toggleCooldown=0.5 |
| Stage 21/OnOffSwitch on MovingBelt/Reset Btn | DetectPlatformResetBtn | (-20, 6.7) | offsetApply=true; collisionMask=mask2048; onSwitchSprite=PlayerSwitch_0; offSwitchSprite=PlayerSwitch_1; activationLayer=mask2048; detectPlatform=Conveyor belt (6) |

## Stage/Stage 3.prefab

| 경로 | 스크립트 | 위치 | 값 |
|---|---|---|---|
| Stage 3/Y Move 1 | DynamicObj | (3, 0) | xMoveValue=0; yMoveValue=1 |
| Stage 3/MoveTraps/Move Trap | MovingTrap | (10.5, 0) | knockbackDir=(0, 0); endPosition=(-4, 0); speed=2; isRotate=true; rotationSpeed=1000 |
| Stage 3/MoveTraps/Move Trap (2) | MovingTrap | (0.5, 16.5) | knockbackDir=(0, 0); endPosition=(0, -14); speed=5; isRotate=true; rotationSpeed=1000 |
| Stage 3/MoveTraps/Move Trap (3) | MovingTrap | (-7.5, 16.5) | knockbackDir=(0, 0); endPosition=(0, -14); speed=5; isRotate=true; rotationSpeed=1000 |
| Stage 3/MoveTraps/Move Trap (4) | MovingTrap | (4.5, 19.5) | knockbackDir=(0, 0); endPosition=(20, 0); speed=5; isRotate=true; rotationSpeed=1000 |
| Stage 3/MoveTraps/Move Trap (5) | MovingTrap | (24.5, 20.4) | knockbackDir=(0, 0); endPosition=(-20, 0); speed=5; isRotate=true; rotationSpeed=1000 |
| Stage 3/MoveTraps/Move Trap (6) | MovingTrap | (4.5, 24.5) | knockbackDir=(0, 0); endPosition=(20, 0); speed=5; isRotate=true; rotationSpeed=1000 |
| Stage 3/MoveTraps/Move Trap (9) | MovingTrap | (-15.5, 24.5) | knockbackDir=(0, 0); endPosition=(20, 0); speed=7; isRotate=true; rotationSpeed=1000 |

## Stage/Stage 4.prefab

| 경로 | 스크립트 | 위치 | 값 |
|---|---|---|---|
| Stage 4/Y Move 1 | DynamicObj | (0, 0) | xMoveValue=0; yMoveValue=1 |
| Stage 4/Monster/Canon | CannonShooter | (-6.5, 6.5) | projectilePrefab=CannonBall; firstShootDelay=0; shootInterval=2; projectileSpeed=10; maxProjectileDistance=20; shootDirection=(1, 0) |
| Stage 4/Monster/Canon (5) | CannonShooter | (-6.5, 10.5) | projectilePrefab=CannonBall; firstShootDelay=0; shootInterval=2; projectileSpeed=10; maxProjectileDistance=20; shootDirection=(1, 0) |
| Stage 4/Monster/Canon (6) | CannonShooter | (-6.5, 14.5) | projectilePrefab=CannonBall; firstShootDelay=0; shootInterval=2; projectileSpeed=10; maxProjectileDistance=20; shootDirection=(1, 0) |
| Stage 4/Monster/Canon (1) | CannonShooter | (6.52, 4.5) | projectilePrefab=CannonBall; firstShootDelay=0; shootInterval=2; projectileSpeed=10; maxProjectileDistance=20; shootDirection=(-1, 0) |
| Stage 4/Monster/Canon (2) | CannonShooter | (6.52, 8.5) | projectilePrefab=CannonBall; firstShootDelay=0; shootInterval=2; projectileSpeed=10; maxProjectileDistance=20; shootDirection=(-1, 0) |
| Stage 4/Monster/Canon (3) | CannonShooter | (6.52, 12.5) | projectilePrefab=CannonBall; firstShootDelay=0; shootInterval=2; projectileSpeed=10; maxProjectileDistance=20; shootDirection=(-1, 0) |
| Stage 4/Monster/Canon (4) | CannonShooter | (6.52, 16.5) | projectilePrefab=CannonBall; firstShootDelay=0; shootInterval=2; projectileSpeed=10; maxProjectileDistance=20; shootDirection=(-1, 0) |
| Stage 4/Monster/Canon (7) | CannonShooter | (-19.5, 27.5) | projectilePrefab=CannonBall; firstShootDelay=0; shootInterval=2; projectileSpeed=10; maxProjectileDistance=20; shootDirection=(1, 0) |
| Stage 4/Monster/Canon (8) | CannonShooter | (14.5, 37.5) | projectilePrefab=CannonBall; firstShootDelay=0; shootInterval=2; projectileSpeed=10; maxProjectileDistance=20; shootDirection=(-1, 0) |
| Stage 4/Monster/Canon (9) | CannonShooter | (14.5, 42.5) | projectilePrefab=CannonBall; firstShootDelay=0; shootInterval=2; projectileSpeed=10; maxProjectileDistance=20; shootDirection=(-1, 0) |
| Stage 4/Monster/Canon (10) | CannonShooter | (2.5, 33.5) | projectilePrefab=CannonBall; firstShootDelay=0; shootInterval=2; projectileSpeed=10; maxProjectileDistance=20; shootDirection=(-1, 0) |

## Stage/Stage 5.prefab

| 경로 | 스크립트 | 위치 | 값 |
|---|---|---|---|
| Stage 5/Y Move 1 | DynamicObj | (20, 0) | xMoveValue=0; yMoveValue=1 |
| Stage 5/Monster/Canon | CannonShooter | (22.5, 6.5) | projectilePrefab=CannonBall; firstShootDelay=0; shootInterval=2; projectileSpeed=10; maxProjectileDistance=20; shootDirection=(-1, 0) |
| Stage 5/Monster/Canon (1) | CannonShooter | (34.5, 15.5) | projectilePrefab=CannonBall; firstShootDelay=0; shootInterval=2; projectileSpeed=10; maxProjectileDistance=20; shootDirection=(-1, 0) |
| Stage 5/RotateObstacles/RotateObstacle | RotatingObstacle | (9.5, 0.5) | knockbackDir=(-1, 0); isCounterclockwise=true; rotationSpeed=80; random=false; randomRange=0 |
| Stage 5/RotateObstacles/RotateObstacle (1) | RotatingObstacle | (20.5, 11.5) | knockbackDir=(-1, 0); isCounterclockwise=true; rotationSpeed=120; random=false; randomRange=0 |
| Stage 5/RotateObstacles/RotateObstacle (2) | RotatingObstacle | (29.5, 20.5) | knockbackDir=(-1, 0); isCounterclockwise=true; rotationSpeed=140; random=false; randomRange=0 |
| Stage 5/RotateObstacles/RotateObstacle (3) | RotatingObstacle | (34.5, 25.5) | knockbackDir=(-1, 0); isCounterclockwise=false; rotationSpeed=100; random=false; randomRange=0 |

## Stage/Stage 6.prefab

| 경로 | 스크립트 | 위치 | 값 |
|---|---|---|---|
| Stage 6/Y Move 1 | DynamicObj | (0, 0) | xMoveValue=0; yMoveValue=1 |
| Stage 6/MoveTraps/Move Trap | MovingTrap | (5.5, 0) | knockbackDir=(0, 0); endPosition=(-4, 0); speed=2; isRotate=true; rotationSpeed=1000 |
| Stage 6/MoveTraps/Move Trap (1) | MovingTrap | (-5.5, 0.5) | knockbackDir=(0, 0); endPosition=(4, 0); speed=1; isRotate=true; rotationSpeed=1000 |
| Stage 6/MoveTraps/Move Trap (4) | MovingTrap | (-19.5, 7.5) | knockbackDir=(0, 0); endPosition=(4, 0); speed=30; isRotate=true; rotationSpeed=1000 |
| Stage 6/MoveTraps/Move Trap (2) | MovingTrap | (19.5, 11.5) | knockbackDir=(0, 0); endPosition=(-15, 0); speed=5; isRotate=true; rotationSpeed=1000 |
| Stage 6/MoveTraps/Move Trap (3) | MovingTrap | (18.5, 4.5) | knockbackDir=(0, 0); endPosition=(-14, 0); speed=5; isRotate=true; rotationSpeed=1000 |
| Stage 6/MoveTraps/Move Trap (5) | MovingTrap | (6.5, 33.9) | knockbackDir=(0, 0); endPosition=(-14, 0); speed=5; isRotate=true; rotationSpeed=1000 |
| Stage 6/MoveTraps/Move Trap (9) | MovingTrap | (6.5, 31.41) | knockbackDir=(0, 0); endPosition=(-4, 0); speed=5; isRotate=true; rotationSpeed=1000 |
| Stage 6/MoveTraps/Move Trap (10) | MovingTrap | (-6, 37.5) | knockbackDir=(0, 0); endPosition=(6, 0); speed=2; isRotate=true; rotationSpeed=1000 |
| Stage 6/MoveTraps/Move Trap (7) | MovingTrap | (6.5, 25.5) | knockbackDir=(0, 0); endPosition=(-6, 0); speed=5; isRotate=true; rotationSpeed=1000 |
| Stage 6/MoveTraps/Move Trap (8) | MovingTrap | (-7.5, 25.5) | knockbackDir=(0, 0); endPosition=(6, 0); speed=5; isRotate=true; rotationSpeed=1000 |
| Stage 6/MoveTraps/Move Trap (6) | MovingTrap | (-0.5, 39) | knockbackDir=(0, 0); endPosition=(0, -10); speed=5; isRotate=true; rotationSpeed=1000 |
| Stage 6/MoveTraps/Move Trap (11) | MovingTrap | (-0.5, 39) | knockbackDir=(0, 0); endPosition=(4, 0); speed=2; isRotate=true; rotationSpeed=1000 |
| Stage 6/Cannons/Canon | CannonShooter | (-8.5, 28.5) | projectilePrefab=CannonBall; firstShootDelay=0; shootInterval=2; projectileSpeed=10; maxProjectileDistance=20; shootDirection=(1, 0) |
| Stage 6/Cannons/Canon (1) | CannonShooter | (-19.5, 51.5) | projectilePrefab=CannonBall; firstShootDelay=0; shootInterval=2; projectileSpeed=10; maxProjectileDistance=20; shootDirection=(1, 0) |
| Stage 6/Cannons/Canon (2) | CannonShooter | (-1.75, 60.5) | projectilePrefab=CannonBall; firstShootDelay=0; shootInterval=2.5; projectileSpeed=10; maxProjectileDistance=20; shootDirection=(-1, 0) |
| Stage 6/Cannons/Canon (3) | CannonShooter | (3.5, 66.5) | projectilePrefab=CannonBall; firstShootDelay=0; shootInterval=2.5; projectileSpeed=10; maxProjectileDistance=20; shootDirection=(-1, 0) |
| Stage 6/Cannons/Canon (4) | CannonShooter | (7.5, 71.6) | projectilePrefab=CannonBall; firstShootDelay=0; shootInterval=2.5; projectileSpeed=8; maxProjectileDistance=20; shootDirection=(-1, 0) |
| Stage 6/Cannons/Canon (5) | CannonShooter | (12.5, 76.6) | projectilePrefab=CannonBall; firstShootDelay=0; shootInterval=3; projectileSpeed=20; maxProjectileDistance=20; shootDirection=(-1, 0) |
| Stage 6/Cannons/Canon (6) | CannonShooter | (19.5, 81.5) | projectilePrefab=CannonBall; firstShootDelay=0; shootInterval=15; projectileSpeed=30; maxProjectileDistance=20; shootDirection=(-1, 0) |
| Stage 6/RotateOpstacles/RotateObstacle (1) | RotatingObstacle | (14.5, 46) | knockbackDir=(-1, 0); isCounterclockwise=true; rotationSpeed=50; random=false; randomRange=0 |
| Stage 6/RotateOpstacles/RotateObstacle (2) | RotatingObstacle | (-6.5, 63.5) | knockbackDir=(-1, 0); isCounterclockwise=true; rotationSpeed=100; random=false; randomRange=0 |
| Stage 6/RotateOpstacles/RotateObstacle (3) | RotatingObstacle | (8.5, 78.5) | knockbackDir=(-1, 0); isCounterclockwise=false; rotationSpeed=20; random=false; randomRange=0 |

## Stage/Stage 7.prefab

| 경로 | 스크립트 | 위치 | 값 |
|---|---|---|---|
| Stage 7/MoveTraps/Move Trap | MovingTrap | (60.5, 9.5) | knockbackDir=(0, 0); endPosition=(0, 0); speed=0; isRotate=true; rotationSpeed=1000 |
| Stage 7/MoveTraps/Move Trap (1) | MovingTrap | (66.5, 13.5) | knockbackDir=(0, 0); endPosition=(0, 0); speed=0; isRotate=true; rotationSpeed=1000 |
| Stage 7/MoveTraps/Move Trap (2) | MovingTrap | (72.5, 9.5) | knockbackDir=(0, 0); endPosition=(0, 0); speed=0; isRotate=true; rotationSpeed=1000 |
| Stage 7/Switch | ObjSummonSwitch | (-1.5, -2.38) | summonLocation=GameObject; cooldownTime=5; targetObjTrans=Box; defaultSprite=Switch_0; pressedSprite=Switch_1 |
| Stage 7/Box | PickupObj | (5, 0) | offsetApply=false; collisionMask=mask3392; maxSlopeAngle=45; gravity=9.8; velocity=(0, 0); wallSlideSpeedMax=3 |
| Stage 7/Grid/ResetTile | RespawnHandler | (0, 0) | targetLayers=mask2304; resetPoint=RespawnPoint; onlyBoxReset=false |
| Stage 7/DoorSwitch | DoorSwitch | (79.5, 16.5) | offsetApply=false; collisionMask=mask0; onSwitchSprite=Switch_1; offSwitchSprite=Switch_0; activationLayer=mask256; door=Door |

## Stage/Stage 8.prefab

| 경로 | 스크립트 | 위치 | 값 |
|---|---|---|---|
| Stage 8/Cannons/Canon | CannonShooter | (-2.5, 6.5) | projectilePrefab=CannonBall; firstShootDelay=0; shootInterval=2; projectileSpeed=10; maxProjectileDistance=20; shootDirection=(1, 0) |
| Stage 8/Cannons/Canon (1) | CannonShooter | (8.5, 1.5) | projectilePrefab=CannonBall; firstShootDelay=0; shootInterval=2; projectileSpeed=10; maxProjectileDistance=20; shootDirection=(-1, 0) |
| Stage 8/Cannons/Canon (2) | CannonShooter | (8.5, 11.5) | projectilePrefab=CannonBall; firstShootDelay=0; shootInterval=2; projectileSpeed=10; maxProjectileDistance=20; shootDirection=(-1, 0) |
| Stage 8/Cannons/Canon (3) | CannonShooter | (-2.5, 16.5) | projectilePrefab=CannonBall; firstShootDelay=0; shootInterval=2; projectileSpeed=20; maxProjectileDistance=20; shootDirection=(1, 0) |
| Stage 8/Cannons/Canon (4) | CannonShooter | (8.5, 39.5) | projectilePrefab=CannonBall; firstShootDelay=0; shootInterval=2; projectileSpeed=11; maxProjectileDistance=20; shootDirection=(1, 0) |
| Stage 8/Cannons/Canon (5) | CannonShooter | (8.5, 41.5) | projectilePrefab=CannonBall; firstShootDelay=0; shootInterval=2; projectileSpeed=10; maxProjectileDistance=20; shootDirection=(1, 0) |
| Stage 8/Cannons/Canon (6) | CannonShooter | (12, 56.5) | projectilePrefab=CannonBall; firstShootDelay=0; shootInterval=2; projectileSpeed=15; maxProjectileDistance=20; shootDirection=(-1, 0) |
| Stage 8/Cannons/Canon (7) | CannonShooter | (-6.5, 70.5) | projectilePrefab=CannonBall; firstShootDelay=0; shootInterval=2; projectileSpeed=20; maxProjectileDistance=20; shootDirection=(1, 0) |
| Stage 8/MoveTraps/Move Trap | MovingTrap | (16.5, 43.5) | knockbackDir=(0, 0); endPosition=(-7, 0); speed=2; isRotate=true; rotationSpeed=1000 |
| Stage 8/MoveTraps/Move Trap (5) | MovingTrap | (11.5, 44) | knockbackDir=(0, 0); endPosition=(-7, 0); speed=2; isRotate=true; rotationSpeed=1000 |
| Stage 8/MoveTraps/Move Trap (1) | MovingTrap | (14.5, 34.54) | knockbackDir=(0, 0); endPosition=(-7, 0); speed=2; isRotate=true; rotationSpeed=1000 |
| Stage 8/MoveTraps/Move Trap (2) | MovingTrap | (-1.5, 60.5) | knockbackDir=(0, 0); endPosition=(-8, 0); speed=2; isRotate=true; rotationSpeed=1000 |
| Stage 8/MoveTraps/Move Trap (3) | MovingTrap | (-1.5, 55.5) | knockbackDir=(0, 0); endPosition=(0, 10); speed=2.3; isRotate=true; rotationSpeed=1000 |
| Stage 8/MoveTraps/Move Trap (4) | MovingTrap | (3, 67) | knockbackDir=(0, 0); endPosition=(10, 0); speed=2.3; isRotate=true; rotationSpeed=1000 |
| Stage 8/RotateOpstacles/RotateObstacle (1) | RotatingObstacle | (9.5, 43.5) | knockbackDir=(1, 0); isCounterclockwise=true; rotationSpeed=50; random=false; randomRange=0 |
| Stage 8/DoorSwitchs/DoorSwitch | DoorSwitch | (8.5, 19.5) | offsetApply=false; collisionMask=mask0; onSwitchSprite=Switch_1; offSwitchSprite=Switch_0; activationLayer=mask256; door=Door |
| Stage 8/DoorSwitchs/DoorSwitch (3) | DoorSwitch | (4.5, 44.5) | offsetApply=false; collisionMask=mask0; onSwitchSprite=Switch_1; offSwitchSprite=Switch_0; activationLayer=mask2048; door=Door |
| Stage 8/DoorSwitchs/DoorSwitch (1) | DoorSwitch | (10.5, 47.5) | offsetApply=false; collisionMask=mask0; onSwitchSprite=Switch_1; offSwitchSprite=Switch_0; activationLayer=mask256; door=Door |
| Stage 8/DoorSwitchs/DoorSwitch (2) | DoorSwitch | (-6.5, 74.5) | offsetApply=false; collisionMask=mask0; onSwitchSprite=Switch_1; offSwitchSprite=Switch_0; activationLayer=mask256; door=Door |
| Stage 8/Boxes/Box Switch Prefab/Box Switch Set | ObjSummonSwitch | (3, 1) | summonLocation=Box Respawn; cooldownTime=5; targetObjTrans=Box; defaultSprite=Switch_0; pressedSprite=Switch_1 |
| Stage 8/Boxes/Box Switch Prefab/Box | PickupObj | (3, 1) | offsetApply=false; collisionMask=mask3520; maxSlopeAngle=45; gravity=9.8; velocity=(0, 0); wallSlideSpeedMax=3 |
| Stage 8/Boxes/Box Switch Prefab (1)/Box Switch Set | ObjSummonSwitch | (0, 23.75) | summonLocation=Box Respawn; cooldownTime=5; targetObjTrans=Box; defaultSprite=Switch_0; pressedSprite=Switch_1 |
| Stage 8/Boxes/Box Switch Prefab (1)/Box | PickupObj | (5, 26) | offsetApply=false; collisionMask=mask3520; maxSlopeAngle=45; gravity=9.8; velocity=(0, 0); wallSlideSpeedMax=3 |
| Stage 8/Boxes/Box Switch Prefab (2)/Box Switch Set | ObjSummonSwitch | (10, 52.5) | summonLocation=Box Respawn; cooldownTime=5; targetObjTrans=Box; defaultSprite=Switch_0; pressedSprite=Switch_1 |
| Stage 8/Boxes/Box Switch Prefab (2)/Box | PickupObj | (7, 53) | offsetApply=false; collisionMask=mask3520; maxSlopeAngle=45; gravity=9.8; velocity=(0, 0); wallSlideSpeedMax=3 |

## Stage/Stage 9.prefab

| 경로 | 스크립트 | 위치 | 값 |
|---|---|---|---|
| Stage 9/Grid/First ResetTile | RespawnHandler | (3, 1) | targetLayers=mask2048; resetPoint=RespawnPoint ; onlyBoxReset=false |
| Stage 9/Grid/ResetTile | RespawnHandler | (3, 1) | targetLayers=mask2048; resetPoint=SecondRespawnPoint ; onlyBoxReset=false |
| Stage 9/Cannons/Canon | CannonShooter | (83, 1.5) | projectilePrefab=CannonBall; firstShootDelay=0; shootInterval=2; projectileSpeed=10; maxProjectileDistance=20; shootDirection=(1, 0) |
| Stage 9/MoveTraps/Move Trap | MovingTrap | (73, 10) | knockbackDir=(0, 0); endPosition=(10, 0); speed=1.3; isRotate=false; rotationSpeed=720 |
| Stage 9/MoveTraps/Move Trap (1) | MovingTrap | (63, 10) | knockbackDir=(0, 0); endPosition=(10, 0); speed=1; isRotate=false; rotationSpeed=720 |
| Stage 9/RotateOpstacles/RotateObstacle (1) | RotatingObstacle | (26.57, 4.5) | knockbackDir=(-1, 0); isCounterclockwise=true; rotationSpeed=120; random=false; randomRange=0 |
| Stage 9/RotateOpstacles/RotateObstacle (2) | RotatingObstacle | (61.5, -0.5) | knockbackDir=(-1, 0); isCounterclockwise=true; rotationSpeed=45; random=true; randomRange=5 |
| Stage 9/RotateOpstacles/RotateObstacle (4) | RotatingObstacle | (65.5, -0.5) | knockbackDir=(-1, 0); isCounterclockwise=true; rotationSpeed=45; random=true; randomRange=5 |
| Stage 9/RotateOpstacles/RotateObstacle (6) | RotatingObstacle | (69.5, -0.5) | knockbackDir=(-1, 0); isCounterclockwise=true; rotationSpeed=45; random=true; randomRange=5 |
| Stage 9/RotateOpstacles/RotateObstacle (9) | RotatingObstacle | (73.5, -0.5) | knockbackDir=(-1, 0); isCounterclockwise=true; rotationSpeed=45; random=true; randomRange=5 |
| Stage 9/RotateOpstacles/RotateObstacle (10) | RotatingObstacle | (77.5, -0.5) | knockbackDir=(-1, 0); isCounterclockwise=true; rotationSpeed=45; random=true; randomRange=5 |
| Stage 9/RotateOpstacles/RotateObstacle (3) | RotatingObstacle | (65.5, 4.65) | knockbackDir=(-1, 0); isCounterclockwise=true; rotationSpeed=45; random=true; randomRange=5 |
| Stage 9/RotateOpstacles/RotateObstacle (7) | RotatingObstacle | (73.5, 4.65) | knockbackDir=(-1, 0); isCounterclockwise=true; rotationSpeed=45; random=true; randomRange=5 |
| Stage 9/RotateOpstacles/RotateObstacle (8) | RotatingObstacle | (79.5, 4.65) | knockbackDir=(-1, 0); isCounterclockwise=true; rotationSpeed=20; random=true; randomRange=5 |
| Stage 9/RotateOpstacles/RotateObstacle (13) | RotatingObstacle | (65.5, 12.5) | knockbackDir=(-1, 0); isCounterclockwise=true; rotationSpeed=60; random=true; randomRange=10 |
| Stage 9/RotateOpstacles/RotateObstacle (14) | RotatingObstacle | (69.5, 12.5) | knockbackDir=(-1, 0); isCounterclockwise=true; rotationSpeed=60; random=true; randomRange=10 |
| Stage 9/RotateOpstacles/RotateObstacle (15) | RotatingObstacle | (73.5, 12.5) | knockbackDir=(-1, 0); isCounterclockwise=true; rotationSpeed=60; random=true; randomRange=10 |
| Stage 9/RotateOpstacles/RotateObstacle (16) | RotatingObstacle | (77.5, 12.5) | knockbackDir=(-1, 0); isCounterclockwise=true; rotationSpeed=60; random=true; randomRange=10 |
| Stage 9/RotateOpstacles/RotateObstacle (23) | RotatingObstacle | (81.5, 12.5) | knockbackDir=(-1, 0); isCounterclockwise=true; rotationSpeed=60; random=true; randomRange=10 |
| Stage 9/RotateOpstacles/RotateObstacle (17) | RotatingObstacle | (63.5, 8.5) | knockbackDir=(-1, 0); isCounterclockwise=true; rotationSpeed=45; random=true; randomRange=10 |
| Stage 9/RotateOpstacles/RotateObstacle (18) | RotatingObstacle | (67.5, 8.5) | knockbackDir=(-1, 0); isCounterclockwise=true; rotationSpeed=45; random=true; randomRange=10 |
| Stage 9/RotateOpstacles/RotateObstacle (19) | RotatingObstacle | (71.5, 8.5) | knockbackDir=(-1, 0); isCounterclockwise=true; rotationSpeed=45; random=true; randomRange=10 |
| Stage 9/RotateOpstacles/RotateObstacle (20) | RotatingObstacle | (75.5, 8.5) | knockbackDir=(-1, 0); isCounterclockwise=true; rotationSpeed=45; random=true; randomRange=10 |
| Stage 9/RotateOpstacles/RotateObstacle (21) | RotatingObstacle | (79.5, 8.5) | knockbackDir=(-1, 0); isCounterclockwise=true; rotationSpeed=45; random=true; randomRange=10 |
| Stage 9/RotateOpstacles/RotateObstacle (22) | RotatingObstacle | (83.5, 8.5) | knockbackDir=(-1, 0); isCounterclockwise=true; rotationSpeed=45; random=true; randomRange=10 |
| Stage 9/DoorSwitchs/PlayerDoorSwitch | DoorSwitch | (56.5, 5.55) | offsetApply=false; collisionMask=mask0; onSwitchSprite=PlayerSwitch_1; offSwitchSprite=PlayerSwitch_0; activationLayer=mask2048; door=Door |
| Stage 9/DoorSwitchs/PlayerDoorSwitch (1) | DoorSwitch | (83.5, 3.5) | offsetApply=false; collisionMask=mask0; onSwitchSprite=PlayerSwitch_1; offSwitchSprite=PlayerSwitch_0; activationLayer=mask2048; door=Door |
| Stage 9/DoorSwitchs/Door/PlayerDoorSwitch (2) | DoorSwitch | (86.5, 10.5) | offsetApply=false; collisionMask=mask0; onSwitchSprite=PlayerSwitch_1; offSwitchSprite=PlayerSwitch_0; activationLayer=mask2048; door=Door |
| Stage 9/DoorSwitchs/Door (1)/BoxDoorSwitch | DoorSwitch | (93.5, 3.5) | offsetApply=false; collisionMask=mask0; onSwitchSprite=Switch_1; offSwitchSprite=Switch_0; activationLayer=mask256; door=Door (1) |
| Stage 9/Boxes/Box Switch Prefab/Box Switch Set | ObjSummonSwitch | (2, -1.5) | summonLocation=Box Respawn; cooldownTime=5; targetObjTrans=Box; defaultSprite=Switch_0; pressedSprite=Switch_1 |
| Stage 9/Boxes/Box Switch Prefab/Box | PickupObj | (9, 0) | offsetApply=false; collisionMask=mask3520; maxSlopeAngle=45; gravity=9.8; velocity=(0, 0); wallSlideSpeedMax=3 |
| Stage 9/RespawnPoints/RespawnPoint  | SavePoint | (3.5, -1.27) | savePointID=0 |
| Stage 9/RespawnPoints/SecondRespawnPoint  | SavePoint | (53.5, 5.55) | savePointID=0 |

## Switch.prefab

| 경로 | 스크립트 | 위치 | 값 |
|---|---|---|---|
| Switch | PlayerSummonSwitch | (-6.51, 9.5) | summonLocation=Switch; cooldownTime=5; defaultSprite=Switch_0; pressedSprite=Switch_1 |
