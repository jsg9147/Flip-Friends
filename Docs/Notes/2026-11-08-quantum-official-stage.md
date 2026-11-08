# Quantum 공식 스테이지 변환 경로 첫 묶음

- 상태: Historical. 아래는 제품7 당시 기록이다. 현재 경사 지원·제품8·공식 시험2종은 [경사 기록](2026-11-08-quantum-slopes.md)을 따른다.

- 제품7/ff-product-7. Unity 6000.6.8f1, Quantum 3.0.14 Stable 2241(버전 변경 없음). 공식 맵은 Unity 프리팹(Tilemap)과 Quantum 작성 전용 컴포넌트로 만들고 작성기가 변환한다. MapEditor·MapData 규칙(x−50~50 등)은 그대로다.
- 이번 묶음은 경로와 시험 스테이지 하나다. 경사 이동·움직이는 지형·새 장치와 21개 스테이지 재구성은 하지 않았다.

## 공식 스테이지 만드는 법

1. 프리팹 루트에 `QuantumOfficialStage`(ID: 소문자·숫자·하이픈 1~48자, 표시 이름).
2. 지형: Ground 태그·레이어 Tilemap 하나 + TilemapCollider2D(Merge) + CompositeCollider2D(Outlines, 반경0) + Rigidbody2D(Kinematic). 다른 스크립트를 붙이지 않는다. 칠한 뒤 `Tilemap.CompressBounds`로 범위를 줄인다(원점이 범위에 남으면 렌더러 범위·추락 경계가 원점까지 늘어난다).
3. 배치: `QuantumStageSpawnPoint`(정확히 2개, 왼쪽→1P), `QuantumStageGoal`(크기, 1~16개), `QuantumStageBox`(0.9 고정, ≤64), `QuantumStageRope`(크기, 수직, ≤64).
4. 장치: `QuantumStageReset`(같은 오브젝트에 Tilemap 트리거가 있으면 사각 조각, 없으면 크기 사각 + SpriteRenderer; 목적지·플레이어/상자 감지·onlyBox), `QuantumStageCheckpoint`(ID, 크기, SpriteRenderer), `QuantumStageBoxSpawner`(대상 QuantumStageBox, 목적지, 쿨다운, 눌림 스프라이트, SpriteRenderer).
5. Project 창에서 프리팹 선택 → `Tools/Flip Friends/Quantum/선택한 공식 스테이지 변환`. 결과는 `Assets/QuantumUser/Resources/OfficialStages/<ID>/`(Stage·Terrain·Devices·Map), `Assets/00_Scenes/QuantumOfficialStages/<ID>.unity`, 공식 목록 `FlipFriendsOfficialStageCatalog`. 같은 ID를 다시 변환하면 자산을 제자리에서 갱신한다.

## 구조

- 공통 계약 `IQuantumMapContent`로 `QuantumCustomMapAsset`(MapData)과 `QuantumOfficialStageAsset`이 같은 `QuantumGameContent.Prepare`를 쓴다. RuntimeConfig 반영(`ApplyTo`/`ApplyFallBoundary`)과 초기 배치 검사(`ValidateBoxesAgainstSpawns`/`ValidateRespawnPlacement`)를 두 경로가 공유한다.
- Tilemap 판독·거부(`ReadTerrain`, `ReadTilemapReset`, 서명)를 `QuantumTilemapReader`로 분리했다. 6:6 작성 씬을 다시 읽어 저장된 윤곽·Reset·서명과 일치함을 확인했다(저장 없음).
- `QuantumOfficialStageConverter`가 FP 변환·정렬·해시를 만들고 기존 장치·지형 Apply를 그대로 호출한다. 합의는 `공식1:<해시>:제품7:…`. 저장 해시는 시작할 때마다 원본 값에서 다시 만들어 대조한다. SDK 맵은 정적 Collider 0개여야 한다(지형은 FP 윤곽으로만 전달).
- 추락 경계는 원본 Mirror 스테이지와 같은 `FallBoundary.TryCalculateStageBounds`(Sprite/Tilemap 렌더러 합집합 ±10, 위쪽 제외)다.
- 작성기 거부: 루트 표식 누락·중복, 잘못된 ID·빈 표시 이름, 허용 목록 밖 컴포넌트(Mirror·원본 장치 스크립트·BoxCollider 등)·누락 스크립트, Ground Tilemap ≠ 1, Ground·Reset Tilemap 밖의 Collider·Rigidbody2D, 비활성/회전/배율≠1 작성 컴포넌트, 스테이지 밖 목적지·소환 대상, 그리고 기존 지형·장치 규칙(8raw 경사, 상한, 교차, 열린 윤곽, 비유한 값 등).
- Main 준비 화면: 공식 목록이 있으면 `공식 스테이지 선택` 버튼 하나를 7번째로 두고, 누를 때마다 목록을 차례로 고른다. 기존 6개 사용례의 선택 번호(0~5)는 그대로다. 7개 버튼 182 간격, TMP overflow 0. `Logs/QuantumOfficialPrepare.png`.

## 발견·수정한 기존 결함 (제품7로 올린 이유)

- **미세 기울기 수평 벽:** 시험 스테이지 바닥 (121,−2raw)→(133,0)처럼 Composite가 만든 1~2raw 오르막이, 그 사이 높이에 선 플레이어에게 수평 벽으로 작동했다. 2P가 x≈126.6에서 속도 5.5로 계속 멈췄다(`FlipFriendsTerrain.Limit`은 벽으로 정확히 계산했고 겹침 짝홀 판정은 섬 수직 변을 지날 때부터 1raw 관통을 보고했다). 6:6 사용례 입력에서는 이 위치를 지나지 않아 드러나지 않았다.
- **수정:** `FlipFriendsMovementSystem.LimitHorizontalWithMicroStep` — 수평 이동이 막혔을 때만 위로 최대 8raw(작성기 허용 경사와 같은 값)까지 올라 다시 잰다. 더 멀리 가면 그 높이를 쓰고, 아니면 위치를 바꾸지 않는다. 실제 벽은 결과가 같다. 플레이어와 상자에 적용했다. 윤곽 지형이 없는 맵(null·빈 배열 모두)은 기존 코드 경로 그대로다. 경사 이동 구현이 아니다.
- 시뮬레이션 규칙이 바뀌었으므로 합의를 제품7/ff-product-7로 올리고 규칙 문자열에 `미세단차8raw;공식1`을 넣었다. 기존 검사의 제품6 단언을 제품7로 바꿨다.

## 시험 스테이지와 실행 검증 (2026-11-08)

- `Assets/03_Prefabs/OfficialStages/Official Test Stage.prefab`(ID official-test, 생성기 `QuantumOfficialTestStageSetup`). x 98~142, MapData 범위 밖. 윤곽 4개/22변(외곽·내부 빈 공간·속 빈 섬·섬 구멍), 2칸 구덩이 Reset Tilemap 2조각(목적지 112.5), 체크포인트 ID1, 상자 소환, 로프, 1칸 단 위 도착. SDK 정적 Collider 0, 추락 경계 X 88~152·Y −12.
- 정지 상태 콘텐츠 검사 `QuantumOfficialStageChecks.RunContent`: 합의 형식, 윤곽·장치·조각 수, MapData 범위 밖 배치, 목록 등록, 섬 구멍·구덩이 보존, 프리팹 재판독 해시, 배치 순서·fr-FR 문화권 독립 해시, 도착 이동 반영, 거부 13종 통과. `Logs/QuantumOfficialStageContent.txt`.
- 새 입력 1200틱: 상자 운반 Reset·체크포인트·전원 도착(완료 1061) 관측, 매 확정 프레임 몸체·지형 겹침 없음. 새 러너에서 1/30·1/120 교대 재생 확정 120~1200 체크섬 10개 일치, 확정1200=5026167679043117472. `Logs/QuantumOfficialStageReplay.json`, `…Trace.txt`, `…Checks.txt`.
- Main: 실제 Button 콜백 Start→공식 스테이지 선택→오프라인→운반 Reset·전원 도착→Main→재진입·메뉴 종료, 러너·입력 잔존 0, QuantumPlay Collider2D 0, Unity 오류 0. 픽셀 클릭·실제 두 사람 조작은 아니다.
- 기존 제품 회귀(제품7): `QuantumProductChecks.RunOffline` 통과(장치 재생 20·복귀 20·상호작용 12·이동 8·정적 6·로프 8, Main 장치/복귀/상호작용 플레이·실패/취소/중복/씬 왕복·잔존, 오류 0). 6:6 `QuantumTilemapChecks.RunStandalone` 통과, 확정1200=5927891891329800569로 직전 기록과 같다(미세 단차가 발동하지 않음). 기존 2:2~6:6 manifest는 모두 저장 해시로 검증된다.
- 개발 Photon 같은 PC 두 연결: 공식 시험 asia·RTT 107/104ms·확정 240~1200 9개 일치·양쪽 운반 Reset·완료 1127 일치·정리 True(확정1200=11289760593813285172). 6:6 재실행 asia·RTT 105/115ms·9개 일치·완료 1485 일치. 지연/손실 주입 없음. `Logs/QuantumOfficialStageCloud.txt`, `QuantumTilemapCloud.txt`.
- 지정 6000.6.3f1 번들 dotnet: 런타임 오류 0/경고 171, 에디터 오류 0/경고 7. 새 경고는 Inspector 전용 `[SerializeField]` 필드의 CS0649다.

## 실패 분석

- 첫 배치에서 상자가 2P 경로를 막았고 1P가 상자 대신 2P를 들었다(플레이어 우선 탐색). 상자를 1P 뒤로 옮겼다. 걷기 속도 점프로는 2칸 구덩이를 못 넘어 Reset이 반복됐다. 입력에 달리기를 켰다. Reset 목적지를 구덩이에서 떨어뜨려 도움닫기를 확보했다. 게임 규칙·단언은 완화하지 않았다.
- 첫 Photon 검사는 이 스테이지가 확정 1200 전에 끝나 비교 표본이 모자랐다. 완료와 확정 1200을 함께 기다리게 했다.

## 미실행

- 다른 PC, 실제 2~4인 조작, 게임패드, 배포 빌드, Steam 다계정·출시 인증, 지연·손실 주입. 기존 Mirror·MapEditor·친구 맵·Steam 로비는 그대로다.
