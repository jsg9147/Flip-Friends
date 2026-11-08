# Quantum 정적 Tilemap 윤곽·다중 조각 Reset 첫 묶음

## 범위와 보존

- 제품6/ff-product-6. 새 Main 여섯 번째 `Tilemap 지형/Reset`은 **Stage1 Ground와 Stage9 First ResetTile 사용례를 조합한 별도 맵**이다. Stage1 또는 Stage9 전체를 지원한다는 뜻이 아니다. 전체21개 스테이지 전환은 미완료다.
- 실제 Unity6000.6.8f1, Quantum3.0.14 Stable2241을 유지했다. Main loaded/active/dirty=true·정지에서 시작했으며 기존 미커밋/미추적 파일·미저장 Main·Mirror 경로·원본 스테이지/프리팹을 보존한다. 자동 커밋·GUID 수동 작성·씬 YAML 편집은 하지 않았다.
- 셸 샌드박스가 네트워크 격리 초기화 오류로 실행되지 않아 외부 실행 승인을 사용했다. 연결 에디터는 Pipeline 포트7801에서 ready였으며 에디터 API로 작성 씬·콘텐츠·SDK 맵·manifest를 저장·재로드했다. 제품 SimulationConfig 자동 씬 로딩 Disabled와 QuantumGameFlow 소유를 유지했다.

## 21개 원본 조사

- 21개 프리팹에서 Tilemap111개, TilemapCollider2D98개, CompositeCollider2D46개, Box507개, Polygon4개, Circle55개를 확인했다(비활성 포함). 사용 타일 에셋33종이다. 원본 상세는 Git 제외 `Logs/QuantumTilemapBaseline.json`, `QuantumTilemapShapes.json`, `QuantumTilemapTileAssets.json`이다.
- 지형은 Ground 태그/레이어, Tilemap Merge+Kinematic Composite Outlines가 중심이다. Ground Tilemap의 조사된 Transform은 회전0·배율1, 위치(0,0)/(3,0)/(20,0)/(3,1)이다. On/Off 전환 지형·비활성 빈 Tilemap도 있어 Collider 종류만 보고 정적 지형으로 일괄 수용하지 않는다.
- 프리팹 인스턴스에서 ProcessTilemapChanges·GenerateGeometry 후 GetShapes로 실제 생성 형상과 렌더러 범위를 읽었다. 원본 플레이어는 Controller2D 레이·ResolvePenetration/CanOccupy 경로이며 Rigidbody의 일반 충돌 이동이 아니다. Ground Outline은 외곽·빈 공간 경계를 모두 갖는다.
- Ground의 스테이지별 생성 경로 수: 1=7, 2=9, 3=34, 4=15, 5=5, 6=8, 7=4, 8=7, 9=10, 10=5, 11=14, 12=7, 13=2, 14=2, 15=8, 16=5, 17=5, 18=12, 19=33, 20=7, 21=1. 이는 변환/플레이 통과 수치가 아니다.
- 겉보기 직각인 Composite 꼭짓점에도 약0.00003의 생성 기울기가 있다. 예: Stage1 외곽(20.9999714,-4)→(20.9999714,61)→(-21,60.99997) 등이다. 박스/AABB로 채우거나 정수 격자로 스냅하면 원본 구멍과 경계가 달라져 채택하지 않았다.
- Reset은 지형과 분리된 Tilemap Polygon 트리거다. 실제 RespawnHandler는 Stage7의2304(Player+Pickable), Stage9~12의2048(Player), onlyBox는 Stage10과12 일부다. Reset 태그 상자의 직접 접촉 의미는 기존 장치 정책을 따른다. Stage15~20 등의 Reset 태그 Tilemap은 핸들러가 없는 경우도 있으므로 이름만으로 목적지/효과를 만들어내지 않는다.
- Stage1에는 Through/OneWay와 이동 함정, Stage9에는 로프·통과 지형 등 미지원 요소가 있다. 이를 빠뜨린 전체 스테이지를 만들지 않고 별도 사용례 맵으로 제한했다.

## 선택한 지원과 결정론적 콘텐츠

- 지원 지형: 원본 정적 Ground Tilemap Merge+Kinematic Composite Outline, 반경0·닫힌 비교차 윤곽, XY 평면. 첫 맵은 Stage1 Ground 전체7윤곽을 끝점 중복만 제거해28변으로 옮겼다. 각 꼭짓점을 기존 Q 규칙(16비트 소수 반올림)으로 변환하며 구멍·섬·생성 미세 기울기를 보존한다.
- 본격적인 경사 이동은 미지원이다. 변의 양 축 변화 중 작은 값이8raw(8/65536)를 넘으면 위치/원인과 함께 거부한다. 이는 생성 오차를 지우는 스냅/완화가 아니다. 수용한 변도 실제 FP 양 끝점을 그대로 사용한다. 원형·둥근 모서리·열린/교차/접촉 윤곽·동적/전환 지형·일반 장치·통과 지형은 이번 묶음 밖이다.
- 지원 Reset: 원본 비합성 정적 Tilemap 트리거의 실제 생성 Polygon이 축 정렬 사각인 경우다. Stage9 `Grid/First ResetTile`의26조각, Transform(3,1), 마스크2048, onlyBox=false, 원본 목적지(3.5,-1.27)를 보존했다. 별도 박스로 재작성하거나 타일 경계/빈 구간을 합쳐 채우지 않았다. 여러 Collider를 한 장치로 작성하는 구성은 이번 작성기가 거부한다.
- 안정 식별: 지형은 원본 stage/path+정규화 꼭짓점 해시다. 경로의 시작점/방향과 생성 열거 순서를 정규화한다. Reset 조각은 장치ID+정렬된 FP 중심/크기 키가 식별자이며 한 장치의 접촉 합집합으로 판정한다. instanceID나 런타임 탐색 순서를 콘텐츠 식별에 사용하지 않는다.
- MapData2.1과 기존2:2/3:3/4:4/5:5 원본·베이크·해시를 보존했다. 새 콘텐츠만6:6이며 새 별도 QuantumMapTerrain/QuantumMapDevices가 FP 콘텐츠를 제공한다. SDK 맵은 기존 박스 베이크를 검증하고 윤곽 지형은 검증된 RuntimeConfig 배열로 전달한다. 시뮬레이션의 Unity Physics2D/Transform/Tilemap/렌더러/시간 조회는 없다.
- 원본 타일 GUID/셀/Collider 종류, 작성 Transform·합성 설정·Reset 마스크/목적지 참조·형상·목적지/조건·렌더러 범위·합친 추락 경계를 canonical 해시에 반영한다. 새 제품 AppVersion과 콘텐츠 변환/팔레트 버전은 별개다. 기존 맵도 새 제품 규칙으로 시작 위치/경계를 재검증한다.
- 지형은 최대64윤곽/512변, 장치32개, 장치별256조각/총512조각, 상자64개다. 누락/중복 ID·중복 꼭짓점/조각·면적0·교차·미지원 경사/Collider·경계 밖 형상·초기 겹침·막힌 목적지·비유한 값·과대 콘텐츠는 시작 전에 거부한다. 검증을 마치기 전에 제품 러너나 부분 맵을 생성하지 않는다.
- 새 맵에는 별도 시작점2개·상자·도착점과 기존 박스 베이크 경로 검사용 지형1개도 있다. 원본 사용례의 값은 바꾸지 않았다. 일반 MapEditor 작성/친구 전송·원본 전체 스테이지를 이식한 것은 아니다.

## 판정과 관련 결함

- 새 지형은 FP 변과 AABB의 열린 가로/세로 구간을 잘라 연속 축 이동을 제한하고, 변 교차+짝홀 내부 판정으로 점유를 검사한다. 구멍을 채우지 않으며 타일 단위 내부 이음새 Collider를 만들지 않는다. 기존 박스 SDK 레이는 보존하고 새 지형 제한을 결합했다. 놓기/던지기·Shrink 복원·복귀/소환의 점유도 같은 윤곽을 검사한다.
- **기존 결함 수정:** 운반자가 Shrink일 때 기존 `position+(0,1)+운반자 offset/half` 대리 판정은 실제 들린 상자/플레이어의 윗부분을 누락했다. 원본 수치에서 carrier y58.3, 상자 offset0.95/half0.45, 천장60, 상승0.5로 옛 대리 판정과 실제 대상 제한 차이를 재현했다. 실제 HeldOffset과 대상의 HalfSize/Offset을 사용하도록 수정했다. 이동 수치·운반 위치/탈출/해제 규칙은 유지한다.
- **전환 중 위험 수정:** Reset 조각마다 진입 이력을 두면 인접 조각을 넘어갈 때 재Reset될 수 있다. 장치당 Player/Box 비트 이력 하나로 합집합 접촉을 소비한다. 같은 틱 다중 접촉·반복 호출은 복귀1회이며 기존 목적지 대기/운반 원자 복귀·onlyBox 정책을 유지한다.
- **전환 중 호환 결함 수정:** 추가한 배열의 null이 SDK/JsonUtility 직렬화 후 빈 배열로 돌아왔다. 기존5:5 장치는 두 배열의 길이0을 기존 단일 박스 판정으로 해석하고, 새 Tilemap 작성 서명이 있는 콘텐츠의 누락 조각은 거부한다. 기존 체크포인트 다중 접촉 단언이 이를 검출했으며 단언은 보존했다.
- **의도된 동작/별도 미지원:** 본격적인 경사 오르내림·wallSliding/wallDirX·일반 BlockJump 확대는 추가하지 않았다. 새 지형의 AABB 연속 제한은 기존 테스트 지형의3줄 레이와 구현 방식이 다르며 본격적인 경사 조작 호환을 주장하지 않는다.

## 실행 검증

- 최종 새 입력1200틱을 기록하고 새 러너에서 서비스 간격1/30·1/120초를 번갈아 재생했다(시뮬레이션은60Hz 유지). 확정120~1200/120간격10개 체크섬이 일치했다. 확정1200=5927891891329800569. 실제 입력에서 상자 운반 Tilemap Reset을 관측하고 매 확정 프레임 플레이어 몸체의 윤곽 지형 겹침이 없음을 검사했다. `Logs/QuantumTilemapReplay.json`, `QuantumTilemapTrace.txt`, `QuantumTilemapChecks.txt`.
- 바닥/양 벽/천장·외곽 내부/구멍·윤곽 순서/방향 독립·Reset 빈 타일/인접 조각·접촉1raw 경계·맵/윤곽/조각 순서 독립 해시·문화권 독립·잘못된 목적지/조각/ID/경사/비유한 값/상한 거부를 검사했다. 직접 프레임 검사는 다중 Reset 접촉/같은 틱 반복 호출1회·운반 상자 전체 점유 대기·공간 해제 동시 복귀·재Reset 방지·상자 직접 다중 조각 복귀/중복 불변과 축소 운반자 천장 판정 재현/수정을 포함한다.
- Main의 실제 생성 Button 콜백→여섯 번째 사용례 선택→제품 QuantumPlay→실제 입력 운반Reset→전원 도착→Main→재진입/메뉴 종료를 실행했다. 픽셀 클릭이나 실제 두 사람 조작 검증은 아니다. 새 맵 해시를 훼손한 런타임 복제 manifest는 실패 UI를 표시했고 러너0/세션없음/표시0이었다. 원본 콘텐츠로 재시도와 메뉴 종료도 통과했다. `Logs/QuantumTilemapFailureUI.json`.
- 제품6에서 기존 이동8·정적6·로프8·상호작용12·복귀20·장치20의 새 재생/경계 검사를 실행했다. 기존 Main의 장치 운반 대기·소환 거부/성공·완료/재진입, 실패/취소/중복 시작·외부 씬 왕복·러너/입력/메뉴/표시 잔존 검사가 통과했다. 실행 Unity Error/Exception/Assert0. `Logs/QuantumProductOffline.txt`. 직전 제품5 수치를 재사용하지 않았다.
- 최종 Tilemap 개발 Photon 두 연결: 같은 PC·asia·RTT115/108ms·지연/손실 주입 없음, 확정240~1200/120간격9개 일치, 완료1496 양쪽 일치, 양쪽 실제 상자 운반Reset 관측, 러너/연결/입력 정리=True. 확정1200=8986908035022667559. `Logs/QuantumTilemapCloud.txt`. 앞선 방115/117ms·완료1491은 중간 실행이며 최종 결과와 구분한다. 다른 PC·실제 두 사람·3~4인·출시 인증 통과로 해석하지 않는다.
- SDK QuantumCodeGenQtn.Run을 실행하고 Unity 컴파일/Mirror Weaver를 통과했다. 지정6000.6.3f1 번들 dotnet의 런타임 빌드 오류0/경고165, 에디터 오류0/경고7이며 최종 테스트 소스의 에디터 재빌드도 별도로 확인한다. 기존 CS0649/사용 중단 경고를 없애려고 벤더를 고치지 않았다. `Logs/QuantumTilemapRuntimeBuild.log`, `QuantumTilemapEditorBuild.log`.
- 준비/플레이 화면을 캡처·시각 확인했다. 여섯 선택의 의도한 두 줄 배치, 판정 구멍/윤곽·상자/Reset 표시, 제품 Collider2D0·Bootstrap0·TMP overflow0·자동 씬 로딩 Disabled다. 새 사용례 카메라는 두 플레이어 범위를 따라 크기를 조정한다. `Logs/QuantumTilemapPrepare.png`, `QuantumTilemapPlay.png`.
- 순수 FP 지형 커널의 워밍업 뒤 Overlaps/Limit 총200,000호출(28변)은112.532ms·관리 할당0바이트였다. 전체 틱·SDK·Unity 표시 비용 측정이 아니며 최대512변의 성능 보증으로 확대하지 않는다. `Logs/QuantumTilemapPerformance.json`. 새 qtn 프레임 필드는 추가하지 않았고 장치1개가26조각의 접촉 이력을 공유한다.

## 실패 분석

- 프리팹의 Tilemap 자식만 직접 Instantiate하면 Grid 조상 없이 셀 형상과 렌더러 범위가 잘못 생성됐다. 생성 형상/범위 검증이 거부했다. 전체 원본을 인스턴스화한 뒤 선택한 지형/Reset·목적지와 Grid 조상을 보존하는 방식으로 수정했다. 원본 프리팹 설정을 고치지 않았다.
- 첫 자동 입력은 두 플레이어가 마주 오면서 상대를 먼저 들었고, 이후 서로 가로막혔다. 두 번째는 가속 중 점프 이동 거리가 부족해 상대를 넘지 못했다. 확정 좌표/운반 참조로 원인을 확인하고 P1 반복 점프와 P0 시작 지연을 사용했다. 충돌 수치·게임 규칙·단언을 완화하지 않았다.

## 미실행과 다음 착수점

- 다른 PC·실제 두 사람·3~4인·배포 빌드·Steam 다계정/출시 인증·게임패드·인위적 지연/손실 주입은 미실행이다. 개발 Photon 같은 PC 두 연결과 구분한다. 출시 개발 앱/익명 폴백을 추가하지 않았다.
- 다음은 Stage7 Ground/Reset의 더 복잡한 오목 윤곽·타일 구멍을 같은 작성 검증으로 검사하고, 통과 가능한 작은 별도 사용례를 선정한다. 지원 전에는 원본 전체 Stage7을 메뉴에 넣지 않는다. 본격적인 경사 이동과 동적/전환 지형은 별도 묶음이다.
