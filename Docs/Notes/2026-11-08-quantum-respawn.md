# Quantum 제품 리셋·추락 복귀 이식

## 시작 상태와 실제 Mirror 동작

- Unity 6000.6.8f1·Quantum 3.0.14 Stable Build2241. 버전을 변경하지 않았다. 연결 에디터 Main은 시작 시 loaded/active, dirty=false, Play 정지 상태였다. 기존 Git 변경·미추적 파일은 보존했다. Main을 저장하거나 폐기하지 않았다.
- PlayerController2D.ServerRespawn은 들린 플레이어 요청을 무시하고 DropAll → ServerMover.Teleport(GetRespawnPosition(), zero)를 실행한다. 목적지는 SavePoint가 있으면 그 위치, 없으면 최초 생성 위치다. SavePoint는 서버가 더 큰 ID만 수용한다(같은 ID 유지).
- Reset 태그의 RespawnHandler는 동일 물리 스텝 중복 접촉을 막고 ServerResetSection을 호출한다. 이 경로는 운반을 유지하며 onlyBoxReset은 상자를 든 플레이어만 이동시킨다. 수동 리셋/추락과 다르다.
- PickupObj는 서버 시작 위치를 homePosition에 저장한다. 들리지 않았을 때 이동 후 추락 검사 → ServerReset → Release로 속도/스무딩/충돌/운반/표시/동기화 위치를 정리한다. ObjSummonSwitch는 별도 summonLocation으로 같은 API를 호출하며 기본 쿨다운5초다.
- ServerMover.Teleport는 Launch로 속도/외부속도/스무딩/등반/발판을 정리하고 상태를 다시 적용하며 에포크·큐·마지막 입력을 초기화한다. Launch의 기존 타이머 일부는 max/유지라 Quantum의 일괄 정리와 완전히 같지 않다.
- FallBoundary.Margin=10. MapRenderBounds는 활성 SpriteRenderer/TilemapRenderer의 로컬 범위를 팔레트 배치 위치/회전/스케일로 변환한다. x<minX, x>maxX, y<minY만 검사하며 경계 자체와 위쪽은 제외한다. 플레이어/상자 모두 중심 좌표 기준이고 운반 중 개별 추락 검사는 없다.
- 기존 Reset 기본은 Keyboard R/Gamepad rightTrigger. PlayerPrefs bindings와 단일 Player 액션에는 로컬2슬롯 소유권 구분이 없다. UI 전체를 개편하지 않고 Quantum 고정 슬롯의 1P R·2P Backspace를 연결했다. 기존 재설정/패드 지원으로 표시하지 않는다.

## 이식 정책과 결정론

- 제품4/ff-product-4: Quantum 입력 Reset과 프레임 WasResetHeld로 상승 엣지를 소비한다. 유지·중복 폴링/재생으로 반복 리셋하지 않는다. 들린 동안 Reset은 소비하고 무시한다. 이미 도착한 플레이어·완료 세션은 복귀를 다시 처리하지 않는다.
- 처리 순서는 복귀 요청/이전 틱 추락 → 상호작용 → 플레이어 이동 → 상자 이동 → 운반 추종 → 새 추락 복귀 → 전원 완료다. 플레이어 요청/복귀 대기는 전역 PlayerRef 순서, 상자는 정렬된 콘텐츠에서 생성된 EntityRef 순서다.
- 최초 플레이어/상자 위치, 속도·스무딩·운반 양방향 참조·접촉·로프·밟기·Shrink·타이머·입력 누름 이력을 qtn 프레임에 둔다. 경계는 작성/시작 시 MapRenderBounds+10에서 FP로 변환·검증해 RuntimeConfig와 제품 합의에 넣는다. 시뮬레이션은 Unity Time/Physics/Transform/Animator/런타임 렌더러를 읽지 않는다.
- 공통 복귀는 속도/스무딩·접지/좌우벽/천장·등반/로프접촉·밟기 대상/속도기여·Shrink·점프/등반/던지기 제한·해제/탈출 기한을 초기화한다. PlayerRef·최초 위치·방향·진단 카운터는 유지한다. Jump/Interact/Reset의 누름 이력은 현재 틱 입력을 소비해 복귀 직후 가짜 재누름을 막는다. 수평/수직/Run은 별도 누적 상태가 없으며 복귀 다음 틱부터 정상 입력한다.
- 운반자는 먼저 기존 안전 놓기(앞→위→몸)를 시도한다. 성공한 대상은 그 자리에서 운동하며, 경계 밖이면 자기 초기 위치 복귀를 예약한다. 놓기 실패 시 양방향 관계를 끊고 대상도 자기 초기 위치로 예약한다. 상자 자체 복귀도 운반자 Held/ReleaseFrame과 상자 Carrier를 함께 정리한다.
- 목적지가 동적 대상에 점유됐으면 임의 위치/오프셋으로 옮기지 않고 복귀 대기한다. 대기 중 위치는 자기 초기 위치이며 표시/충돌/운반·밟기 탐색/이동/도착에서 제외한다. 비면 같은 순서로 재생성한다. 지형 속/경계 밖 초기 위치는 콘텐츠에서 거부하고, 손상된 프레임에서도 대기해 무한 복귀 카운트·재운반 루프를 막는다. 복귀한 틱 전체는 입력/밟기/들기/도착을 차단한다.
- 차이: Mirror의 무검사 놓기 fallback 대신 안전 놓기 실패 시 대상 복귀, 점유 시 충돌 없는 대기, 복귀 틱 조작 차단, 운동 타이머 일괄 정리를 명시적으로 도입했다. 정상 놓기로 해제된 대상의 Shrink 등은 일반 해제 동작을 따르며 자기 복귀가 예약된 경우에만 공통 초기화한다.
- 체크포인트/구간 Reset/소환 장치는 이식하지 않았다. 팔레트 ID/프리팹 컴포넌트 허용 목록에서 위치·원인과 함께 콘텐츠 전체를 거부한다. 비활성 wallSliding/wallDirX와 일반 점프에 미연결인 BlockJump를 켜지 않았다.

## 콘텐츠와 보존

- 별도 FlipFriendsRespawnMap 원본/베이크/manifest/QuantumRespawnTest 씬을 Unity API로 additive 생성·저장·재로드했다. 기존 씬·프리팹·맵은 덮어쓰지 않았다. 새 맵은 양쪽으로 떨어질 수 있는 지형·상자·로프·도착을 제공한다. Main 준비 화면의 4번째 버튼으로 등록한다.
- MapData2.1 유지. 기존 정적/로프2:2·상호작용3:3의 원본·베이크·기존 콘텐츠 해시는 보존한다. 제품 시작 때 경계/초기 위치 안전성을 재검증하고 제품4 합의에 경계를 추가한다. 새 복귀 맵만 변환/팔레트4:4이고 경계가 canonical 콘텐츠 해시에도 들어간다. 이전 제품3과 같은 AppVersion으로 합의하지 않는다.
- 제품 SimulationConfig.AutoLoadSceneFromMap=Disabled 유지. 출시 Photon/Steam 미설정 실패·개발 앱 거부·익명 폴백 없음도 유지한다.

## 검증과 측정

- 최종 Check Product Offline And Main 통과. Reset 키 슬롯/누름·유지1회/해제후 재누름/들린 요청 무시, 정확한 경계/1raw 바깥/위쪽 제외, 속도/스무딩/로프/밟기/Shrink/타이머 정리, 운반자/들린 대상/상자 복귀, 놓을 공간 없음, 목적지 점유/지형 속/경계 밖 대기, 중복 복귀/같은 틱 들기·점프·도착 차단, 미지원 SavePoint/RespawnHandler/ObjSummonSwitch 거부를 검사했다.
- 새 제품 입력 기록 → 새 러너 재생의 확정120~2400(120간격) 체크섬20개 일치. 최종2400=7799402321825515121. 관측 복귀5회·플레이어운반리셋/상자운반리셋/추락/상자복귀 모두True. Logs/QuantumRespawnChecks.txt, QuantumRespawnReplay.json, QuantumRespawnTrace.txt.
- 같은 최종 코드로 기존 이동8개·정적6개·로프8개·상호작용12개 재생 일치. Main 실제 버튼 콜백 → 새 복귀 맵 → 기록 입력 시나리오 → 위 입력 전원 도착 → Main → 재진입/메뉴 종료를 실행했다. 실패·취소·중복 시작·외부 씬 왕복·러너/입력/메뉴/표시 잔존 검사도 유지했다. 최종 오프라인 실행 중 Unity Error/Exception/Assert0. Logs/QuantumProductOffline.txt, QuantumRespawnMainTrace.txt.
- 제품4 새 개발 Photon 방3개: 로프/이동 확정 체크섬15개·완료2078·RTT101/117ms, 상호작용11개·완료1672·RTT112/116ms 일치. 새 복귀 맵은 확정240~2400의19개·완료2640·RTT108/116ms 일치. 모두asia·같은 PC·지연/손실 주입 없음이다. 양쪽 복귀5회와 플레이어운반리셋/상자운반리셋/추락/상자복귀 상태 단언이 모두True였다. 새 복귀2400=10444438068602979227. 최종 전체 Photon 실행/정리 중 Unity 오류0, 종료 후 러너/연결/입력 콜백 정리=True. Logs/QuantumProductCloudMovement.txt, QuantumInteractionCloud.txt, QuantumRespawnCloud.txt, QuantumRespawnCloudTrace.txt, QuantumProductCloud.txt. 이전 세션 결과를 재사용하지 않았다.
- SDK qtn 코드 생성·Unity 컴파일/Mirror Weaver 완료. 저장소 지정6000.6.3f1 번들 dotnet으로 Assembly-CSharp 및 참조 Quantum 어셈블리 빌드 오류0/경고163, Assembly-CSharp-Editor 오류0/경고7. Logs/QuantumRespawnRuntimeFinal.log, QuantumRespawnEditorFinal.log. 첫 --no-restore는 Temp 자산 파일 부재로 실패해 정상 restore 빌드로 수정했다. 현재 Unity CLI WindowsApps alias가 실행되지 않아 이미 연결된 Unity MCP API를 사용했다.
- 준비/플레이 화면을 실제 캡처·시각 확인했다. 지원 맵4·플레이 러너1/입력1·Bootstrap0·Unity Collider2D0·TMP overflow0, 종료 후 러너0/입력0을 확인했다. SimulationConfig 자동 씬 로딩 Disabled 유지. Logs/QuantumRespawnPrepare.png, QuantumRespawnPlay.png. 마지막 Main 편집 상태를 강제 저장하지 않았다.

## 실패 분석

- 첫 실제 입력은 상자 접근 전에 플레이어 우선 탐색으로 파트너를 다시 집어 상자 복귀를 관찰하지 못했다. 두번째 시도는 파트너가 왼쪽으로 지나치게 달려 추락·원점 복귀한 뒤 다시 들렸다. 확정 위치/운반 관계로 원인을 확인하고 파트너를 (-3)으로 감속 정지시키고 상자 들기 시작 시점을 늦췄다. 단언/탐색 거리·이동 수치는 바꾸지 않았다.

## 미실행과 후속

- 다른 PC·실제 두 사람 조작·Steam 다계정·출시 공급자/소유권/익명 차단·게임패드·인위적 지연/손실 시험은 미실행이다. 같은 PC 개발 Photon 두 연결을 이 항목들의 통과로 취급하지 않는다.
- 첫 제품 경로 확장이며 전체 Quantum 전환이 아니다. 피격/스프링/바운스/발판/스위치/체크포인트/구간Reset/소환/21개 스테이지·친구 맵 전송·Steam 로비/초대는 후속 범위다. 공식 SDK 표에서6000.6이 명시되지 않았다는 기존 기록과 이 프로젝트 실행 성공을 구분하며 이번에는 표를 재조회하지 않았다.
