# 전체 스테이지 연결 점검 및 클래스 분석
- 상태: 조사 기록
- 이 문서는 수정 전 기준선이다. 단계 0·1 이후 현재 상태와 검증은 `../STAGE_COMPONENTS.md`를 따른다.
- 기준: 2026-10-22 KST, Unity 6000.6.3f1에서 읽은 저장된 Stage 1~21 프리팹.
- 현재 상태: 조사만 수행했다. 이번 요청에서는 프리팹·게임 코드를 추가 수정하지 않았다. Stage 11은 직전 요청에서 연결한 상태를 검사했다.
- 검증: 비활성 자식·중첩 프리팹을 포함한 컴포넌트 및 직렬화 참조 검사, 주요 사용 코드 대조. 실제 플레이·호스트/원격 클라이언트 동작은 미검증.
- 원시 근거: [stage-audit-data.json](2026-10-22-stage-audit-data.json). Unity 에디터 조회 결과이며 TMP의 정상적인 선택 참조는 원시 목록에서 제외했다.

## 핵심 결과
- 기본 맵 카탈로그 21개 모두 올바른 Stage 프리팹을 참조한다.
- 전 스테이지 Missing Script 0개, 루트 NetworkIdentity 1개씩, 중첩 NetworkIdentity 0개, Finish 태그 오브젝트 1개씩이다.
- 직접 부착된 프로젝트 클래스는 15종이다. 상속 클래스는 별도 컴포넌트 수로 중복 집계하지 않았다.
- 필수 참조 누락 또는 목록 불완전: Stage 8, 12~14, 15~21. Stage 11은 직전 수정 후 연결 정상이다.
- OnOffSwitch → 매니저 참조는 60개 모두 존재한다. 반대 방향의 매니저 목록에서 스위치 46개가 빠져 있다(전체 누락 8개 매니저, 부분 누락 1개).
- Conveyor 27개 중 매니저 목록에 등록된 것은 11개, 미등록은 16개다. 미등록 벨트의 방향을 고정할 의도가 있었는지는 레벨 설계 확인이 필요하다.
- Stage 21 자동 매니저의 conveyorBelts에 명시적인 null 항목 4개가 있다. 코드는 null을 건너뛰므로 이 항목 자체로 예외가 나지는 않는다.

## 스테이지별 현재 상태
1. Stage 1: DynamicObj 1, MovingTrap 4. 검사 범위에서 연결 누락 없음.
2. Stage 2: DynamicObj 1, MovingTrap 5. 검사 범위에서 연결 누락 없음.
3. Stage 3: DynamicObj 1, MovingTrap 7. 검사 범위에서 연결 누락 없음.
4. Stage 4: DynamicObj 1, CannonShooter 11. 검사 범위에서 연결 누락 없음.
5. Stage 5: DynamicObj 1, CannonShooter 2, RotatingObstacle 4. 검사 범위에서 연결 누락 없음.
6. Stage 6: DynamicObj 1, MovingTrap 12, CannonShooter 7, RotatingObstacle 3. 검사 범위에서 연결 누락 없음.
7. Stage 7: MovingTrap 3, ObjSummonSwitch 1, PickupObj 1, RespawnHandler 1, DoorSwitch 1. 검사 범위에서 연결 누락 없음.
8. Stage 8: MovingTrap 6, CannonShooter 8, RotatingObstacle 1, ObjSummonSwitch 3, PickupObj 3, RespawnHandler 1, DoorSwitch 4. Grid/ResetTile.resetPoint가 null이다.
9. Stage 9: MovingTrap 2, CannonShooter 1, RotatingObstacle 20, ObjSummonSwitch 1, PickupObj 1, RespawnHandler 2, DoorSwitch 4, SavePoint 2. 검사 범위에서 연결 누락 없음.
10. Stage 10: MovingTrap 6, CannonShooter 3, RotatingObstacle 6, ObjSummonSwitch 1, PickupObj 1, RespawnHandler 2, DoorSwitch 1. 검사 범위에서 연결 누락 없음.
11. Stage 11: MovingTrap 5, CannonShooter 3, RotatingObstacle 2, RespawnHandler 3, OnOffManager 1, OnOffSwitch 11, PlayerDetectPlatform 1. 스위치 11개 및 전환 지형 2개 연결 정상.
12. Stage 12: MovingTrap 15, RespawnHandler 4, PlatformController 2, DoorSwitch 2, OnOffManager 1, OnOffSwitch 8, ObjSummonSwitch 1, PickupObj 1. 매니저 switches 0/8, 지형 참조 2개 null. 일반 이동 발판 2개의 passengerMask=0.
13. Stage 13: OnOffManager 1, OnOffSwitch 2, PlayerDetectPlatform 20. 매니저 switches 0/2, 지형 참조 2개 null.
14. Stage 14: MovingTrap 6, OnOffManager 1, OnOffSwitch 4, PlayerDetectPlatform 22. 매니저 switches 0/4, 지형 참조 2개 null.
15. Stage 15: MovingTrap 5, CannonShooter 8, PlayerDetectPlatform 9, SavePoint 2, DoorSwitch 4, DetectPlatformResetBtn 1. 리셋 버튼 대상·스프라이트·감지 레이어 누락. 발판 spriteRenderer 1개 null. 상위 문 그룹 2개에 불완전한 DoorSwitch 부착.
16. Stage 16: MovingTrap 7, SavePoint 3, OnOffManager 1, OnOffSwitch 6, PlayerDetectPlatform 18, DetectPlatformResetBtn 1, DoorSwitch 6. 매니저 switches 0/6, 지형 참조 2개 null. 리셋 버튼 1개 불완전, 발판 spriteRenderer 1개 null, 상위 문 그룹 3개에 불완전한 DoorSwitch 부착.
17. Stage 17: MovingTrap 7, CannonShooter 1, RotatingObstacle 1, SavePoint 1, OnOffManager 1, OnOffSwitch 4, PlayerDetectPlatform 5, DetectPlatformResetBtn 2, DoorSwitch 4. 매니저 switches 0/4, 지형 참조 2개 null. 리셋 버튼 2개 불완전, 발판 spriteRenderer 2개 null, 상위 문 그룹 2개에 불완전한 DoorSwitch 부착.
18. Stage 18: MovingTrap 4, CannonShooter 2, OnOffManager 1, OnOffSwitch 8, PlayerDetectPlatform 5, DoorSwitch 4, SavePoint 2, Conveyor 5. 매니저 switches 0/8, conveyorBelts 0/5. 상위 문 그룹 2개에 불완전한 DoorSwitch 부착. 전환 지형 참조는 null이나 타일맵 자체가 비어 있다.
19. Stage 19: MovingTrap 7, ObjSummonSwitch 1, PickupObj 1, OnOffManager 1, OnOffSwitch 12, PlayerDetectPlatform 6, DoorSwitch 2, SavePoint 2, Conveyor 8. 매니저 switches 0/12, conveyorBelts 0/8. 상위 문 그룹 1개에 불완전한 DoorSwitch 부착. 전환 지형 자체가 없으므로 지형 null은 정상.
20. Stage 20: MovingTrap 2, CannonShooter 22, SavePoint 1, OnOffManager 2, OnOffSwitch 2, Conveyor 10, PlatformController 3, PlayerDetectPlatform 1. Auto 매니저는 스위치 1개·벨트 8개 연결. 다른 매니저는 스위치 0/1·벨트 0개, 두 매니저 모두 자동 토글=true. 벨트 (8), (9)는 어느 매니저에도 미등록. 전환 지형 없음. 일반 발판 3개 passengerMask=0.
21. Stage 21: MovingTrap 1, CannonShooter 1, ObjSummonSwitch 1, PickupObj 1, SavePoint 1, OnOffManager 2, OnOffSwitch 3, Conveyor 4, PlayerDetectPlatform 1, DetectPlatformResetBtn 1. Auto 매니저는 스위치 1개·유효 벨트 3개·null 벨트 4개. 다른 매니저 목록은 스위치 1/2로 이동 벨트 위 스위치가 빠짐. 벨트 (6)는 매니저 미등록, passengerMask=0. 두 매니저 모두 자동 토글=true. 리셋 대상 참조는 정상.

## 누락의 실제 영향과 판단
### 우선 수정할 항목
- Stage 8 Grid/ResetTile.resetPoint: Player와 Pickable 레이어를 감지하는 활성 컴포넌트다. RespawnHandler.ResetTarget/RpcPositionReset 및 PickupObj의 Reset 진입 코드가 resetPoint.position을 바로 읽으므로 접촉 시 NullReferenceException 위험이 있다. Box Respawn 후보가 3곳이라 목적지를 추측해서 연결하면 안 된다.
- Stage 12~14·16~20 OnOffManager.switches: 매니저를 가리키는 스위치가 있지만 반대 목록은 비어 있다. 다른 스위치의 표시 갱신 및 자동 토글 표시 전파가 빠진다. Stage 21은 목록 한 항목 추가가 필요하다.
- Stage 18·19 conveyorBelts: 스위치를 눌러도 벨트에 UpdateConveyorState가 전달되지 않는다. Stage 20의 2개 및 Stage 21의 1개 미등록 벨트는 고정 방향 의도인지 확인해야 한다.
- Stage 15·16 각 1개, Stage 17 2개의 Platform Reset: detectPlatform, onSwitchSprite, offSwitchSprite가 null이고 activationLayer=0이다. BoxCollider2D.isTrigger도 false다. 현재 설정은 LayerBasedSwitch의 트리거 감지 방식과 맞지 않는다. 트리거 콜백을 받으면 자식 코드가 레이어 검사 결과와 무관하게 RPC를 보내 null 대상에 접근할 수 있다.
- 위 버튼과 같은 그룹의 Platform이 가장 유력한 리셋 대상이지만, 연결뿐 아니라 버튼의 감지 방식·스프라이트 설정도 함께 정해야 한다.

### 자동 보완 또는 설계 확인 항목
- Stage 12·13·14·16·17의 전환 지형: Inspector 참조는 비었지만 실제 타일이 있고 매니저가 하나여서 Awake의 이름 검색으로 실행 시 보완된다. 명시적으로 연결해 두는 것이 바람직하나 지형 토글 완전 불능으로 분류하지 않았다.
- Stage 15·18·21의 OnSwitchGround/OffSwitchGround는 사용 타일 종류가 0개다. Stage 15에 매니저가 없다는 것 자체를 기능 누락으로 단정할 수 없다.
- Stage 19·20에는 전환 지형 자체가 없다. 두 지형 필드 null은 정상적인 선택 값이다.
- Stage 21은 매니저가 2개라 지형 자동 탐색이 실행되지 않지만 현재 두 타일맵이 비어 있어 영향은 없다.
- PlayerDetectPlatform.spriteRenderer null: Stage 15 1개, 16 1개, 17 2개, 20 1개, 21 1개. 모두 disableColliderWhenFalling=false이고 코드에 null 방어가 있어 즉시 예외는 없다. 해당 옵션을 켜면 페이드·색 복원 연출이 빠진다.
- Stage 12 일반 발판 2개, Stage 20 일반 발판 3개, Stage 21 감지 발판 1개의 passengerMask=0: 앞의 5개는 플레이어를 운반하지 않는 장애물 의도일 수 있다. 21번 감지 발판은 DetectPlayer가 같은 마스크를 쓰므로 현재 설정으로 플레이어를 감지하지 못한다.
- Stage 15~19의 상위 Switch Door 그룹에 DoorSwitch 10개가 부착되어 있으나 로컬 SpriteRenderer가 없다(2/3/2/2/1개). 자식에는 별도의 실제 DoorSwitch가 있다. 상위 콜라이더는 isTrigger=false라 일반 트리거 버튼으로 쓰기에도 맞지 않는다. 콜백이 발생하면 RpcOnSwitchSpriteChange에서 null 접근 가능성이 있으며, 불필요한 중복 스크립트인지 확인해야 한다.
- TMP의 선택 머티리얼·스프라이트·스타일 필드, 컨베이어가 없는 매니저의 빈 conveyorBelts는 결함으로 집계하지 않았다.

## 스테이지를 구성하는 클래스
### 로딩·공통 기반
- BuiltInMapCatalog (PlayingScripts): mapId와 스테이지 프리팹을 연결하는 ScriptableObject. 21개 등록 모두 확인.
- StageManager (PlayingScripts): GamePlay 씬의 로더. 카탈로그 조회 → Instantiate → NetworkServer.Spawn → FallBoundary 계산. 스테이지 프리팹 안에 붙는 컴포넌트는 아니다.
- GameManager (01_Scripts 루트): 모든 플레이어의 완료 상태를 확인하고 대기실로 복귀. 스테이지 안의 Finish 태그는 PlayerController2D가 처리한다.
- FallBoundary (PlayingScripts): 렌더러 범위로 스테이지 밖 추락을 판정하는 일반 C# 객체. 플레이어·상자 리스폰에 사용한다.
- Mirror.NetworkIdentity: 21개 스테이지 루트의 네트워크 식별자. 자식 장치들은 같은 루트에 소속된다.
- StagePartBehaviour (ObstacleScripts): NetworkBehaviour 기반. 독립 부품 프리팹 편집 시 루트 NetworkIdentity가 없는 경우의 Mirror 검사를 보완한다.
- RaycastController (PlayerScripts/New Scripts): BoxCollider2D와 레이 원점·간격·충돌 마스크를 제공한다. 스위치·발판·상자의 공통 기반이다.
- Grid, Tilemap, TilemapRenderer, TilemapCollider2D, CompositeCollider2D, Rigidbody2D: 지형 표현과 2D 충돌 구성. SpriteRenderer·Animator는 장치 표현을 담당한다.

### 장애물·벨트
- BasicTrap (ObstacleScripts): 넉백 방향을 가진 StagePartBehaviour 기반 클래스. 직접 부착 수 0, MovingTrap/RotatingObstacle의 부모다.
- MovingTrap (104개): 시작 시각과 경로를 기준으로 왕복 이동 및 선택 회전. 서버·클라이언트가 시간을 바탕으로 계산한다.
- CannonShooter (69개): 서버가 투사체 프리팹을 생성·스폰하고 속도와 수명 범위를 설정한다.
- RotatingObstacle (37개): 서버에서 회전하며 NetworkTransformUnreliable로 회전을 전달한다.
- Conveyor (27개): 벨트 애니메이션·방향·색상 상태를 관리한다. 플레이어 가속 처리는 MovementHandler가 isClockwise를 읽어 수행한다.

### 퍼즐·스위치
- Switch: isActivated SyncVar, 스프라이트 및 머리로 치는 레이 감지 기반.
- LayerBasedSwitch: 활성 레이어와 트리거 진입/이탈로 토글한다. DoorSwitch와 DetectPlatformResetBtn의 부모다.
- OnOffSwitch (60개): 서버에서 아래쪽 플레이어 감지 후 매니저에 상태 변경을 알린다.
- OnOffManager (12개): 소속 스위치 표시, 전환 지형, 컨베이어 방향을 일괄 변경하고 선택적으로 자동 토글한다.
- DoorSwitch (32개): 대상 door의 SpriteRenderer와 Collider2D를 켜고 끈다. 32개 중 10개는 앞서 언급한 상위 그룹의 불완전 컴포넌트다.
- ObjSummonSwitch (9개): targetObjTrans를 summonLocation으로 되돌리는 장치. 이름과 일부 주석은 소환이지만 실제 동작은 지정 오브젝트 이동이다.

### 발판·진행·오브젝트
- PlatformController (직접 부착 5개): 경유점 이동 및 승객 운반. PlayerDetectPlatform의 부모이며 자식 88개를 이 수에 더하지 않았다.
- PlayerDetectPlatform (88개): 플레이어 감지 후 이동·낙하·충돌 해제·복원을 제어한다.
- DetectPlatformResetBtn (5개): 지정 PlayerDetectPlatform의 내부 상태를 리셋한다. 15~17번의 4개는 대상 미지정, 21번의 1개는 대상 지정.
- PickupObj (9개): 레이 기반 상자 이동·운반 상태·위치 동기화·스테이지 밖 복귀.
- ObjSummonSwitch와 PickupObj는 같은 스테이지에 각 9개 배치되어 있으며 상자 재소환 퍼즐을 구성한다.
- RespawnHandler (13개): 특정 레이어가 구간 리셋 지형에 닿으면 resetPoint로 이동시킨다.
- SavePoint (14개): 플레이어가 체크포인트에 접촉하면 저장 지점을 설정한다.
- DynamicObj (6개): Stage 1~6에서 인원수에 따라 오브젝트 시작 위치를 조정한다.
- Mirror.NetworkTransformReliable/Unreliable, TMPro.TextMeshPro는 위치·회전 동기화 및 장치 설명 텍스트를 구성한다.

## 참조 연결과 별개로 발견한 코드 문제
- OnOffManager.ToggleOnOffState는 소속 스위치의 RpcToggleSwitch만 호출한다. 해당 RPC는 그림만 바꾸며 Switch.isActivated는 설정하지 않는다.
- 예: A와 B가 false일 때 A를 눌러 매니저를 true로 바꿔도 B 내부 상태는 false다. 이어 B를 누르면 false→true가 되어 매니저에 또 true를 전달하므로 기대한 반전이 일어나지 않는다. Stage 11도 참조 연결만으로 이 문제는 해결되지 않는다.
- 자동 토글도 개별 스위치 내부 상태를 갱신하지 않는다. 해결 시 서버에서 상태를 일괄 설정하고 SyncVar hook으로 표시를 맞추는 흐름을 검토해야 한다.
- DetectPlatformResetBtn의 OnTriggerEnter2D는 부모의 레이어 판정 결과와 무관하게 서버에서 리셋 RPC를 호출한다. 대상 연결 후에도 감지 조건을 정리할 필요가 있다.

## 검증과 한계
- 에디터 읽기 검사로 저장된 참조·객체 구성·컴포넌트 요구사항을 대조했다. 끝점이 2개 미만인 이동 발판, 비어 있는 AnimatorController, SpriteRenderer.sprite, 문 대상의 렌더러/콜라이더 누락은 검사 범위에서 발견하지 못했다.
- 활성 여부는 프리팹 에셋의 activeInHierarchy가 아니라 모든 조상의 activeSelf를 따라 판정했다. 프리팹 에셋의 activeInHierarchy는 false를 반환할 수 있다.
- 완료 시 Unity Console error 조회 0건. 오류를 재현하기 위해 게임을 실행하거나 네트워크 세션을 시작하지는 않았다.
- 충돌 배치의 적정성, 난이도, 전체 경로 클리어 가능성, 호스트/원격 클라이언트 동작은 이 정적 검사로 보증하지 않는다.
- 코드·프리팹은 변경하지 않았고 조사 기록만 추가했다.

## 다음 작업
- Stage 8 리스폰 목적지와 Stage 20·21의 벨트 제어 의도를 정한 뒤 필수 참조를 연결한다.
- On/Off의 서버 내부 상태 일괄 갱신을 수정하고, 서로 다른 스위치를 연속으로 누르는 경우 및 자동 토글 후 누르는 경우를 검증한다.
- 발판 리셋 버튼의 감지 조건·대상·스프라이트와 상위 문 그룹의 중복 DoorSwitch를 정리한다.
