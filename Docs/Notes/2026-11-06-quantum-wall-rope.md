# Quantum 벽 접촉·로프 이식 검증

- 기준: 2026-11-06 KST. 프로젝트/연결 에디터 `6000.6.8f1`, Quantum `3.0.14 Stable Build 2241`.
- 공식 설치 문서의 지원 표는 6000.5까지이며 6000.6 명시는 없다. 이번 프로젝트 실행 성공과 공식 지원을 구분한다.
- 기존 미커밋 Mirror 코드·씬·프리팹·설정을 보존했다. 자동 커밋·초기화·Mirror 제거를 하지 않았다.

## 실제 Mirror 동작 조사

- `Player.prefab`의 MovementHandler: 걷기 4, 달리기 5.5, 최대/최소 점프 높이 2.2/1.2, 정점 0.3초, 달리기 점프 추가 2. 중력 식의 결과 크기 44, 기본 점프 13.2, 최대 낙하 12다.
- 벽 값: `wallJumpClimb=(10,25)`, `wallJumpOff=(13,27)`, `wallLeap=(16,25)`, `wallSlideSpeedMax=3`, `wallStickTime=0.25`, `wallSlideTime=0`.
- `Controller2D`는 수평 레이에서 left/right를 설정한다. 그러나 `MovementHandler.wallSliding`과 `wallDirX`의 대입 경로가 없고, 전자는 기본 false다. 미끄럼 제한·세 종류 벽 점프 분기는 도달하지 않는다. `wallStickTime`도 실행에 사용하지 않는다. 벽 상승·자동 벽 점프를 Quantum에 새로 추가하지 않았다.
- ApplyInput의 점프 → Simulate의 타이머 감소 → 로프 진입 검사 → 중력 → 등반이면 `directionalInput * moveSpeed` → 지형 충돌 순서다. 상하 입력이 있어야 진입하며, 등반 뒤 무입력은 정지다. 좌우 이동도 허용한다.
- 로프 접촉 수가 0이면 등반을 해제한다. 중심 고정·끝점 클램프는 없다. 점프는 Y=13.2 후 등반을 해제하고 0.3초 제한을 건다. Quantum은 60Hz 18틱으로 표현하고 같은 순서로 첫 틱을 차감한다.
- 이번 이식은 운반·밟기·피격 상태를 추가하지 않는다. 기존 가속·달리기·최소 높이 점프 컷은 기존 Quantum 기반처럼 미이식이다.

## 변환·씬

- 실제 팔레트 `terrain.rope`는 `Assets/03_Prefabs/MapPrefabs/Rope.prefab`이다. 자식 없는 루트, BoxCollider2D 크기 `(0.35,1)`·오프셋 `(0,-0.5)`·enabled·isTrigger·Rope 태그를 확인했다. SpriteRenderer·NetworkIdentity·NetworkTransformReliable도 있다.
- Mirror 배치처럼 프리팹 루트 스케일을 배치 스케일로 대체하며 오프셋에도 부호 있는 배치 스케일과 회전을 적용한다. 로프는 검증한 수직 배치(180도 단위)만 지원한다. 지형은 기존 90도 단위를 유지한다.
- MapData 2.1은 유지했다. 변환/팔레트 버전은 2/2이며 기존 정렬·FP 원시 값 직렬화 규칙에 로프 구분/중심/크기를 포함한다. 해시는 콘텐츠 일치 확인용이다.
- 기존 정적 원본 JSON은 보존하고 v2 검증 에셋만 갱신했다. 새 `QuantumRopeTest.unity`는 박스 4개·로프 4개·시작점 2개·도착 표시를 Unity API로 생성/베이크/저장/재로드했다. 로프는 불변 RuntimeConfig FP 데이터이며 Unity 콜라이더를 런타임에 생성하지 않는다.
- 미지원 회전·콜라이더/자식/장치·비트리거·비활성·Rigidbody·태그/변환 값은 위치와 원인을 보고하며 전체 변환을 중단한다. 비유한 BoxCollider 크기는 Unity setter에서 정규화하므로 이를 변환기 거부 시험으로 계산하지 않았다. 비유한 배치 값 거부는 실행했다.

## 자동 검증

- 벽 접촉을 먼저 추가하고 기존 v1 정적 맵의 생성·양쪽 벽 접촉 플래그·바닥·천장·점프와 새 러너 재생 120~720 체크섬 6개를 통과한 뒤 로프를 추가했다. 기록: `Logs/QuantumWallRopeBaseline/WallChecks.txt`.
- 변환 10회 반복/역순, 좌표 변경 해시, 로프 오프셋·반전/180도, 미지원 값/형태 전체 거부와 위치 진단, 검증 에셋 해시·변환/팔레트 버전 불일치 거부를 통과했다.
- 로프는 두 플레이어 모두 무입력 진입 금지·상하 진입·상승/하강·정지·상단 이탈·수평 이탈·점프 1회 분리·18틱 감소/재진입 차단·만료 재진입·벽/바닥/천장 충돌·벽 미끄럼 비활성을 단언한다. 오프라인은 플레이어별 확정 상태 900개·최대 간격 1틱을 관찰했다.
- 입력에 위/아래 FP 값과 Jump button을 기록한다. 새 러너에 기록을 재생해 확정 120/240/360/480/600/720/840/960 체크섬 8개가 일치했다. 최종 `7127839490825245942`. 기록: `Logs/QuantumRopeChecks.txt`, `Logs/QuantumRopeReplay.json`.
- 최종 정적 v2 해시 `1853f2f5245ad4570894b368d4953d8e4447a653f688b0e028b8854171b0b052`에서 기존 두 플레이어 생성/이동/점프/접지/양쪽 벽 플래그/천장·비관통과 재생 120~720 체크섬 6개를 다시 통과했다. 720=`15628097944017739190`, `Logs/QuantumCustomMapChecks.txt`.
- 최종 두 씬 재로드는 박스 각 4개·로프 0/4개·Unity Collider/Collider2D 0개·Mirror 매니저 0개·dirty=false이며 빌드 첫 Main을 유지한다. `Logs/QuantumWallRopeSceneCheck.txt`.
- SDK Qtn 생성기·Unity 컴파일 후 Play/종료를 실행했다. `Assembly-CSharp`·`Assembly-CSharp-Editor` 및 참조된 `Quantum.Simulation`, `Quantum.Unity`, `Quantum.Unity.Editor`, `Quantum.Unity.Editor.CodeGen` 빌드 오류 0. 최종 증분 에디터 경고 7건/런타임 경고 0건, 초기 전체 참조 빌드에서는 기존 경고 106건이 있었다. `Logs/QuantumRopeFinalBuild.log`, `Logs/QuantumRopeRuntimeBuild.log`.

## 실제 Photon 새 실행

- 에디터 전용 개발 앱·`asia`·두 연결/러너·같은 PC. Steam 인증·출시 경로의 익명 폴백을 추가하지 않았다.
- 두 참가자의 `2:2:e5ab54034f8b698c0f62cba13acc46cabc46d82178e1605b691ac71d596fea3c` 합의 뒤 시작했다.
- 최종 측정 RTT 175/126ms, 지연/손실 주입 없음. 각 러너에서 두 플레이어의 전체 로프 동작 단언이 통과했다. 플레이어별 확정 상태 869개·최대 간격 1틱이다.
- 양쪽 확정 체크섬: 240=`13008809904540018092`, 360=`18064950001872122109`, 480=`10751490795516801953`, 600=`9030581949241470784`, 720=`16341657850723085109`, 840=`7491903809499758739`, 960=`15774136902437804685`.
- 자료: `Logs/QuantumRopeCloudCheck.txt`. 서로 다른 예측 프레임이나 로컬/온라인의 서로 다른 세션 체크섬을 desync로 비교하지 않았다.
- 초기 시도는 초반 플레이어 등록 이후 시나리오 준비 부족, 연결 순서와 전역 PlayerRef의 혼동으로 동작 범위가 누락돼 실패했다. 확정 위치 추적으로 반대 방향 이동을 확인하고, 준비 240틱·확인된 전역 PlayerRef 기준 입력으로 수정한 뒤 새 두 연결에서 모든 단언을 통과했다. 관찰 간격은 1틱이었으며 확정 프레임 누락이 원인은 아니었다.
- 실제 두 사람 키보드 조작, 다른 PC·Steam 다계정·RTT/손실 주입·출시 소유권 인증은 미실행이다.

## 인계

- 비활성 벽타기/벽 점프의 활성화 여부를 먼저 확정한다. 기존 프리팹 수치를 임의로 해석해 새 기능을 켜지 않는다.
- 조작: 1P A/D·W/S·Space, 2P 방향키·오른쪽 Ctrl. 로프 검사는 `Tools/Flip Friends/Quantum/Check Rope Conversion And Replay`, 온라인은 `Check Development Cloud Two Clients`다.
- 가변 점프·가속 범위를 정한 뒤 밟기·운반으로 진행한다. 전체 맵 에디터·친구 맵 전송과 출시 인증은 별도 범위다.
