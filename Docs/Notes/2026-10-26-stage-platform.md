# 단계 4 발판 조사·검증 근거 (2026-10-26)

- 기준선: `Temp/StagePhase4`에 수정 전 21개 프리팹과 참조 검사 12개를 보관했다. Temp는 커밋 대상이 아니다.
- 기존 서버는 FixedUpdate 외부에서 승객 Controller2D.Move를 호출했고, 원격 소유자의 예측·재시뮬레이션에 이 이동량이 없었다. ResetPlatformState도 위치/경로 진행률/복귀 코루틴을 초기화하지 않았다.
- PlatformMotion은 기존 speed의 구간 진행률/초, easing, 경유점 배열, cyclic/isRepeat 의미를 보존한다. 구간 소요 틱은 기존 float 누적 방식으로 한 번 계산해 캐시한다. 속도 .3/2/4/8은 각 500틱을 기존 적분과 대조했다. 원위치로의 반복/자동 복귀는 승객 순간이동으로 적용하지 않는다.
- 이동 시작 시각/초기 위치/이동 활성/리셋 세대는 SyncVar다. 입력의 platformTime은 서버에서 유한값과 현재 시각 주변 범위를 검사한다. StatePayload는 탑승 부품 참조·세대·마지막 시각을 전달한다.
- MovementHandler.Simulate에서 발판을 입력 시각의 위치/충돌로 잠시 맞추고 운반·일반 이동을 실행한 뒤 원래 위치로 복원한다. 운반 중 지지 발판만 충돌에서 제외해 상승 발판의 중복 밀기를 막고 벽 판정은 유지한다.
- 리셋은 최초 위치/시계/상태/감지 지연/판정/알파를 복원한다. 코루틴과 Tween 대신 시각 평가를 사용하며, 세대 변경으로 이전 탑승 이동량을 차단한다.

## 프리팹 판단

- Stage 15·16·17 두 발판은 자체 SpriteRenderer 하나씩이 있어 총 4개를 연결했다.
- Stage 20 Conveyor belt (9)는 벨트 렌더러 5개, Stage 21 Conveyor belt (6)는 벨트와 자식 스위치 렌더러 6개다. 두 곳 모두 낙하/소멸 연출이 꺼져 있어 특정 자식을 임의 연결하지 않았다. 옵션을 켜면 검사기가 필수 렌더러 누락을 오류로 보고한다.
- Stage 12 LagerBar_0 두 개는 Trap 태그·Trap 레이어·트리거 콜라이더다. Stage 20 Move Trap (1)/(3)/(4)도 Trap 태그·Enemy 레이어·트리거다. 기존 passengerMask=0을 유지하고 isPassengerTransportEnabled=false로 의도를 명시했다.
- Stage 21 벨트 감지 마스크는 다른 이동 벨트들과 같은 Player로 복구했다. Stage 20 벨트의 speed=0은 의도 확인 없이 변경하지 않았다.
- 팔레트의 모든 프리팹에 PlatformController가 없음을 에디터 API로 확인했다. 저장/전송 스키마 변경은 없다.

## 검증 한계

- 저장 후 재로드, 전체 참조 21개 0건, 변경 6개 스테이지의 Transform/Collider/Renderer/Animator와 이동/대기/연출 설정 불변을 확인했다.
- StagePlatformChecks는 별도 편집 씬의 실제 Raycast/충돌, 두 플레이어 운반, 수평/상승/하강, 점프 분리, 벽 차단, 재시뮬레이션, 자동 복귀/반복 리셋, 초기/변경 SyncVar와 탑승 참조/시각/세대 직렬화를 검사했다. 이전 단계 검사 4종도 통과했다.
- 실제 소켓, PlayMode 키 조작, 지연 조건의 화면 떨림, 맵 에디터 테스트 플레이는 미실행이다. 느슨한 상자용 탑승 운반은 추가하지 않았으며 기본 마스크는 Player만 포함한다.
