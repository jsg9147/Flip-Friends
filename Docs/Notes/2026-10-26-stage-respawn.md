# 단계 2 리셋 경로 조사 (2026-10-26)

- 작업 전 기준선: `Temp/StagePhase2/preexisting.diff`, 전체 21개 참조 검사 `before.json`(39개). Temp 파일은 커밋 대상이 아니다.
- 기존 이중 호출: PlayerController2D의 소유자 Reset 접촉 → CmdPositionReset → 체크포인트 복귀와 RespawnHandler의 서버 접촉 → 구간 Teleport가 경쟁했다. 상자도 PickupObj 접촉과 RespawnHandler RPC가 각각 위치를 바꿨다.
- onlyBoxReset: Stage 10 두 곳·12 두 곳, 모두 Player 마스크(2048). 기존 의미는 상자를 든 플레이어만 구간 이동하며 상자를 계속 든다는 것이다. 이름과 달리 상자만 이동시키는 옵션이 아니다. 기존 PickupObj 자체 Reset 접촉은 마스크를 무시하므로 Reset 태그 지형의 상자 처리는 계속 허용한다.
- 리셋 키·추락은 DropAll 후 체크포인트(없으면 최초 생성 위치), 구간 이동은 운반 유지, 상자 추락은 최초 배치 위치, 소환은 해당 스위치의 summonLocation이다.
- 기본 스테이지 상자는 자식 NetworkIdentity가 없다. PlayerInteraction의 GameObject RPC 인자는 Mirror에서 직렬화 경고가 발생했으므로 NetworkBehaviour 참조(루트 netId + 부품 인덱스)로 변경했다.

## Stage 8 조사와 사용자 확정

- Unity PrefabUtility.LoadPrefabContents에서 Grid/ResetTile의 사용 타일 종류 0, 실제 GetTile 검사도 0. TilemapCollider2D bounds 크기도 0이다. cellBounds는 이전 편집 흔적으로 `(0,-3)`~`(78,5)`가 남아 있어 실제 충돌 구간 근거로 쓸 수 없다.
- 첫 후보: `Box Switch Prefab/Box Respawn` (3,1). 같은 부모의 첫 상자와 스위치가 이 위치를 사용한다.
- 둘째 후보: `Box Switch Prefab (1)/Box Respawn` (5,26). 스위치 (0,23.75)가 같은 부모의 중간 상자를 소환한다. 별도 OnlyObj 지형은 y=23에 있다.
- 셋째 후보: `Box Switch Prefab (2)/Box Respawn` (7,53). 스위치 (10,52.5)가 같은 부모의 상단 상자를 소환한다.
- 수직 지형은 대략 y=-3~80으로 세 구간에 걸친다. 빈 ResetTile만으로 목적지를 확정할 수 없어 사용자에게 질문했다. 이후 사용자가 불필요한 배치이므로 제거하도록 확정했다. Unity API로 Grid/ResetTile 오브젝트를 제거하고 저장·재로드를 확인했다. 상자 세 구간은 유지했다.

## 참조·ID 재검사

- RespawnHandler 13개: Stage 7~12, Reset 태그/Reset 레이어(15), 마스크 2048 또는 2304를 유지한다. 조사 시 목적지 누락은 Stage 8 한 곳이었으며 불필요한 오브젝트 제거 후 RespawnHandler는 12개다.
- 상자 9개는 모두 Pickable(8), 소환 스위치 9개의 대상은 모두 PickupObj이고 각 소환 위치 참조가 존재한다. 표시 스프라이트 필수 참조도 누락이 없다.
- SavePoint 14개는 트리거이며 ID는 음수가 없다. Stage 9(0,0), 16(0,1,1), 18(0,0), 19(0,0)에 중복 ID가 있다. 기존 ID를 바꾸면 진행 의도가 달라질 수 있어 보존했다. 동일 ID는 처음 도달한 체크포인트를 유지한다.
- 맵 에디터 팔레트 interactive.box는 `Assets/03_Prefabs/Box.prefab`을 직접 사용하며 루트 NetworkIdentity 1개·Pickable 레이어다. 새 서버 API의 적용 대상이다.


