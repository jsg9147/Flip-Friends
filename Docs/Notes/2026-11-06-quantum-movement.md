# Quantum 제품 이동 이식 조사·검증

- 기준: 2026-11-06 KST. 프로젝트와 연결 에디터 Unity `6000.6.8f1`, SDK `Assets/Photon/Quantum/build_info.txt`의 Quantum `3.0.14 Stable Build 2241`을 재확인했다. 버전 변경 없음.
- [공식 SDK 설치 표](https://doc.photonengine.com/quantum/v3/getting-started/initial-setup)는 이번 조회에서도 6000.5까지이며 6000.6은 미명시다. 아래 프로젝트 실행 성공과 공식 지원 보장은 구분한다.
- 기존 Main/QuantumPlay·제품 코드·생성 코드·문서 등 미커밋 변경을 보존했다. 자동 커밋·초기화·Mirror/FizzySteamworks 제거·씬/프리팹/맵 덮어쓰기는 하지 않았다. 새 검사 `.meta`는 Unity가 생성했다.

## 실제 활성 Mirror 경로

- `PlayerInputManager.OnMove/OnRun/OnJump` → `ClientMover.BuildInputPayload`의 movement/run/jump/jumpHeld/jumpUp → `MovementHandler.ApplyInput` → `Simulate(Time.fixedDeltaTime)`다. 서버도 같은 ApplyInput/Simulate를 실행하며 재시뮬레이션은 StatePayload의 velocity/velocityXSmoothing을 복원한다.
- 저장 Player 프리팹을 Unity API로 읽었다. 걷기4·달리기5.5·최대/최소 높이2.2/1.2·정점시간0.3초·runJumpHeight2다. FixedDeltaTime은 약0.02(50Hz)다. Quantum은 기존60Hz를 유지한다.
- 중력은 `-(2×2.2/0.3²)×0.9=-44`, 기본 점프13.2, 낙하 제한12다. 높이2.2/1.2라는 필드명이 그대로 실제 궤적의 정점 높이를 보장하지 않는다. 중력0.9와 매 틱 중력 후 이동, 키 해제 시점이 높이를 결정한다.
- 수평 목표는 입력×걷기/달리기 속도다. SmoothDamp의 smoothTime0.4초, 입력과 기존 속도의 부호가 다르면 ×0.7이다. `Mathf.Sign(0)=+1` 의미까지 유지한다. 공중 제어도 같은 식이며 별도 가속 계수가 없다.
- 달리기 보너스는 지상 점프 직전 `isRunPressed`일 때 `2×Clamp01((Abs(velocity.x)-4)/(5.5-4))`를 수직 속도에 더한다. 키만 켠 정지는 보너스0, 이전 속도4.75는1, 5.5는2다. 이번 틱 수평 스무딩 전 속도를 사용하며 반대 방향 입력 여부를 별도 제한하지 않는다.
- 점프 누름은 이전 등반/접지 상태에서 처리하고 해제는 상승 속도가 `sqrt(105.6)`보다 클 때만 그 값으로 자른다. 이어 타이머 감소 → 로프 진입/이탈 → 수평 SmoothDamp·중력 → 등반 속도 덮어쓰기 → X/Y 충돌 순서다. 로프 점프에는 달리기 보너스가 없다.
- 수평 충돌은 이동량을 제한하고 velocity.x/스무딩을 초기화하지 않는다. 바닥/천장은 velocity.y만0으로 만든다. 로프 중에도 스무딩 누적값은 계산되고 마지막에 속도를 양 축×4로 덮어쓴다.
- wallSliding/wallDirX 활성화 대입이 없으므로 벽 미끄럼·벽 점프를 추가하지 않았다. CanJump에 코요테 계산이 있어도 OnJumpInputDown의 below 조건이 선행하므로 새 공중 점프를 켜지 않았다. 밟기·운반·피격·상자·장치로 범위를 확대하지 않았다.

## 구현과 호환성

- `FlipFriendsMovementSystem.Simulate` 하나를 제품 시스템과 검사에서 사용한다. Unity SmoothDamp 호출 대신 [Unity 공식 참조 식](https://github.com/Unity-Technologies/UnityCsReference/blob/master/Runtime/Export/Math/Mathf.cs)의 무제한 maxSpeed 유리 근사와 초 단위 smoothTime을 FP로 계산한다. dt는 Quantum 프레임 값이다. Unity Time/Physics2D/Transform/코루틴은 판정 근거가 아니다.
- Qtn에 Run button·VelocityXSmoothing·WasJumpHeld를 추가하고 SDK 생성기를 실행했다. Jump button의 유지값과 프레임의 이전 유지값으로 누름/해제를 결정한다. 유지 중 재착지해도 점프를 반복하지 않는다. 도착 시 속도/스무딩을0으로 만들고 이후 입력은 무시한다.
- 양 축/Jump/Run이 입력 기록에 포함된다. 로프18틱과 첫 틱 차감·충돌 순서·확정 완료만 Unity에서 종료하는 경계를 유지했다. 시험 Bootstrap도 같은 제품 세션·입력과 시뮬레이션을 사용한다.
- MapData2.1·변환/팔레트v2·맵 원본과 베이크는 그대로다. 제품 합의는 `제품2`와 이동 규칙/도착 박스 해시로 갱신했다. 제품 개발/출시 온라인 AppVersion은 `ff-product-2`, 별도 기존 클라우드 시험은 `ff-product-2-test`다. 이전 입력/시뮬레이션을 같은 버전으로 합의하지 않는다.
- 고정 키: 오프라인 로컬슬롯0은 A/D·W/S·Space·왼쪽 Shift, 슬롯1은 방향키·오른쪽 Ctrl·오른쪽 Shift다. 온라인 각 연결의 키보드는 로컬슬롯0을 읽는다. 전역 PlayerRef·Photon 사용자ID·SteamID를 키보드 슬롯으로 쓰지 않는다. 온라인 자동 입력만 GetLocalPlayers로 전역 PlayerRef를 얻어 공통 시나리오에 넣는다.
- 기존 `KeyRebindingManager`는 단일 Player 액션 자산과 PlayerPrefs `bindings`를 저장한다. 이를 두 로컬 슬롯에 그대로 활성화하면 방향키/장치가 겹친다. 이번에는 고정 키를 유지했고 키 재설정·게임패드·Interact/Reset/Previous/Next는 Quantum 이동에 미연결이다. UI 개편으로 확대하지 않았다.
- Mirror50Hz와 Quantum60Hz의 시간 적분·FP 양자화 차이는 남는다. 물리/전체 스테이지와 비트 단위로 같은 궤적이라는 주장은 하지 않는다. 속도·시간·보너스 수치는 보정하지 않았다.

## 자동 검사와 입력 재생

- Main Play에서 `Check Product Offline And Main`을 새 실행했다. 먼저 `QuantumMovementChecks`가 실제 제품 세션/입력 콜백으로 새 이동 입력을 기록한다. 별도 새 러너가 기록을 재생하며 동일 확정120~960(120간격) 체크섬8개를 대조한다.
- 새 검사: 정지 가속/해제 감속 수렴·좌우 반전 누적값·달리기 전환·공중 동일 제어·보너스 정지0/중간1/최대2/키 해제0·해제 컷과 중력 순서·짧은/긴 점프·유지 중 착지 후 중복 금지·로프 점프 보너스 없음/18틱 제한/재진입·벽/천장 뒤 속도/누적값·도착 정리/입력 무시.
- 가상 New Input System 키보드로 양쪽 Shift·점프/해제·좌우·로컬 슬롯 독립을 검사하고 가상 장치를 제거해 원래 키보드를 복원했다. 실제 두 사람이 조작한 시험은 아니다.
- 동일 Quantum DeltaTime에서 Unity Mathf.SmoothDamp와 FP 이동을600틱 대조했다. 최대 수평 속도 오차0.0005775914, 검사 한계0.003이다. dt는 FP 원시1092/65536(약0.0166626초)다. 평지 점프 상승량은 짧은1.32219, 긴1.87007이다. 두 플레이어의 확정 상태에서 가속·감속·반전·달리기·공중·해제 컷을 모두 관찰했다.
- 새 이동 기록 재생 체크섬: 120=`6194907034274251637`, 240=`3468752287444885326`, 360=`3532428768949022234`, 480=`15692049618085123086`, 600=`14455080934848772765`, 720=`11820685477326967508`, 840=`3788494975070413917`, 960=`16320939019774569677`.
- 이어 기존 정적/로프 두 플레이어의 모든 상태 전이·비관통 단언을 유지했다. 제품 정적 재생6개 최종720=`10687505130516998661`, 로프 재생8개 최종960=`1994596849077362832`가 각각 새 러너와 일치했다. 로프 플레이어별 확정 관찰891개·최대 간격1틱·로프 점프1회다.
- Main 실제 Button.onClick → 준비 화면/지원 맵 선택 → QuantumPlay → 위 입력 전원 도착 → Main → 재진입/메뉴 종료를 통과했다. 미지원 원본 위치/원인 실패UI·출시 설정 실패/익명 폴백 없음·실패 재시도·시작 취소·중복 시작·외부 씬 왕복 후 러너0·제품 입력0·메뉴 콜백/표시 잔존 없음·Mirror 비활성을 검사했다.
- 자료는 Git 제외 `Logs/QuantumMovementChecks.txt`, `QuantumMovementReplay.json`, `QuantumProductStaticChecks.txt`, `QuantumProductRopeChecks.txt`, 각 제품 Replay.json, `QuantumProductOffline.txt`다. 이전 세션 결과를 이번 통과로 재사용하지 않았다.

## 실패 분석과 시나리오 수정

- 첫 제품 로프 회귀는 새 가속으로 우측 플레이어가 벽 로프 도달 전 x4.466에서 위 입력을 받아 도착 처리돼 실패했다. 확정 위치/속도로 원인을 확인했다. 벽 쪽 상향 입력을 시나리오280틱에서300틱으로 늦췄다.
- 이때300~360의 상승60틱은 y4.498993에서 멈춰 천장 접촉을 못 했다. 상향 구간을370틱까지 유지해 실제 천장 충돌을 발생시켰다. 귀환600~650의 불필요한 위 입력은 도착 트리거와 겹치므로 제거했다. 천장/벽/바닥·수평 이탈·재진입 등 기존 Required 단언과 비관통 허용 범위는 삭제/완화하지 않았다.
- 직접 로프18틱 검사 초기에는 좁은 중앙 로프에서 수평5.5로 출발해 제한 만료 전에 로프를 벗어났으므로 재진입 단언이 실패했다. 수평 속도를 유지한 채 벽 쪽 긴 로프를 사용하는 검사로 바꿔 접촉과 타이머를 분리했다.
- 자동 도착 운전은 즉시 속도를 뒤집던 이진 입력 대신 거리×2−현재 수평속도를 [-1,1]로 제한해 감속을 고려한다. 제품 이동 수치나 도착 판정 범위를 바꾸지 않았다.
- 최종 콘솔 점검에서 기본 SimulationConfig의 `UnloadPreviousSceneThenLoad`가 제품 맵에 기록된 QuantumRopeTest/QuantumCustomMapTest를 자동 로드하려는 오류와 후속 unload 예외를 확인했다. 제품 흐름은 씬 수명을 직접 소유하므로 전용 `FlipFriendsProductSimulationConfig.asset`을 기본 설정에서 복제해 AutoLoadSceneFromMap만 Disabled로 저장했다. Unity API로 생성/저장/재임포트한 뒤 새 도메인에서 GUID/모드를 재조회했다. 원본 설정·맵·씬은 변경하지 않았다. 제품 준비는 이 설정을 필수로 요구하고 수동 씬 수명도 합의 해시에 포함한다. 이후 오프라인/Photon 검사는 실행 중 Unity Error/Exception/Assert도 실패로 세도록 보강해 전부 재실행했고 최종 오류/예외0건으로 통과했다.

## 실제 새 개발 Photon 두 연결

- Main 유휴 Play에서 `Check Product Development Two Clients`를 실행했다. 에디터 전용 개발 시작 메서드와 새 QuantumGameSession 두 개를 사용했다. 먼저 참가자 대기 취소·서로 다른 콘텐츠 거부/정리 후 새 방으로 재시도했다.
- 실제 지역asia, RTT115/109ms, 같은 PC, 지연/손실 주입 없음. 로프960틱 뒤 새 이동 시나리오를1920틱까지 실행했다. 양 러너에서 두 플레이어의 로프 전체 전이와 가속·감속·반전·달리기·공중·점프 해제 상태를 단언했다.
- 동일 확정 체크섬15개: 240=`13477358481408994742`, 360=`18278820417542691229`, 480=`12078124282277239603`, 600=`3155507101422989713`, 720=`14615677358728308566`, 840=`7451138276465858282`, 960=`4239480112322798891`, 1080=`15277825624747095350`, 1200=`13950549472931051389`, 1320=`4038472806357724976`, 1440=`2852070331394603449`, 1560=`9375997409048162557`, 1680=`3863798471749921749`, 1800=`1029232579604598105`, 1920=`3365334970821842997`.
- 이어 위 입력 도착 완료 프레임은 양쪽2115로 일치했다. 종료 후 러너·Photon 연결·입력 콜백 정리=True. 각 러너의 로프 확정 관찰은 플레이어별871개·최대 간격1틱이다. 자료 `Logs/QuantumProductCloud.txt`.
- 서로 다른 세션의 체크섬이나 예측 프레임끼리는 비교하지 않았다. 구매 인증·출시 공급자/소유권/익명 거부의 증거가 아니며 출시 버튼에 개발 앱/익명 폴백을 넣지 않았다.

## 컴파일·표시·한계

- SDK Qtn 코드 생성과 Unity 컴파일/Mirror Weaver 완료. 번들 dotnet `build Assembly-CSharp.csproj`, `Assembly-CSharp-Editor.csproj` 및 참조 Quantum.Simulation/Unity/Unity.Editor/CodeGen 오류0. 샌드박스 기본 병렬 빌드는 오류 메시지 없이 실패해 `--no-restore -m:1`로 실행했다. 전체 런타임/참조 재빌드 경고162, 최종 증분 런타임0·에디터7이며 기존 벤더/API/미할당 필드 경고 등을 포함한다. 로그 `QuantumMovementRuntimeBuild.log`, `QuantumMovementEditorBuild.log`.
- Main에서 제품 정적 맵을 다시 열어 실제 표시를 확인했다. QuantumPlay 러너1·입력구독1·시험Bootstrap0·Unity Collider2D0·TMP 넘침0. 조작/달리기/가변 점프 안내 캡처 `Logs/QuantumMovementPlay.png`를 시각 확인했다.
- 최종 새 오프라인 재실행에서 이전 제품 규칙 합의 거부까지 통과했다. 종료 후 Main 편집 상태·활성 러너0·제품 입력0·표시0을 확인했다(`Logs/QuantumMovementFinalState.txt`). Main의 미저장 dirty 상태는 보존했고 강제 저장/재로드하지 않았다. 이번 작업은 씬/프리팹/맵 에셋을 수정하지 않았다.
- 이번 완료는 첫 제품 경로의 이동 이식이다. 전체 게임 Quantum 전환이 아니다. 밟기·운반·상자·장치·추락/리스폰·전체21개 스테이지·Steam 로비/초대·친구 맵 전송은 미이식이다.
- 다른 PC·Steam 다계정·출시 인증·실제 두 사람 조작·게임패드·플레이어 빌드 실조작·지연/손실 주입은 미실행이다. Steam 출시 앱은 미발급이며 보안 문서는 Draft를 유지한다.
- 다음 실제 교체는 밟기 → 운반/던지기 → 상자 → 리셋/추락·장치 → 기본 스테이지/친구 맵 공유 → Steam 로비/초대 순서다. 비활성 벽 점프는 별도 동작 결정 전 켜지 않는다.
