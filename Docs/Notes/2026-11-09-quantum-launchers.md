# Quantum 스프링·바운스 — 제품10

- 상태: Historical (제품10 구현·검증 기록). Unity6000.6.8f1·Quantum3.0.14 Stable2241 유지.
- 합의는 제품10/ff-product-10이며 `LauncherRules`에 진입·형상·힘·속도 덮어쓰기·상태 유지·다중 순서를 포함한다. 장치가 없는 기존 콘텐츠 해시는 유지한다.

## 원본 기준

- `MovementHandler.OnTriggerEnter2D`: Bounce/Spring 태그에 진입하면 `(플레이어 Transform − 장치 Transform).normalized × 힘`으로 velocity를 덮어쓴다. 코드 기본 bounceForce30과 달리 실제 Player.prefab은20, springJumpForce50이다.
- 실제 `Obstacle/bouncy ball.prefab`은 원형 트리거·반지름1·배율1이다. 저장된 프리팹/씬에는 Spring 태그 배치가 없어 코드50을 별도 공식 시험에서 사용한다.
- 서버와 소유 클라이언트가 적용한다. 무적·조작 제한·운반·로프 조건 검사는 없고 관계/등반/수평 스무딩/타이머를 바꾸지 않는다. 들린 플레이어의 Collider는 유지된다. 해제/던지기는 기존처럼 별도 속도로 덮어쓴다.
- 진입에서만 적용하며 유지 접촉은 재발동하지 않는다. 속도 변경은 다음 이동 틱에서 중력·수평 SmoothDamp·로프 속도 합성·점프 해제 컷을 그대로 거친다. 상자에는 이 콜백이 없다.

## Quantum 이식

- `FlipFriendsLauncherSystem`: 이동/장치/피격 뒤·완료 앞에서 판정한다. 원본에서 불명확한 같은 틱 교차 이벤트 순서는 피격 후 발사로 고정했다.
- `FlipFriendsLauncher`의 ContentIndex/PlayerContacts를 프레임에 보관한다. 동일 틱 재호출/지속 접촉은 진입을 중복 소비하지 않고, 이탈 후 재진입에 다시 적용한다.
- 여러 장치에 같은 틱 진입하면 변환기의 ID 순으로 모두 소비하고 마지막 속도 덮어쓰기가 남는다. 플레이어별 결과는 다른 플레이어 순서와 독립이다.
- 원/AABB 최근접점 또는 볼록 Polygon/AABB 분리축을 FP로 계산한다. 경계 접촉만으로는 발동하지 않는다. 방향 원점은 Collider 중심/offset과 별개인 원본 Transform이다.
- 도착·복귀 대기·복귀 틱은 기존 Quantum 정책대로 제외한다. 들린 플레이어와 운반자는 관계를 유지한 채 속도만 바뀐다. `LaunchCount`는 복귀 뒤 유지하는 관측값, LaunchFrame/Kind/속도는 복귀 시 초기화한다.

## 작성·콘텐츠

- `QuantumStageLauncher`의 Bounce/Spring 종류 + 같은 오브젝트의 활성 트리거 하나(Circle/Box/3~8점 볼록 Polygon) + SpriteRenderer. 회전0·배율1, 정적만 지원한다.
- Collider offset은 월드 판정으로 변환한다. 원 중심/반지름·Polygon 꼭짓점·방향 원점·종류·표시 범위를 콘텐츠 해시에 포함한다.
- 비트리거·다중 Collider·동적 Rigidbody·회전/배율·오목/일직선·Effector/Composite·Capsule·표시 없음·원본 태그만 있는 판정·같은 오브젝트의 피격 트랩 혼합은 거부한다. 기존 트랩은 계속 Box/Polygon만 허용한다.
- `QuantumLauncherSetup.Create`가 Unity API로 별도 `Official Launcher Test.prefab`/`official-launcher-test`를 만들고 작성기로 베이크·공식 목록에 등록한다. 기존 세 공식 시험 프리팹은 보존한다.

## 검증

- `QuantumLauncherChecks.RunContent`: 프리팹 재로드 해시·원형/Box·ID 순 독립·제품10 합의·종류/방향 원점/판정 오프셋 해시·변환 거부4종·작성기 거부10종 통과.
- `Run`: 실제 입력 기록/별도 러너 재생(1/30·1/120 교대) 확정120~2400 체크섬20개 일치(2400=`839121854484592999`). 스프링2회·바운스1회, 바운스 당시 상자 운반 유지, 전원 완료594, 매 확정 프레임 플레이어 지형 비관통, Unity 오류0.
- 직접8종: 바운스20·진입1회/재진입·로프/타이머/스무딩 유지, 스프링50 대각 정규화, 원 모서리/경계·원점 일치0, 운반자/들린 플레이어 관계 유지, 복귀대기/복귀틱/도착 제외, 복귀 초기화·횟수 유지, 다중 진입 마지막·모든 진입 소비, 상자 비대상.
- `RunMain`: 실제 Button 콜백으로 공식 시험 선택→오프라인→발사3회→전원 완료→Main→재진입→메뉴 종료, 러너/입력0 통과.
- `RunCloud`: 개발 Photon 동일PC 두 연결 asia·RTT103/115ms·확정240~2400 체크섬19개·발사 횟수/프레임/종류/운반 상태 일치, 완료650 일치, 러너/연결/입력 정리 True. 지연/손실 주입 없음.
- 제품10 회귀: 기존 제품 전체 오프라인(장치20·복귀20·상호작용12·이동8·정적6·로프8·Main), Tilemap10·official-test10·경사16·피격25 재생과 각 Main 통과. 피격 직접11종도 통과. 네 공식 콘텐츠 검사 통과. 기존 콘텐츠 자산 해시는 유지되고 프레임 필드 추가로 체크섬 값만 바뀐다.
- 지정6000.6.3f1 번들 dotnet 런타임/에디터 빌드 오류0. 최종 에디터 프로젝트 빌드는 경고180개(기존 코드/SDK), 새 묶음 파일 경고0. 공유 컴파일 서버 대기를 피하려고 `/p:UseSharedCompilation=false`로 실행했다. Unity 최종 컴파일 실패 없음·Main만 열림/미저장 유지·플레이 종료·러너0/입력0·자동 씬 로딩 Disabled·git diff 공백 검사 통과.
- 초기 시험 입력의 플레이어 역할은 변환기의 시작점 X순 정렬에 맞춰 수정했다. 게임플레이 규칙이나 단언을 완화하지 않았다.

## 미실행

- 실제 사람 조작·다른 PC·3~4인·게임패드·배포 빌드·Steam 실계정/출시 인증·지연/손실 주입.
- 기존 사용례/공식/경사/피격 Photon 재실행과 Edit Mode Test Runner. 새 스프링/바운스 Photon과 기존 오프라인 회귀를 구분한다.
