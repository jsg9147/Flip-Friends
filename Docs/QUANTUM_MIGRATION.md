# Quantum 전환 실행 기반

- 상태: Active (첫 제품 통합 경로 완료, 전체 게임 전환·출시 인증 미완료)

## 현재 상태

- 제품10/ff-product-10에 **스프링/바운스**를 추가했다. 원본 Player.prefab의 바운스20·스프링50과 위치 기반 방향 속도 덮어쓰기를 이동 뒤 진입에서 적용한다. 접촉 이력은 프레임에 저장하며 로프·운반 관계·스무딩·타이머를 유지한다. 공식 작성 컴포넌트 `QuantumStageLauncher`는 정적 원형·Box·볼록 Polygon 트리거를 지원하고 별도 `official-launcher-test`를 등록했다. [구현·검증](Notes/2026-11-09-quantum-launchers.md).

- 제품9/ff-product-9에 **피격·넉백**과 최소 정적 트랩을 추가했다. 원본 `HandleDamage`/`OnDamaged`처럼 이동 뒤 접촉으로 판정하고 넉백 방향×(20,20)·무적60·조작 제한60·로프 차단60틱·운반자 넉백 쪽 내려놓기를 적용한다. 방향은 지정값(정규화 없음) 또는 트랩→플레이어 단위 벡터, 동일 틱 다중 접촉은 ID 순 첫 트랩, 들린 플레이어·상자는 제외, 복귀·도착은 기존처럼 초기화한다. 작성 컴포넌트 `QuantumStageTrap`(트리거 Box·볼록 Polygon)과 별도 공식 `official-hazard-test`를 추가했다. 이동/회전 트랩·대포·투사체·컨베이어(외부 속도)는 미이식이며 해당 Collider·컴포넌트는 계속 거부한다. [피격 구현·검증](Notes/2026-11-08-quantum-hazards.md).
- 이전 묶음: 제품8 경사(실제 Player.prefab30도·Box.prefab45도, 작성기45도 이하 허용, `official-slope-test`)([기록](Notes/2026-11-08-quantum-slopes.md)), 제품7 공식 스테이지 변환·목록·Main 선택과 Composite 8raw 미세 단차 수정([기록](Notes/2026-11-08-quantum-official-stage.md)), 제품6 Tilemap 윤곽 지형·사각 조각 Reset([기록](Notes/2026-11-08-quantum-tilemap.md)), 제품5 복귀·체크포인트·구간 Reset·상자 소환([복귀](Notes/2026-11-08-quantum-respawn.md)·[장치](Notes/2026-11-08-quantum-devices.md)).
- Stage7 Ground/ResetTile은 작성기를 통과하지만 MapData 배치 범위(x−50~50) 밖이라 사용례를 만들지 않았고 규칙도 완화하지 않았다([Stage7 검사](Notes/2026-11-08-quantum-stage7-tilemap.md)).
- Main Start → `QuantumPrepareScreen`의 사용례 6종 또는 공식 스테이지 선택(현재 시험4종 순환) → 오프라인 2인 → QuantumPlay → 전원 도착/메뉴 종료 → Main·재진입이 제품 경로다. 시험 Bootstrap이나 Mirror 플레이어가 전제 조건이 아니다.
- 이동·로프·밟기·운반·던지기·상자·복귀·장치·경사·피격·스프링/바운스는 Quantum FP/DeltaTime과 프레임 상태로만 처리하며 Unity는 입력 수집·표시를 맡는다. Mirror 이동·물리·Animator는 생성하지 않는다.
- 60Hz 이동의 걷기4·달리기5.5·SmoothDamp0.4/반전0.7·공중 제어·가변 점프·조건부 달리기 점프 보너스·로프18틱과 충돌 순서를 보존했다. 벽 미끄럼/벽 점프·일반 BlockJump 차단은 원본에 활성 경로가 없어 켜지 않았다.
- 밟기13.2(점프 유지×1.2)+X차×0.5, 반복 차단18틱·밟힌 쪽9틱·Shrink30틱. 기본 판정은 Quantum (0.8,1)/오프셋0(Mirror (0.9,0.8)/(0,-0.1)과 차이)이며 피격 판정도 이 몸체를 쓴다.
- 운반: 플레이어 우선 탐색·0.2 레이10개·머리 위 점유 검사·양방향 관계, 같은 틱 전역 PlayerRef 순서, 운반 위치(0,0.95)·해제9틱·탈출90/15틱·플레이어 던지기(7,12)/24틱·상자 던지기(6,6)·중력9.8/감속1.5초/낙하12. 놓기는 앞→머리 위→운반자 자리, 모두 막히면 운반 유지(기존 무검사 fallback과 의도적 차이).
- 고정 키: 1P A/D·W/S·Space·왼쪽 Shift·E·R, 2P 방향키·오른쪽 Ctrl·오른쪽 Shift·Enter·Backspace. 아래+들기는 놓기. 온라인 키보드는 각 연결의 로컬슬롯0이다. 키 재설정/게임패드는 미연결.
- MapData2.1 유지. 정적/로프2:2·상호작용3:3·복귀4:4·장치5:5·Tilemap6:6의 원본/베이크/해시를 보존한다. 공식 스테이지는 `공식1:<해시>`이며 트랩이 없는 공식 콘텐츠 해시는 바뀌지 않았다. 규칙 서명은 `제품10`·온라인 `ff-product-10`다.
- 제품 전용 FlipFriendsProductSimulationConfig의 자동 씬 로딩 Disabled를 유지하며 QuantumGameFlow가 씬 수명을 소유한다.
- 실제 Unity6000.6.8f1·Quantum3.0.14 Stable2241·Steamworks.NET2024.8.0이다. 버전은 변경하지 않았다. [공식 지원 표](https://doc.photonengine.com/quantum/v3/getting-started/initial-setup)의6000.6 미명시와 프로젝트 실행 성공은 별개다.
- 기존 Steam 게임 버튼·Steam 로비/초대·MapEditor 테스트·친구 맵 전송·21개 스테이지는 Mirror다. 출시 온라인은 별도 출시 Photon 앱/실제 Steam 티켓이 필수이며 미설정 실패 UI·익명 폴백 없음이다. 개발 익명 연결은 UNITY_EDITOR에만 있다. 보안 문서는 Draft다.

## 핵심 흐름

- Main Start → `QuantumPrepareScreen` → `QuantumGameFlow` → `QuantumGameSession` → 입력 → 복귀/상호작용/이동/상자/운반/장치/피격/완료 시스템 → 확정 프레임 → `QuantumGameView` → Main.
- 공식 스테이지: 프리팹 루트 `QuantumOfficialStage`+작성 컴포넌트 → `Tools/Flip Friends/Quantum/선택한 공식 스테이지 변환`(`QuantumOfficialStageWriter`) → `QuantumOfficialStageConverter`(FP 변환·해시) → 공식 목록.
- 온라인: `SteamPhotonTicket` → Photon Steam 인증 → 2명 대기 → 방 닫기 → Quantum 시작. 초기 시험은 최대 2명이며 제품 목표 4명 검증은 남아 있다.
- `QuantumCloudCheck`·각 `*Checks.RunCloud`는 특정 개발 앱만 쓰는 에디터 전용 익명 2연결 시험이다. 출시 구매 인증의 증거가 아니다.

## 검증

- 제품10 스프링/바운스(2026-11-09): 작성기/변환 거부14종·원본/해시, 실제 입력 기록/별도 러너 재생20개 일치·상자 운반·전원 완료594·직접8종·Unity 오류0, Main 선택/완료/재진입/종료·잔존0 통과. 개발 Photon 동일PC 두 연결 asia·RTT103/115ms·확정19개·발사 상태/완료650 일치·정리 True. 제품 전체 오프라인·Tilemap/공식/경사/피격 재생 및 Main 회귀·공식 콘텐츠4종 통과. [상세 범위](Notes/2026-11-09-quantum-launchers.md).
- 제품10 지정 dotnet 런타임/에디터 빌드 오류0(기존 코드/SDK 경고는 남음). 종료 뒤 Main 미저장 상태를 유지하고 러너0·입력0·자동 씬 로딩 Disabled를 확인했다.
- 제품9 피격(2026-11-08): 작성기/변환 거부13종, 실제 입력 기록·별도 러너 재생25개 일치, 지정/위치 기반/대각/지속 접촉 재피격60·운반자 내려놓기·피격 직후 Reset·완료, 직접 규칙11종, Main 선택/완료/복귀/재진입·잔존0, 개발 Photon 동일PC 두 연결 asia·RTT122/107ms·확정24개·피격 상태·완료1266 일치 통과. 같은 제품9 코드로 기존 제품 전체 오프라인(장치20·복귀20·상호작용12·이동8·정적6·로프8·Main)·Tilemap10·official-test10·경사16 재생과 Main, 공식 콘텐츠 검사 3종 통과. 지정 dotnet 런타임/에디터 오류0. [실행 범위·체크섬](Notes/2026-11-08-quantum-hazards.md).
- 제품 규칙이 바뀌면 확정 체크섬 값은 이전 기록과 달라질 수 있다. 같은 실행의 기록·재생과 두 연결 사이 일치로 판단한다.
- `QuantumProductChecks.RunOffline`은 새 플레이 세션에서 다른 Main 검사보다 먼저 실행한다(선택 콘텐츠가 첫 사용례라고 가정).
- 이전 Photon 기록은 각 Notes에 있다. 기존 2:2~6:6·official-test·경사·피격 Photon은 제품10에서 다시 실행하지 않았다.
- 다른 PC·실제 두 사람/3~4인·게임패드·배포 빌드·Steam 다계정/출시 인증·지연/손실 주입은 미실행이다. 개발 Photon 검증과 출시 인증을 구분한다.

## 남은 작업

- Steam 앱 발급 후 별도 출시 Photon 앱에서 Publisher Web API 키를 등록하고 공급자 `version=2`, `identity=photon`, `verifyOwnership=true`, 익명 거부를 설정·검증한다. [보안 기준](QUANTUM_SECURITY.md)을 따른다.
- 이동/전환 지형·스위치/문·컨베이어(외부 속도)·움직이는/회전 트랩·대포/투사체, 45도 초과/동적 콜라이더, 친구 맵 전송은 별도 이식 범위다.
- 공식 맵은 Unity 프리팹(Tilemap)+Quantum 작성 전용 컴포넌트로 만들고 변환한다. 21개 스테이지는 사용자가 새로 구성해 등록한다. MapEditor는 유저·친구 맵용이며 MapData 규칙을 유지한다. 원본 프리팹은 보존한다.
- 현재2인 전제를 최대4인으로 확장하고 키 재설정/게임패드·애니메이션/효과음/카메라를 연결한다.

## 다음 작업

[최신 착수 순서](NEXT_SESSION.md#다음-작업)에 따라 이동·전환 지형/발판부터 진행한다. 원본 Stage7 이식은 보류한다.
