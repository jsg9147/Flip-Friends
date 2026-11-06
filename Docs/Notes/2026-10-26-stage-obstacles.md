# 단계 5 장애물·탄환·배치 조사 (2026-10-26)

- 수정 전 스테이지 21개와 CannonBall 프리팹을 `Temp/StagePhase5`에 보관했다. 전체 참조 검사 전후 모두 0개다.
- 대포 69개는 모두 `Assets/03_Prefabs/Obstacle/CannonBall.prefab`을 사용한다. 원래 속도는 ClientRpc에서만 설정돼 전용 서버에서 0이었고, NetworkTransform도 ClientToServer였다. 초기화 후 Spawn + 서버 권한 위치 동기화로 변경했다.
- 발사 시 원래의 world shootDirection·정규화·1유닛 포구 오프셋·속도·최대 거리를 유지한다. 초기 지연과 주기는 서버 시계로 예약하며 지연 후 누락 발사를 한 프레임에 몰아서 실행하지 않는다.
- 수명은 탄환 컴포넌트가 서버에서 0.1초 간격으로 검사한다. 기존과 동일하게 발사점과 현재 위치의 거리가 최대 거리보다 커야 제거한다. 대포 비활성화나 파괴로 코루틴이 중단돼 탄환이 남는 의존성을 없앴다.
- 회전 장애물 37개는 모두 자기 Transform을 서버 권한으로 동기화한다. 시간 기반 회전으로 바꾸지 않고 랜덤 선택만 서버로 이동했다. 선택된 속도/방향을 SyncVar로 전달하고 기본 설정 값은 덮어쓰지 않는다.
- Stage 1~6의 DynamicObj 6개는 모두 x=0/y=1의 인원 오프셋이다. 원래 NetworkTransform.target은 null이었으며 런타임 Configure가 자기 자신을 선택하지만, 이번에는 명시적으로 저장했다. 1~4인 위치와 재적용 멱등성을 검사했다. DynamicObj의 자식 MovingTrap은 0개다.
- MovingTrap 104개의 양수 시간 경로·속도·회전 식은 유지했다. 시작 전 음수 시간은 0으로 제한하고 반복 OnStartServer에서 시작 시각을 덮어쓰지 않는다.
- GameManager의 서버 완료 판정이 Command를 호출해 전용 서버에서 복귀할 수 없는 경로를 발견했다. 서버가 기존 ReturnRoomScene을 직접 호출하고 중복 복귀를 차단한다. 외부 Command는 서버 완료 상태를 다시 확인한다.

## 검증과 한계

- StageObstacleChecks: 트랩 왕복/정지/위상, 회전 랜덤 선택/직렬화, 인원 배치, 발사 예약, Spawn 전 Rigidbody2D 속도, 실제 Mirror Spawn/UnSpawn, 범위 경계, 원격 물리 비활성화를 통과했다. 서버 플래그/네트워크 ID 카운터/Random 상태를 검사 후 복원한다.
- StageLifecycleChecks: 21개 프리팹 격리 로드/경계/Finish/정리, 카탈로그·씬 연결, 실제 활성 Main 매니저의 21개 스테이지/팔레트 6종/탄환 spawn 등록, 클리어 요청 1회를 통과했다. 씬 참조는 PreviewScene으로 읽는다.
- 완료 판정 하네스는 UNITY_EDITOR 조건부 런타임 어셈블리 클래스다. Editor 어셈블리의 MonoBehaviour는 AddComponent로 생성되지 않아 분리했으며 플레이어 빌드에는 포함하지 않는다. 복귀 메서드를 카운트만 하므로 실제 씬 전환 검사가 아니다.
- 독립 Physics2D 씬을 편집 모드에서 생성하려 했으나 SceneManager.CreateScene이 PlayMode 전용이라 실행할 수 없었다. 메인 씬 물리를 임의 진행하지 않고 실제 탄환 비행 검증을 수동 항목으로 남겼다.
- Stage 1~6의 Transform/Collider/Renderer/Animator 블록은 기준선과 동일하다. 대포의 발사 간격/지연/속도/방향/거리와 이동·회전 설정도 변경하지 않았다.
- 기존 단계 1~4 검사 5종, Unity/Mirror Weaver와 런타임·에디터 빌드를 확인한다. 실제 호스트/원격 소켓, 2~4인 Steam, 전체 클리어·씬 전환, 팔레트 테스트 플레이의 탄환 비행은 미검증이다.
