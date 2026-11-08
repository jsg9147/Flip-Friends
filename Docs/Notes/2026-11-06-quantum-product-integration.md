# Quantum 첫 제품 통합 조사·검증

- 기준: 2026-11-06 KST. 시작 Git 작업 트리는 깨끗했다. 프로젝트와 연결 에디터 Unity 6000.6.8f1, Quantum 3.0.14 Stable Build 2241. SDK/에디터 버전을 변경하지 않았다. CLI만 최신 beta.17로 갱신했다.
- [공식 SDK 설치 표](https://doc.photonengine.com/quantum/v3/getting-started/initial-setup)를 다시 조회했다. 6000.5까지 명시됐고 6000.6은 미명시다. 아래 프로젝트 실행 결과가 공식 지원 보장을 대신하지 않는다.

## 변경 전 실제 결합

- Main Start의 ScreenNavigationButton은 `mode-select`를 열었다. Host/Public/Private 화면 → SteamRoomManager의 Steam 로비 → SlimeRoomManager의 Mirror 방/게임 플레이어 → GameRoom 맵 선택/준비 → GamePlay StageManager 생성이었다. 맵 전송은 CustomRoomPlayer Command·서버 세션 캐시/클라이언트 캐시와 결합돼 있다.
- SteamRoomManager가 계정/친구 로비·입장 가능 여부를 관리하고 Mirror/FizzySteamworks가 연결/플레이어를 만든다. 기존 Player 프리팹에는 NetworkIdentity/NetworkTransform, Controller2D, PlayerController2D, ClientMover/ServerMover, MovementHandler, PlayerInputManager/PlayerInput, Rigidbody2D, Animator, 운반/상태/사운드가 붙어 있다.
- `PlayerController2D.OnTriggerStay2D`는 Finish 접촉과 위 입력이 있을 때 도착을 요청하고, 도착 상태가 이동/표시·콜라이더/애니메이션을 끈다. GameManager는 서버에서 전원 도착을 확인해 ReturnRoomScene으로 복귀한다. Quantum은 접촉만으로 자동 완료하지 않았다.

## 실제 교체와 보존

- Main Start → QuantumPrepareScreen → 검증된 정적/로프 맵 선택 → QuantumGameFlow/QuantumGameSession → QuantumPlay. 완료/메뉴 종료는 Main으로 돌아가고 다음 Start에서 결과 메시지와 함께 다시 준비한다.
- 준비 UI는 ScreenNavigator와 기존 InputManager 취소 입력을 사용한다. 플레이 메뉴 입력도 기존 InputManager 구독이며 종료 시 해제한다. 세션은 단일 시작·취소 토큰·제한 시간·러너/연결/콜백 정리를 소유하고, 표시 클래스는 판정을 하지 않는다.
- 기존 Player 프리팹의 Sprite만 사용한다. Quantum 엔티티를 기준으로 표시하며 Unity Collider/Physics2D나 Animator가 시뮬레이션을 바꾸지 않는다. 맵 표시도 FP 원본 지형/로프/도착 박스의 렌더러뿐이다.
- MapData 2.1·변환/팔레트 v2·기존 JSON/베이크/시험 씬은 유지했다. 도착 박스도 기존과 같은 FP 양자화·배치 오프셋/스케일·회전을 계산하고, 기존 지형 해시 뒤 `제품1` 규칙+도착 원시 값의 SHA-256을 덧붙여 온라인 합의한다. 콘텐츠 해시는 구매 인증이 아니다.
- FlipFriendsPlayer.IsFinished/FinishedFrame과 전역 FlipFriendsCompletedFrame은 Quantum 프레임 상태다. 확정 완료만 Unity가 받아 세션을 종료한다. 이동 수치/60Hz·벽 점프 비활성은 그대로다.
- `기존 Steam 게임 (Mirror)` → 기존 mode-select/GameRoom/GamePlay, MapEditor 테스트 플레이, Steam 로비·초대·친구 맵 전송은 아직 Mirror다. SDK/벤더를 수정·제거하거나 자동 커밋/초기화하지 않았다. Main/QuantumPlay·빌드 등록은 Unity API로 저장·재로드했다.

## 이번 오프라인 새 실행

- 메뉴: `Tools/Flip Friends/Quantum/Check Product Offline And Main`. Main Play 상태에서 실행한다. Bootstrap이 없는 Main에서 제품 세션을 시작하고 제품 입력 콜백을 기록한다.
- 정적 지형 두 플레이어 생성·걷기/점프·바닥/좌우 벽/천장 접촉·비관통을 통과했다. 새 러너 재생의 동일 확정 120~720(120 간격) 체크섬 6개 일치, 720=`16629492837420807181`.
- 로프 두 플레이어의 자동 진입 없음·상하 진입/상승/하강/정지·상단/수평 이탈·점프 분리·18틱 제한/재진입·벽/바닥/천장·벽 미끄럼 자동 활성 없음 단언을 통과했다. 새 러너 재생의 동일 확정 120~960 체크섬 8개 일치, 960=`3988582437409947062`.
- 실제 Main Button.onClick → Quantum 준비 화면 → 로프 선택/오프라인 시작 → 위 입력 전원 도착 → Main → 정적 맵 재진입 → 플레이 종료를 통과했다. 입력은 자동 공급이며 실제 두 사람이 키보드를 조작한 결과는 아니다.
- 미지원 interactive.box 원본의 위치/원인 실패 UI·부분 시작 없음, 출시 App ID 미설정 실패/익명 폴백 없음, 실패 후 재시도·시작 취소·중복 시작·외부 씬 로드 후 종료도 단언했다. 뒤에는 활성 러너 0개·제품 입력 콜백 0개·메뉴 콜백 잔존 없음·표시 객체 없음·Mirror 서버/클라이언트 비활성이다.
- 자료: Git 제외 `Logs/QuantumProductOffline.txt`, `QuantumProductStaticChecks.txt`, `QuantumProductRopeChecks.txt`, 두 맵의 `QuantumProduct*Replay.json`.
- 기존 로프/정적 시험 씬도 각각 다시 열어 Play하고 `QuantumRopeChecks.Run`/`QuantumCustomMapChecks.Run`을 실행했다. 변환 반복/역순·미지원 데이터·검증 에셋 거부와 기존 상태 전이·새 러너 재생 8/6개가 제품 코드 분리 후에도 통과했다.

## 최종 저장·컴파일·표시

- SDK Qtn 코드 생성과 Unity 컴파일/Mirror Weaver 뒤 런타임/에디터 및 참조 Quantum.Simulation, Quantum.Unity, Quantum.Unity.Editor, Quantum.Unity.Editor.CodeGen 빌드 오류 0. 런타임 경고 112개·에디터 증분 경고 7개는 기존 API/벤더·미할당 필드 등을 포함한다. `Logs/QuantumProductRuntimeBuild.log`, `QuantumProductEditorBuild.log`.
- Main/QuantumPlay를 Unity API로 저장·재로드해 dirty=false, 시험 Bootstrap 0개, 빌드 첫 Main과 QuantumPlay 포함을 확인했다. 두 manifest의 원본·v2 해시·베이크/제품 도착 규칙을 다시 검증했다. `Logs/QuantumProductFinalSceneCheck.txt`.
- 실제 Main 진입 플레이 화면에서 러너 1개·시험 Bootstrap 0개·Unity Collider/Collider2D 0개·TMP overflow 0개를 확인했다. 한글 글꼴을 연결해 기호 누락을 수정하고 준비/플레이 화면을 시각 확인했다. 캡처는 Git 제외 `Logs/QuantumProductPrepareFinal.png`, `QuantumProductPlayFinal.png`다.
- Unity 직렬화의 빈 문자열 공백은 그대로 유지했다. 소스/문서 diff 공백 검사와 씬 직렬화 공백은 구분한다. 검증 종료 후 Play를 끄고 Main을 다시 열었으며 활성 러너·제품 입력 콜백은 0개다.

## 이번 실제 개발 Photon 새 실행

- 메뉴: `Tools/Flip Friends/Quantum/Check Product Development Two Clients`. 제품 QuantumGameSession의 에디터 전용 시작만 사용한다. 일반 온라인 버튼은 이를 호출하지 않으며 플레이어 빌드에 메서드가 없다.
- 참가자 대기 중 취소 → 시작 작업 종료/Photon 연결 정리, 서로 다른 정적/로프 맵 → 불일치 예외 또는 상대 대기 제한 시간 취소 → 러너 시작 없음·연결/콜백 정리 후 새 세션으로 재시도했다. 검사 초기에는 두 작업 모두 Faulted라고 가정해 실패했으며 Canceled도 정상 실패로 다루도록 고쳤다.
- 최종 새 두 연결: asia, 같은 PC, 측정 RTT 114/111ms, 지연/손실 주입 없음. 연결 번호를 전역 PlayerRef로 간주하지 않고 각 러너의 GetLocalPlayers로 입력 대상을 찾는다.
- 양쪽 로프 상태 전이 완료 및 동일 확정 체크섬: 240=`11467401774956239271`, 360=`10706570113424823069`, 480=`10744554608557724040`, 600=`16130061249325902039`, 720=`14214159636138250185`, 840=`2235942452570720767`, 960=`4992925787348085690`.
- 이어 제품 도착 입력으로 양쪽 완료 프레임 1061 일치. 종료 후 러너·Photon 연결·제품 입력 콜백 정리=True. 자료: `Logs/QuantumProductCloud.txt`.
- 이전 세션/Bootstrap 결과를 이번 통과 근거로 사용하지 않았다. 세션마다 시작/플레이어 등록 프레임이 달라질 수 있어 서로 다른 세션의 체크섬을 비교하지 않고 같은 기록 재생 또는 같은 온라인 세션의 동일 확정 프레임만 대조했다.

## 한계와 후속

- 첫 제품 통합 경로 완료이며 전체 게임 Quantum 전환 완료가 아니다. 현재 2인·정적 박스/수직 로프·기본 이동/점프·도착만 지원한다. 운반/밟기·상자·장치·가변 점프/가속/달리기·키 재설정·추락/리스폰·21개 기존 스테이지는 미이식이다.
- 출시 Steam 앱/Photon 공급자·서버 소유권/익명 차단·실계정·Steam 다계정·다른 PC·지연/손실 주입·실제 두 사람/게임패드 조작·플레이어 빌드 실조작은 미실행이다. 출시 시작 실패 상태는 유지하며 개발 익명 경로로 대체하지 않는다.
- 다음은 제품 경로에서 가변 점프·가속/달리기 → 밟기/운반/상자 → 리셋·장치/스테이지 → 친구 맵 전송·Steam 로비/초대 순서다.
