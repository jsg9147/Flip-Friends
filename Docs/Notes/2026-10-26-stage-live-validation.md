# 스테이지 리팩토링 후 실제 런타임 검증

- 기준: 2026-10-26 KST, Unity 6000.6.3f1, 현재 작업 디렉터리의 미커밋 소스 포함.
- 기존 코드/프리팹 변경을 유지했다. 이번 게임 코드 수정은 `SlimeRoomManager`의 서버 초기화/이탈 수명 처리다. 프리팹·난이도·저장 스키마·팔레트는 수정하지 않았다.
- 결과 종류: **소켓 런타임**, **에디터 서버 전용**, **API로 배치/상태 설정**, **격리 검사**를 구분한다. 전체 스테이지의 실제 키 조작 클리어는 실행하지 않았다.

## 실행 환경과 증거

- `LocalNetworkTest`를 읽고 동일 PC의 별도 Windows 프로세스끼리 KCP 7777로 접속했다. `-localhost`/`-localclient`, 먼저 `-latency 0`, 다음 `-latency 60` 순서다. 후자는 각 송신에 60ms를 설정한 것으로 실제 RTT를 측정한 값은 아니다.
- 모든 실행 파일은 이번 작업에서 BuildPipeline으로 만든 개발 빌드다. 최초 빌드 → 관측 도구 포함 빌드 → 첫 수정 포함 FinalBuild → 이탈 수정 포함 VerifiedBuild 순서다. 마지막 VerifiedBuild는 오류 0, 경고 2, 278.32MB, 19.84초로 성공했다.
- 최종 VerifiedBuild의 Assembly-CSharp.dll SHA-256: `C32293075F80A82740BF7B47274437405F2856C721FD60B2E71E2F7554C2EC23`.
- 원시 증거는 커밋하지 않는 `Temp/StagePlayValidation/`의 `*.log`, `*.jsonl`, `*.latest`, `verified-stages-result.txt`, `verified-counts-result.txt`, `final-checks.txt`, `*-dotnet-final.log`에 있다. Temp는 이후 Unity가 정리할 수 있으므로 재현 방법과 요약을 여기에 남긴다.
- `LocalStagePlayProbe`는 개발/에디터 컴파일과 명시적 `-stageprobe <절대경로 접두사>`가 모두 있어야 생성된다. 약 0.1초마다 씬, 연결, netId, Identity 수, 플레이어 완료/위치, DynamicObj, 탄환 속도/위치/물리 활성, 대포 예약을 수집한다. 평상시 실행에는 관측 객체가 없다.
- `<접두사>.command`에 증가하는 `sequence`, `action`, 선택적 `stage`/`count` JSON을 쓴다. 지원 명령은 `stage`, `finish`, `room`, `dynamicRepeat`, `cannonsOff`, `cannonsOn`, `disconnect`, `quit`다. `finish`는 netId 순서의 count명 완료 상태를 설정하고 서버 FinishCheck를 호출하므로 입력/도착 판정 검증이 아니다.

```text
Slime Climb.exe -localhost -latency 60 -batchmode -nographics -stageprobe "<절대경로>/host"
# 호스트 GameRoom 준비 및 연결 1개 확인 후 별도 프로세스 실행
Slime Climb.exe -localclient -latency 60 -batchmode -nographics -stageprobe "<절대경로>/client"
{"sequence":1,"action":"stage","stage":"stage-20"}
{"sequence":2,"action":"finish","count":1}
{"sequence":3,"action":"finish","count":2}
{"sequence":4,"action":"quit"}
```

## 소켓 런타임 결과

- 무지연 2인: Stage 1~21 양쪽 생성, 각 스테이지 루트 Identity 하나, 플레이어 2개, 미도착 상태 1.2초 대기, 1명 API 완료 후 0.5초 대기, 전원 API 완료 후 실제 GameRoom 복귀, 양쪽 이전 스테이지/CannonBall 제거를 통과했다.
- 60ms 2인: 같은 21개 시나리오를 통과했다. 두 번째 수정 후 VerifiedBuild로 21개 전체를 다시 실행해 모두 통과했다.
- 이 검사에서 복귀 요청 자체의 호출 횟수는 계측하지 않았다. 연속 기록에서 씬 복귀/안정 상태를 관측했으며, 호출 1회 자체는 기존 격리 GameManager 검사에서 보장한다.
- Stage 20: 실제 활성 대포 22개, 서버 탄환 이동과 원격 수신을 관측했다. 대포 비활성화 시 예약 0, 이후 양쪽 탄환 0, 재활성화 후 예약/발사 재개를 확인했다.
- Stage 21: 지연 세션에서 같은 중지/소멸/재개를 확인했다. 호스트만 API 완료시킨 뒤 미완료 원격 클라이언트를 끊자 연결 1개로 실제 GameRoom 복귀 및 스테이지/탄환 0을 확인했다.
- 무지연 장시간 기록에서 서버 탄환 netId 811개, 원격 탄환 표본 14,386개를 관측했고 원격 `Rigidbody2D.simulated=true` 표본은 0개였다. 연속 위치로 계산한 예시 속도는 설정 8에 대해 7.94~8.08이었다. 0.1초 관측과 FixedUpdate 경계 오차를 포함하며 엄밀한 프레임별 튐 판정은 아니다.
- 초기 지연 21개 기록에서도 원격 탄환 표본 1,261개 모두 자체 물리가 꺼져 있었다. 머즐 초기/종료 순간의 보간 품질은 화면을 따라가는 별도 조작 검증이 필요하다.
- Stage 1/2/6 DynamicObj는 기본 위치 (0,0), 설정 오프셋 (0,1)이다. 실제 2인에서 (0,1)이 양쪽 일치하고 서버 API를 연속 2회 적용해도 누적되지 않았다.
- Stage 3/4/5 DynamicObj는 저장 프리팹의 GameObject 자체가 비활성이다. 기존 상태를 유지했으며 이를 활성 인원 배치 통과로 표시하지 않는다. 비활성 원본 위치는 각각 (3,0), (0,0), (20,0)이다.
- 3/4인은 Steam 계정 3~4개가 아니라 호스트 1개와 별도 로컬 KCP 클라이언트 프로세스들이다. 활성 Stage 1/2/6에서 3인 오프셋 2, 4인 오프셋 3, 원격 일치·반복 적용·API 완료 왕복을 확인했다.
- 최종 VerifiedBuild에서는 4→3→1인 전환으로 Stage 1/2/6을 각 인원에서 모두 반복해 9회 통과했다. 1인 오프셋은 0이었다. 2인 오프셋 1은 별도의 최종 21개 회귀에서 확인했다. 3/4인 전체 스테이지 순회나 동시 실조작은 실행하지 않았다.

## 재현한 실패와 수정

### 서버 전용 첫 접속 실패

1. 에디터 Main에서 자동 LocalNetworkTest를 잠시 끄고 Play.
2. 기존 `UseLocalTransport` 경로로 KCP를 붙인 뒤 `NetworkManager.StartServer()` 실행.
3. GameRoom에 준비된 서버로 현재 소스의 원격 클라이언트 접속.
4. `OnStartHost`만 초기화하던 `lobbyPlayerList.Add`에서 NullReferenceException. KCP가 접속을 끊었다.

- 수정: 목록 생성을 `OnStartServer`로 이동했다. 필수 상태를 null 방어로 생략하지 않는다.
- 재검증: `NetworkServer.active=true`, `NetworkClient.active=false`, 연결 1개와 목록 초기화를 확인했다. Stage 21 실제 탄환 속도 10/위치 변화, 원격 물리 off, API 완료 후 실제 GameRoom 복귀를 확인했다.
- 이는 **에디터의 서버 전용 실행**이다. UNITY_SERVER 전용 배포 빌드/Steam 전용 서버 통과를 뜻하지 않는다.

### 대기실 이탈 직후 재시작 시 유령 플레이어

1. 지연 60ms 호스트+클라이언트 3개로 Stage 1/2/6 완료 왕복.
2. 4→3인 후 같은 왕복, 이어 대기실에서 원격 2개를 종료하고 빠르게 1인 재시작.
3. 끊긴 연결의 `pendingPlayers` 항목이 다음 씬에서 처리됐다. `SceneLoadedForPlayer` → `ReplacePlayerForConnection` → `SendChangeOwnerMessage`에서 NullReferenceException이 발생하고 연결 1개인데 플레이어 2개가 생성됐다.

- 원인: Mirror 기본 이탈 경로가 roomSlots는 제거하지만 pendingPlayers는 제거하지 않는다. 현재 게임의 `OnRoomServerDisconnect`도 이를 정리하지 않았다.
- 수정: 프로젝트 이탈 콜백에서 해당 연결의 pendingPlayers 항목을 제거했다. 벤더 코드는 고치지 않았고 신규 입장/재입장 정책도 바꾸지 않았다.
- 재검증: 수정 빌드에서 같은 4→3→1인 전환/재시작을 재실행해 9회 완료 왕복 통과. 마지막은 연결 1개·플레이어 1개였고 유령 플레이어/예외가 없었다. 최종 빌드 21개 지연 회귀와 해당 세션 로그에도 예외가 없었다.

### 테스트 환경에서 구분한 실패

- 호스트/클라이언트를 동시에 시작하면 클라이언트가 GameRoom보다 먼저 도착해 기존 정책으로 거절됐다. Headless Mirror는 플레이어 0 상태의 이탈 시 서버를 멈출 수도 있다. 호스트 GameRoom 준비를 확인한 다음 클라이언트를 시작해 해결했으며 입장 제한을 완화하지 않았다.
- 관측 `.latest` 파일을 갱신 중 읽어 빈 값을 비교한 스크립트 실패 1회가 있었다. 연속 JSONL의 오프셋은 전후 동일했다. 읽기 재시도로 하네스를 고쳤으며 DynamicObj 게임 코드는 바꾸지 않았다.
- 처음 에디터 서버 전용 실행을 시도할 때 기존 호스트가 7777을 사용해 SocketException이 났다. 기존 검증 호스트를 종료하고 다시 실행했다. 게임 결함 통계에 포함하지 않는다.

## 맵 에디터 실제 런타임/API 회귀

- Main → MapEditor를 실제 씬 전환했다. 초기 편집 데이터는 빈 새 맵이었다.
- API로 임시 맵에 지형, 시작점, 도착점, `interactive.box`, `obstacle.cannon` 5개를 배치하고 RebuildFromMapData/TryStartTestPlay를 실행했다. 지형 (0,-2), scale (20,1,1), 시작 (-5,0), 도착 (6,0), Box (0,1), Cannon (-6,3)이다.
- 다른 세션과 충돌하지 않도록 에디터의 임시 KCP 포트는 7788을 썼다. 테스트 플레이의 실제 호스트/씬 생성 흐름을 사용했다.
- 탄환 x=-0.2000035, y=3, 실제 Rigidbody 속도 10을 관측했다. 대포를 끄고 기다려 탄환 0을 확인했다.
- 상자는 실제 물리/게임 이동 후 (0,-1.05000007)에 안정됐다. 경계 minY=-12.5 아래로 Release한 뒤 기존 배치 위치를 거쳐 같은 지면으로 복귀했다.
- PickUpObj API를 통해 운반 상태를 만든 뒤 ServerReset((0,1))을 호출했다. 운반 전 양쪽 참조 true → 직후 heldObject/IsCarried false, 이후 지면 y=-1.05로 안정됐다. 이전 동기화 위치로 되돌아가는 현상은 관측하지 않았다. 원격 운반 조작 검증은 아니다.
- API 전원 완료 후 MapEditor 복귀, 테스트 플레이 재시작 후 `GameManager.ExitGame()` 메뉴 종료 경로로 복귀를 각각 확인했다. 두 번 모두 같은 mapId/전체 JSON/오브젝트 5개를 유지했고 서버/클라이언트/탄환이 정리됐다. 마지막 MapEditor → Main도 확인했다.
- UI 드래그 배치, 저장/불러오기, 실제 입력으로 맵을 끝까지 클리어하는 검증은 수행하지 않았다.

## 격리/컴파일 검증과 남은 항목

- 두 번째 코드 수정 후 검사 메뉴 7종 모두 PASS, 저장된 21개 스테이지 참조 지적 0개를 다시 확인했다. 런타임/에디터 dotnet 빌드, Unity 컴파일/Mirror Weaver, Windows 개발 빌드 오류 0개다.
- 경고는 남아 있다. 기존 미할당 직렬화 필드 경고와 관측 도구의 DEVELOPMENT_BUILD 조건에 대한 Unity UAC0009 경고를 포함하며 오류로 숨기지 않는다.
- 전체 작업 트리 `git diff --check`에는 기존 CannonBall 프리팹의 빈 m_Name 후행 공백이 남는다. 이번 작업은 그 프리팹을 편집하지 않았다.
- **미검증:** 두 사람이 실제 입력으로 수평/상승/하강 발판을 함께 타고 점프로 분리, 벽/천장·운반·이동 중 리셋/자동 복귀/알파·충돌 복원, 보정 떨림·관통, 머리로 일반 콜라이더 버튼 누르기.
- **미검증:** 원격의 상자 재소환/구간 리셋/리셋 키/추락 목적지·onlyBoxReset·운반 표시/충돌 무시, 쿨다운 표시·다중 실제 접촉·체크포인트 역행. 기존 격리 검사 통과와 구분한다.
- **미검증:** 문 다중 접촉/비활성화, MovingTrap 왕복/정지/회전과 랜덤 회전의 실제 양쪽 시간 경과 정밀 비교, Stage 20/21 자동 토글 간격 계측. 기존 설정은 변경하지 않았다.
- **미검증:** 실제 조작 전체 21개 클리어, Steam 2~4인 로비/전송/입장 정책, 전용 서버 배포 빌드. 로컬 KCP 프로세스 수를 Steam 인원 검증으로 대신하지 않는다.
- 실제 입력 환경은 PC 한 대·키보드 한 개이며 Windows 입력 도구는 단발 키 입력을 제공한다. 원격 화면과 Space 입력을 시도했지만 성공한 점프/조작 클리어로 입증하지 못했다. 두 플레이어의 연속 이동·점프·운반 동시 조작은 이번 API 시나리오로 대체하지 않았으며 위 항목은 수동 검증으로 남긴다. Steam 계정 여러 개나 컨트롤러 여러 개도 준비하지 않았다.

## 정리와 보존

- 이번에 띄운 모든 Slime Climb 프로세스에 quit를 보내 종료했고 마지막 프로세스 목록에 게임이 없음을 확인했다. 에디터는 Play 아님, 서버/클라이언트 false, 임시 KCP 컴포넌트 0이다.
- 시작 시 Main은 dirty=true, LocalNetworkTest EditorPrefs는 Host(1)였다. 처음 미저장 Main을 `Temp/StagePlayValidation/Main-unsaved.unity`에 saveAsCopy로 보관했다. MCP 빌드 도구가 씬을 자동 저장했지만, 최종 Main과 최초 백업 SHA-256은 모두 `AD15ABFE197EF96FE62F7D1A6925E8FC6FE644D90EDC2747707076EEF541EF08`로 같았다. 마지막에 Main dirty=true와 Host(1)를 복원했다.
- 기존 변경을 reset/checkout/commit하지 않았다. 이번 작업의 소스 변경은 SlimeRoomManager 및 명시적 개발용 LocalStagePlayProbe와 Unity가 생성한 meta다. 기능 문서 5개와 이 실행 기록을 갱신했다.
