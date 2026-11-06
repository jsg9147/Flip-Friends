# Quantum 전환 실행 기반

- 상태: Active (단계 0~1 일부 구현, 출시 인증 미완료)

## 현재 상태

- Quantum 3.0.14 Stable Build 2241을 공식 사이트에서 내려받아 임포트했다. Preview는 선택하지 않았다. 이전 설치 검증은 Unity 6000.6.3f1이며, 2026-11-06 조사 시 프로젝트·실행 에디터는 6000.6.8f1이다. 이번 세션에서 버전을 변경하지 않았다. Steamworks.NET은 2024.8.0이다.
- [공식 설치 문서](https://doc.photonengine.com/quantum/v3/getting-started/initial-setup)의 지원 표는 6000.5까지다. 6000.6 전체 호환성이 보장됐다고 해석하지 않으며 이번 프로젝트의 컴파일·실행 결과만 기록한다.
- SDK 원본은 `Assets/Photon`, 공식 생성 구조는 `Assets/QuantumUser`에 둔다. 생성기는 SDK가 제공한 GUID를 생성하며 수동 GUID를 작성하지 않았다.
- 기존 미커밋 코드·씬·프리팹을 보존했다. Mirror/FizzySteamworks를 제거하지 않았고 빌드의 첫 씬은 기존 Main이다. 테스트는 별도 `Assets/00_Scenes/QuantumMovementTest.unity`에서 시작한다.
- `FlipFriendsMovementSystem`은 Quantum 고정소수점 프레임 상태·입력으로 플레이어 생성·좌우 이동·점프·접지·좌우 벽/천장 접촉·로프를 처리한다. Unity Physics2D·Transform·코루틴은 게임 판정에 사용하지 않는다.
- 테스트는 60Hz·2슬롯이다. 기존 Mirror는 Fixed Timestep 0.02(50Hz), 걷기 4·달리기 5.5, 최대/최소 점프 높이 2.2/1.2, 정점 시간 0.3초다. 최소 이동은 걷기 4·중력 44·점프 속도 13.2·최대 낙하 12를 사용한다. 기존 가변 점프·가속·달리기·밟기까지 이식한 상태는 아니다.
- 입력은 New Input System을 사용한다. 오프라인 1P A/D·W/S·Space, 2P 좌/우·위/아래·오른쪽 Ctrl(점프)다. 위/아래 FP 입력과 Quantum button의 `WasPressed`가 기록/재생된다. Unity 큐브는 표시만 하며 판정 위치는 Quantum 프레임에 있다.
- 별도 `QuantumCustomMapTest.unity`에 기존 `MapData` 2.1·`MapDataValidator`·실제 팔레트를 재사용하는 변환 지형을 연결했다. 저장 맵 `새 맵`은 시작점 1개·짧은 바닥만 있어 충돌 검증에 부적합했으며, 원본을 보존하고 작은 시험 JSON(`FlipFriendsCustomMap.json`)을 추가했다. 시작점 2개·도착 표시 1개·정적 박스 4개다.
- `QuantumMapConverter`는 Mirror Spawn 경로와 분리된다. 정적 지형·시작점에 더해 `terrain.rope`의 자식 없는 루트 단일 활성 BoxCollider2D 트리거·Rope 태그를 지원한다. 실제 로프는 크기 `(0.35,1)`·오프셋 `(0,-0.5)`이며 배치 스케일로 루트 스케일을 대체하고 오프셋에도 적용한다. 기존 NetworkIdentity/NetworkTransformReliable은 작성 메타데이터만 허용하며 생성하지 않는다. 도착점은 표시만 지원한다. 상자·장치·자식/원형/합성/비트리거 로프 등은 번호·ID·위치·원인과 함께 전체 변환을 중단한다.
- 변환 v2·팔레트 v2(MapData는 2.1 유지): XY 평면(Z=0, Z 스케일=1), 지형 회전 90도 단위(-3600~3600), 수직 로프 회전 180도 단위, 부호 있는 XY 스케일 절댓값 0.01~128을 지원한다. 좌표·크기·오프셋은 float의 정확한 이진 값을 65536배해 중간값을 0에서 먼 쪽으로 반올림한다. 회전·반전은 FP 연산으로 처리하고 전체 크기는 2 원시 단위(1/32768)로 반올림한다. Unity float 왕복에 정확히 표현되지 않는 결과는 거부한다.
- 고정 팔레트 ID와 위치·스케일·회전의 FP 원시 정수 키를 Ordinal 정렬해 생성한다. 맵 ID·스키마·변환/팔레트 버전·정렬한 배치·계산된 지형/로프의 구분과 원시 값을 LF/불변 문화권 UTF-8로 직렬화하고 SHA-256을 구한다(표시 이름·작성자는 제외). 동일 키의 중복은 유지한다. 시작점도 같은 정렬 순서이며 2개가 정확히 필요하다. v1 검증 에셋은 자동 수용하지 않으며 원본을 v2로 재변환해 저장한다.
- `QuantumRopeTest.unity`는 정적 박스 4개·로프 4개·시작점 2개·도착 표시로 구성된다. 로프의 중심/크기는 검증된 RuntimeConfig 불변 FP 데이터, 접촉 수·등반 여부·재등반 제한은 프레임 상태다. 상하 입력으로 진입하고 붙은 동안 양 축 속도 4·무입력 정지, 접촉이 없어지면 이탈한다. 점프 속도 13.2·재등반 제한 0.3초(60Hz 18틱, 점프 처리 후 첫 틱 차감)다. 중심 고정·끝점 클램프는 없다.
- Mirror 실제 코드의 `wallSliding`/`wallDirX`에는 대입 경로가 없어 벽 미끄럼·벽 점프가 실행되지 않는다. 프리팹에는 미끄럼 3·붙기 0.25초·점프 `(10,25)/(13,27)/(16,25)`가 남아 있다. Quantum에 자동 활성화하지 않았으며 벽 접촉과 비활성 동작을 검증한다. 벽타기 신규 활성화는 동작 확정 후 별도 작업이다.
- `QuantumCustomMapAsset`은 시작 전 원본·버전을 재검증하고 해시와 실제 베이크된 박스 위치·크기·개수·판정 설정을 대조한다. 온라인은 각 참가자의 Photon 속성에 `변환:팔레트:해시` 확인값을 게시하고, 두 명 모두 일치한 뒤 러너를 시작한다. 불일치·이탈/연결 끊김·15초 초과는 시작을 중단한다. 맵 전송·구매 인증을 대체하지 않는다.
- 무료 `Flip Friends Quantum Dev` 앱을 생성했다. 공개 App ID는 PhotonServerSettings에 연결했다. 출시용 앱은 아직 없으며 개발 앱에는 Steam 인증 공급자가 없다.
- `QuantumTestBootstrap`의 온라인 경로는 실제 Steam App ID·로그인·Web API 티켓을 요구한다. App ID 0/480, 발급 실패·취소·15초 제한 시간은 접속을 중단하며 익명 폴백은 없다.
- `SteamPhotonTicket`은 identity `photon`으로 비동기 콜백 완료를 기다려 Steam 인증 값을 만들고 인증 완료·실패·종료 시 티켓을 취소한다. 서버의 소유권 검증을 대체하지 않는다.
- 사용자가 Steam 출시 앱 미발급을 확인했다. Publisher 키·Steam 공급자·`verifyOwnership=true`·출시 앱 익명 거부와 실계정 인증은 차단 항목이다. 비밀 값을 저장·요청하지 않았다.
- 출시 목표는 30개 스테이지·첫 클리어 약 2시간이며 현재 콘텐츠는 21개다. 커스텀 맵은 친구 전송이 범위이며 공개 검색 서버는 포함하지 않는다.

## 핵심 흐름

- `QuantumTestBootstrap` → Quantum 로컬/온라인 러너 → 입력 → `FlipFriendsMovementSystem` → 프레임 상태 → Unity 표시.
- 온라인: `SteamPhotonTicket` → Photon Steam 인증 → 2명 대기 → 방 닫기 → Quantum 시작. 초기 시험은 최대 2명이며 제품 목표 4명 검증은 남아 있다.
- `QuantumTestSceneSetup`은 SDK 씬 생성·맵 베이크 API로 정적 지형을 만들고 저장·재로드한다. 기존 테스트 씬을 덮어쓰지 않는다.
- `QuantumReplayCheck`는 기록 입력을 새 러너에서 재생해 같은 확정 프레임의 체크섬과 2명 생성을 대조한다.
- `QuantumCloudCheck`는 특정 개발 앱만 사용하는 에디터 전용 익명 2연결 시험이다. 플레이어 빌드에 포함되지 않으며 출시 구매 인증의 증거가 아니다.

## 검증

- 2026-11-06 벽 접촉을 먼저 추가하고 기존 v1 정적 맵의 두 플레이어 생성·좌우 벽 플래그·점프·바닥·천장과 확정 120~720 재생 6개를 통과한 뒤 로프를 추가했다. 최종 v2 정적 회귀와 로프 검증은 아래 기록을 따른다.
- `Check Rope Conversion And Replay`: 반복 10회/역순·로프 좌표 변경 해시·실제 오프셋·음수 스케일/180도, 미지원 회전/장치/콜라이더/자식/동적/태그/비유한 값의 위치 진단 및 전체 거부, 실제 검증 에셋의 해시·두 버전 불일치 거부를 통과했다.
- 로프 오프라인 두 플레이어의 진입·상하·정지·상단/수평 이탈·점프 1회 분리·재등반 제한/재진입·벽/바닥/천장 충돌·비활성 벽 미끄럼을 자동 입력으로 검증했다. 준비 240틱 후 시나리오를 시작하며, 새 러너 재생의 확정 120~960(120간격) 체크섬 8개 일치·960=`7127839490825245942`. 자료: `Logs/QuantumRopeReplay.json`, `Logs/QuantumRopeChecks.txt`. 실제 두 사람의 키보드 조작은 미실행이다.
- 로프 맵 SHA-256=`e5ab54034f8b698c0f62cba13acc46cabc46d82178e1605b691ac71d596fea3c`(변환 2/팔레트 2). 개발 앱 Photon `asia`의 새 두 연결·러너에서 맵 합의 후 두 플레이어의 로프 전 항목 통과, 동일 확정 240~960(120간격) 체크섬 7개 일치·960=`15774136902437804685`. 최종 측정 RTT 175/126ms·같은 PC·지연/손실 주입 없음. `Logs/QuantumRopeCloudCheck.txt` 및 [측정·시험 도구 수정 근거](Notes/2026-11-06-quantum-wall-rope.md)를 따른다. Steam 다계정·다른 PC·출시 인증의 증거가 아니다.
- 최종 v2 정적 맵에서 양쪽 플레이어 생성·걷기·점프·접지·좌우 벽 플래그·천장/비관통과 확정 120~720 재생 6개가 통과했다(720=`15628097944017739190`). 두 시험 씬 재로드 결과 정적 박스 각 4개·로프 0/4개·Unity Collider/Collider2D 및 Mirror 매니저 0개·dirty=false, 빌드 첫 Main을 확인했다. SDK Qtn 코드 생성·Unity 컴파일/Play와 `Assembly-CSharp`, `Assembly-CSharp-Editor`, 참조 Quantum 4개 어셈블리 빌드 오류 0. 최종 증분 에디터 빌드 경고 7건·런타임 빌드 경고 0건이며 전체 기존 경고가 없다는 의미는 아니다. `Logs/QuantumRopeFinalBuild.log`, `QuantumRopeRuntimeBuild.log`, `QuantumWallRopeSceneCheck.txt`를 따른다.
- 이전 정적 맵 v1 SHA-256=`7da4e3240975e348c93b94337ec8836e0e3fc2ca9508965b744712387b4efbf0`은 과거 결과다. 동일 원본 JSON·박스 4개의 v2 해시=`1853f2f5245ad4570894b368d4953d8e4447a653f688b0e028b8854171b0b052`이며 검증 에셋·씬을 Unity API로 갱신/저장/재로드했다. 두 시험 씬 모두 Main 첫 빌드·기존 Mirror 경로를 보존한다.
- `Tools/Flip Friends/Quantum/Check Custom Map Conversion And Replay`는 새 시험 씬 Play 중 실행한다. 반복 10회/역순의 변환·해시 일치, 좌표 변경 해시 변화, 90도/음수 스케일/반올림 격자, 미지원 팔레트·원형 콜라이더·45도·NaN/무한/0 스케일·Z 좌표·스키마와 해시/두 버전 불일치 거부를 통과했다.
- 이전 v1 정적 맵의 오프라인 확정 120~720 재생 6개 일치·720=`1102645753820639666`은 과거 결과다. 현재 `Logs/QuantumCustomMapReplay.json`·`QuantumCustomMapChecks.txt`는 위 최종 v2 회귀 결과로 갱신됐다. 실제 두 사람 조작·플레이어 간 충돌 검증은 아니다.
- 이전 v1 변환 맵의 개발 Photon `asia` 두 연결 결과는 확정 240~720 체크섬 5개 일치·RTT 107/109ms·같은 PC·주입 없음이었다(`Logs/QuantumCustomMapCloudCheck.txt`). 이번 로프 변경의 통과 증거로 재사용하지 않는다. Steam 다계정·다른 PC·다른 플랫폼·구매 인증은 미실행이다.
- 이전 정적 맵 단계의 전체 빌드는 기존 Inspector/벤더·이전 API 등을 포함한 경고 106건·오류 0건이었다(`Logs/QuantumMapFinalBuild.log`). 이번 로프 최종 빌드·재로드 결과는 위 기록과 구분한다. 6000.6의 공식 지원 여부를 실행 성공으로 대신하지 않는다.
- SDK 입력·컴포넌트 `.qtn` 코드 생성과 Quantum 시뮬레이션 어셈블리 컴파일을 실행했다.
- 독립 테스트 씬 저장·재로드와 오프라인 두 플레이어 생성, 반복 입력을 통한 좌우 이동·점프·바닥 접지를 확인했다. 실제 두 사람의 키보드 조작 회귀는 별도다.
- 이전 기본 이동 씬의 확정 프레임 1355: 원본/입력 재생 체크섬 `12518711715144769879` 일치, 플레이어 2명. 재생 자료와 결과는 Git 제외 `Logs/QuantumMovementReplay.json`, `Logs/QuantumReplayCheck.json`이다. 이번 변환 맵·다른 플랫폼 결정론의 증거로 재사용하지 않는다.
- 이전 기본 이동 씬의 개발 앱 Photon `asia` 실제 2연결·2러너·플레이어 2명 생성 통과. 확정 240/360/480/600 체크섬이 양쪽 모두 일치했다. 측정 RTT 106/106ms, 인위적 지연·손실 주입 없음. 이번 변환 맵 검증과 구분하며 Steam 다계정·서로 다른 PC·구매 인증 검증을 대체하지 않는다. 결과는 `Logs/QuantumCloudCheck.txt`다.
- `Assembly-CSharp`, `Assembly-CSharp-Editor`, `Quantum.Simulation`, `Quantum.Unity`, `Quantum.Unity.Editor`, `Quantum.Unity.Editor.CodeGen` 빌드 오류 0. Unity 컴파일·Mirror Weaver 뒤 실제 Play 진입·종료를 확인했다. Steam App ID 0/480 요청 거부도 실행했다.
- 재로드한 테스트 씬은 Bootstrap 1개·Quantum 정적 박스 4개·Mirror 매니저 0개이고, 기존 빌드 첫 씬은 Main이다. SDK 설치의 `QUANTUM_XY`·Photon 심볼과 `.qtn` 프로젝트 생성 확장은 유지한다.
- 기존 KCP 수단은 `Tools/Flip Friends/Local Network Test`와 빌드 `-localhost`/`-localclient -latency 60`이다. 양쪽 송신 지연이므로 예상 RTT 약 120ms이며 이번 세션에 KCP 회귀를 재실행하지 않았다.
- RTT 0/60/120/200ms·손실 1~3%, Steam 2~4인·가족 공유·환불·중복 계정·티켓 만료 검증은 미실행이다.

## 남은 작업

- Steam 앱 발급 후 별도 출시 Photon 앱에서 Publisher Web API 키를 직접 등록하고 공급자 `version=2`, `identity=photon`, `verifyOwnership=true`, 익명 거부를 설정·검증한다. [보안 기준](QUANTUM_SECURITY.md)을 따른다.
- 지원 지형 밖의 장치·추가 맵·경사/동적 콜라이더와 친구 맵 전송은 별도 이식 범위다.
- 벽타기·로프 → 밟기·들기·던지기·상자 → 피격·리스폰·발판 순서로 이식한다. Shrink의 콜라이더 크기는 프레임 상태로 옮긴다.
- 21개 기존 스테이지 회귀 후 추가 9개를 제작한다. Steam 친구 초대·로비/지역 연결과 진행 중 입장 제한을 이식한다.

## 다음 작업

- 비활성인 Mirror 벽타기/벽 점프를 새로 활성화할지 동작부터 확정한다. 로프 기반을 유지하며 가변 점프·가속의 이식 범위를 정한 뒤 밟기·운반으로 진행한다. 친구 전송·전체 맵 에디터·출시 인증은 별도 범위이며 Steam 다계정·다른 PC·지연/손실 시험은 미실행이다.
