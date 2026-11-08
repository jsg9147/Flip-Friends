# Quantum 체크포인트·구간 Reset·상자 소환

## 환경과 보존

- Unity6000.6.8f1·Quantum3.0.14 Stable2241. 버전은 바꾸지 않았다. 공식 지원 표에서6000.6이 명시되지 않았다는 기존 기록과 프로젝트 실행 결과는 별개이며 이번에는 표를 재조회하지 않았다.
- 시작 시 Main loaded/active/dirty=true·Play 정지였다. 기존 미커밋·미추적 파일을 보존하고 Main을 강제 저장하거나 폐기하지 않았다. 새 맵/장치 콘텐츠/베이크/manifest/작성 씬은 Unity API로 별도 생성·저장·재로드했다. GUID를 직접 작성하지 않았다.
- CLI의 WindowsApps 별칭이 실행되지 않아 기존 연결 Unity Pipeline 도구를 사용했다. 제품 SimulationConfig.AutoLoadSceneFromMap=Disabled와 QuantumGameFlow 씬 수명 소유를 유지한다.

## 실제 Mirror 조사

- SavePoint는 Player 태그의 TriggerEnter만 처리하며 서버가 플레이어별 더 큰 ID만 수용한다. 같은 ID는 먼저 도달한 위치를 유지한다. 실제14개 중 중복 진행 ID가 있어 진행 ID의 중복을 금지하지 않는다. Quantum 안정 장치 ID와 진행 ID는 별개다.
- RespawnHandler는 TriggerEnter/CollisionEnter를 받고 같은 물리 스텝·동일 대상의 중복을 제거한다. Reset 태그의 상자는 Player 전용 마스크에서도 직접 복귀한다. Stage7 마스크2304(Player+Pickable), 나머지 실제 장치2048(Player)이며 기존 Reset 판정은 Tilemap 트리거다.
- ServerResetSection은 들린 플레이어를 무시하고, onlyBoxReset=true이면 상자를 든 플레이어만 이동시킨다. 플레이어를 든 경우에는 허용하지 않는다. 구간 복귀는 운반을 유지하며 리셋키·추락의 DropAll과 다르다.
- ObjSummonSwitch는 Player 태그의 TriggerEnter만 받고 실제9개 모두 cooldownTime=5초다. Box Switch 원본 판정은0.79×0.2/오프셋(0,-0.39), 표시 Switch_0/Switch_1이다. 클래스 기본값으로 프리팹 값을 덮어쓰지 않았다. 기존9개 프리팹·스테이지 연결은 바꾸지 않았다.
- 소환 성공은 PickupObj.ServerReset→Release→StateReset으로 운반자/상자의 양방향 참조·충돌 제외·표시·속도·스무딩을 정리한다. 상자 최초 homePosition과 소환 좌표는 별개다. 실제 Stage8 첫 버튼과 초기 소환 위치가 겹치는 배치도 있으며 임의 보정하지 않았다.
- PlayerController2D/PlayerInteraction/ServerMover/MovementHandler/StageManager의 구간 이동·수동 복귀·운반·추락 흐름을 대조했다. wallSliding/wallDirX와 일반 점프에 미연결인 BlockJump는 활성화하지 않았다. 상세 원본 배치 조사 출력은 Git 제외 Logs/QuantumDeviceBaseline.txt다.

## 결정론적 콘텐츠

- 기존 MapData2.1은 변경하지 않았다. 새 FlipFriendsDeviceContent는 별도 검증된 장치 콘텐츠이며 manifest가 원본 맵과 함께 소유한다. 기존 팔레트/커스텀 맵 편집·전송에 장치 작성 기능을 추가한 것은 아니다.
- 작성기의 Read는 실제 SavePoint/RespawnHandler/ObjSummonSwitch에서 판정·접촉 마스크·목적지·onlyBoxReset·대상 상자·실제 쿨다운·렌더러 범위를 추출한다. 새 예제는 기존 체크포인트/소환 프리팹을 복제하고, 새 구간 박스는 기존 마스크/조건을 명시적으로 설정한다. 원본 스테이지의 Tilemap을 박스로 자동 단순화하지 않는다.
- 이번 지원 형태는 원본 크기·축 정렬·단일 루트 박스 트리거다. Tilemap/원형/다중/동적/합성/회전 판정과 누락/중복 장치 ID·미지원 마스크·잘못된 상자/목적지는 위치·원인과 함께 전체 거부한다. 기존 전체 스테이지는 여전히 Mirror이며 전체 스테이지 변환기로 표시하지 않는다.
- 장치는 ordinal 문자열 ID로 정렬한다. 상자는 기존 정렬된 FP 초기 배치 좌표가 유일한 콘텐츠 참조이며 이를 동일한 ContentIndex로 해석한다. Unity instanceID나 런타임 탐색 순서는 사용하지 않는다. 장치 최대32개·상자64개를 검증한다.
- 장치 판정/진행ID/목적지/조건/대상/쿨다운/원본 렌더러 범위와 확장 경계를 canonical 해시에 넣는다. 기존 MapRenderBounds+여백10에 장치 Sprite 범위를 합친다. TMP 표시는 MapRenderBounds와 같이 경계 판정 근거에서 제외한다.
- 새 맵만 변환/팔레트5:5, 전 제품 규칙5/ff-product-5다. 기존2:2/3:3/4:4의 원본/베이크/콘텐츠 해시는 보존하고 제품 시작 때 기존처럼 복귀 경계·초기 위치를 재검증한다. 이전 제품4와 같은 AppVersion으로 합의하지 않는다.

## 프레임 상태와 확정 정책

- 체크포인트 활성 여부/ID/위치, 별도 RespawnPosition, SectionPending, 장치별 Player/Box 진입 이력, 쿨다운 종료 틱·마지막 소환 틱과 관측 카운터를 qtn 프레임에 둔다. Unity 시간/Physics/Transform/Animator/코루틴을 시뮬레이션 판정 근거로 읽지 않는다.
- 플레이어별 더 큰 진행ID만 갱신한다. 같은 틱 다중 접촉도 최댓값을 선택하며 동률이면 안정 장치ID 순의 최초 위치다. 들린 플레이어는 기존처럼 Player 트리거 접촉을 허용한다. 도착/복귀 대기·복귀한 틱의 접촉은 효과 없이 소비한다. 수동 Reset/추락은 활성 체크포인트, 없으면 최초 생성 위치로 간다. 최초 플레이어/상자 위치는 덮어쓰지 않는다.
- 구간 Reset은 들린 플레이어 요청을 무시하고 onlyBoxReset의 기존 의미를 보존한다. 상자 직접 접촉은 별도 목적지 복귀이며 운반 중 상자는 Mirror의 비활성 콜라이더처럼 직접 접촉에서 제외한다.
- 운반자 몸체와 들린 대상 전체 판정에 대해 목적지 점유를 검사한다. 막히면 운반 관계를 유지한 채 두 대상을 목적지 좌표에서 숨기고 충돌·이동·입력·탈출·도착·체크포인트·개별 추락 대상에서 제외한다. 수동 Reset은 소비하고 무시한다. 대기 그룹에 속한 상자의 소환도 거부한다. 다른 자유 상자의 소환은 독립적으로 작동한다.
- 공간이 비면 두 대상을 같은 틱에 복귀시키며 위치/이전 위치·속도/스무딩·접촉·로프/밟기/Shrink·운동 타이머를 함께 정리한다. 운반 참조·진행ID·최초 위치·관측 카운터는 유지한다. 해제9틱/탈출90틱·요청15틱은 복귀 시 재시작한다. 복귀 틱 조작은 차단하고 누름 이력을 현재 입력으로 소비한다.
- 확정 정책에 따른 차이: Mirror의 점유 검사 없는 구간 순간이동 대신 원자적 운반 대기를 사용한다. 소환도 목적지가 플레이어/다른 상자/지형에 막혔으면 상자의 위치·속도·관계·복귀 상태를 전혀 바꾸지 않으며 쿨다운을 소비하지 않는다.
- 소환 성공 때만 양방향 관계·충돌 제외·운동/복귀 상태를 정리하고 쿨다운을 같은 틱에 시작한다. 여러 플레이어는 PlayerRef 순, 여러 장치는 안정ID 순이며 동일 상자는 한 틱에 한 번만 소환된다. 실패/쿨다운 만료 중 유지 접촉은 재실행하지 않으며 이탈 후 재진입이 필요하다. 쿨다운0도 같은 틱 중복을 막는다.
- 처리 순서: 수동Reset/이전추락·대기 복귀 → 들기/던지기/밟기 → 이동 → 상자 이동 → 운반 추종 → 새추락 → 체크포인트 → 구간Reset → 소환 → 도착/전원완료. 순간이동 목적지의 접촉을 소비해 재운반·재Reset 루프를 막는다. 기존 장치 없는 맵의 도착 처리 위치는 보존했다.
- Unity 표시는 프레임의 남은 쿨다운으로 버튼 Sprite/남은 초를 표시한다. 플레이어/상자 표시와 입력/메뉴 콜백은 기존 제품 수명을 따른다. Main 다섯 번째 선택은 별도 장치 맵이고 시험 Bootstrap/Mirror 플레이어를 생성하지 않는다.

## 자동 검사와 실행 결과

- 장치 경계 검사와 새 제품 입력 기록→새 러너 재생의 확정120~2400/120간격20개가 일치했다. 실제 입력으로 CP 갱신·상자 운반 구간 복귀·대기·막힌 소환 거부·이탈/재진입 후 성공·CP 수동Reset을 관찰한다. 확정2400=1504269023730722095다. Logs/QuantumDeviceChecks.txt·QuantumDeviceReplay.json·QuantumDeviceTrace.txt에 기록했다.
- 검사 범위: 미접촉/최초/같은/이전/더큰/동시ID·플레이어 독립, CP 뒤 수동/추락·최초위치 보존, onlyBox 빈손/플레이어 거부·상자 허용, 플레이어/상자 운반대기·몸체/들린대상 점유·중복·공간해제 원자복귀, 대기 중 Reset/탈출/CP/소환 우선순위, 상자 직접Reset, 소환 실패 상자 전체상태 불변·성공 관계정리·쿨다운 경계·동시/중복/유지 접촉·소환후 추락 원위치, 완료후 입력 무시, 장치/참조/경계/위치/쿨다운 해시·잘못된 연결/형태 거부다.

- 최종 Check Product Offline And Main 통과: 기존 이동8/정적6/로프8/상호작용12/복귀20 재생과 장치20개가 일치했다. Main 실제 UI→새 장치 맵→운반대기/막힌소환 거부/재진입 성공/CP복귀→전원도착→Main→재진입/메뉴종료, 실패/취소/중복/외부씬 왕복·러너/입력/메뉴/표시 잔존 검사를 모두 통과했다. 해당 실행 Unity Error/Exception/Assert0. Logs/QuantumProductOffline.txt·QuantumDeviceMainTrace.txt.
- 새 제품5 개발 Photon 네 방: 로프/이동15개·완료2078·RTT114/106ms, 상호작용11개·완료1580·RTT116/111ms, 복귀19개·완료2643·RTT112/108ms 일치. 새 장치 맵은19개·완료2686·RTT106/107ms이며 양쪽 CP/운반/대기/소환성공/거부/CP리셋=True, 확정2400=16199973030325376849다. 모두asia·같은 PC·지연/손실 주입 없음. 전체 실행/정리 중 Unity 오류0, 종료 후 러너/연결/입력 구독 정리=True. Logs/QuantumProductCloud.txt·QuantumDeviceCloud.txt·QuantumDeviceCloudTrace.txt. 이전 세션/Bootstrap 결과를 재사용하지 않았다.
- 해시/제품 합의 숫자 표기를 InvariantCulture로 고정하고 음수 기호를 바꾼 문화권에서도 동일 콘텐츠/합의를 검사했다. 같은 새 입력 재생20개·확정2400=1504269023730722095를 다시 확인했다. 그 뒤 새 Photon 장치 방도 확정19개·완료2687·RTT114/115ms·확정2400=10788101965913094414 일치, 양쪽 장치 상태 True·Unity 오류0·종료 정리 통과다. asia·같은 PC·주입 없음. Logs/QuantumDeviceCloudFinalStatus.txt가 최종 장치 실행 근거이며 네 방 전체 회귀 결과는 위 로그를 따른다.
- SDK qtn 코드 생성·Unity 컴파일/Mirror Weaver 완료. 지정6000.6.3f1 번들 dotnet의 Assembly-CSharp 및 참조 Quantum 빌드 오류0, 전체 첫 빌드 경고165·최종 증분111, 에디터 오류0/자체 기존경고7(참조 재빌드 포함118), 마지막 런타임 증분 오류0/경고0. Logs/QuantumDeviceRuntimeFinal.log·QuantumDeviceEditorFinal.log. 기본 병렬 복원이 무진단 실패해 -m:1로 실행했다.
- 준비/플레이 화면을 실제 캡처·시각 확인했다. 지원 맵5·러너1/입력1·Bootstrap0·Unity Collider2D0·TMP overflow0, 자동 씬 로딩 Disabled다. Logs/QuantumDevicePrepare.png·QuantumDevicePlay.png.

## 실패 분석

- 첫 Main 종료 자동 입력은 소환된 상자를 넘으려 점프하면서 높은 구간Reset 트리거에 반복 진입했다. 확정 좌표·구간횟수 증가와 실제 입력으로 원인을 확인했다. 종료 입력을 상자를 들어 운반하는 경로로 바꿨으며 게임 판정·이동 수치·단언·허용 범위는 바꾸지 않았다.
- 첫 dotnet 기본 병렬 복원은 오류 메시지 없이 자식 MSBuild 실패로 종료됐다. 동일 지정 SDK의 `-m:1` 빌드로 실행했으며 결과와 로그는 최종 검증에 기록한다. Unity CLI/스킬 버전을 임의 설치·변경하지 않았다.

## 미실행과 다음 순서

- 다른 PC·실제 두 사람 조작·Steam 다계정·출시 Steam 공급자/소유권/익명 차단·게임패드·인위적 지연/손실 주입은 미실행이다. 개발 Photon 같은 PC 두 연결을 이 항목들의 통과로 간주하지 않는다. 출시 앱은 미발급이고 보안 문서는 Draft다.
- 이번은 첫 제품 경로의 기능 확장이다. 피격·스프링·바운스·이동 발판·일반 스위치·전체21개 스테이지·친구 맵 전송·Steam 로비/초대는 이식하지 않았다.
- 다음은 실제 기본 스테이지의 Tilemap/장치 판정 지원·명시적 거부를 작은 묶음부터 확장하고, 이후 커스텀 맵 선택/친구 전송 → Steam 로비/초대 진입을 교체한다.
