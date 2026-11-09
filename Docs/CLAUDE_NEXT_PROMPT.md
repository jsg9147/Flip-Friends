# Claude Code 작업 요청 — Quantum 최대 4인·입력·표현 연결 (전체 교체 순서 5)

`C:\Unity Project\Flip-Friends`에서 Quantum 전환의 다음 묶음을 구현해줘. 기준은 제품14 / `ff-product-14`(커밋 `bedda32`, 대포/투사체 완료)이다. 현재 제품 경로의 2인 전제를 최대 4인으로 넓히고, 키 재설정·게임패드·장치 배정과 애니메이션·효과음·카메라를 Quantum 확정/예측 프레임에 연결해줘. 조사나 계획에서 멈추지 말고 구현, 검증, 문서 갱신까지 진행해줘.

## 착수

1. `CLAUDE.md`, `PROJECT_DOCUMENTATION.md`, `Docs/QUANTUM_MIGRATION.md`, `Docs/NEXT_SESSION.md`를 읽어. 입력·UI는 `.claude/rules/ui.md`와 `Docs/UI_REBUILD.md`, 애니메이션은 `Docs/PLAYER_ANIMATION.md`, 운반·밟기 순서는 `Docs/PLAYER_MOVEMENT_SYNC.md`를 추가로 읽어. Notes는 원본 동작이나 측정 근거가 필요할 때만 확인해.
2. Git 상태와 열린 Unity 씬의 미저장 여부를 확인하고 기존 작업을 보존해(`Main.unity`의 기존 미저장 변경을 저장하거나 버리지 마). Unity 6000.6.8f1과 Quantum 3.0.14 Stable 2241을 확인하고, 제품14 규칙 서명과 공식 시험 8종 해시를 기록해.
3. 2인 전제를 전부 찾아 목록으로 만들어. 최소한 다음을 확인해.
   - `QuantumGameSession`의 `MaxPlayers = 2`·`PlayerCount = 2`, `QuantumMapAgreement`의 참가자 수 검사, `QuantumGameView`의 연결 유지 조건
   - 공식 변환기와 `QuantumMapConverter`의 "시작점 정확히 2개" 규칙, MapData 2.1 시작점 규칙
   - 시뮬레이션의 `Frame.PlayerCount` 사용(이미 사용 중지 경고가 있다), PlayerRef 비트 마스크, 운반 탐색·밟기·같은 틱 PlayerRef 순서, 완료 판정
   - `QuantumPlayerInput`의 1P/2P 고정 키, 온라인 로컬 슬롯0 처리, `QuantumPrepareScreen`의 2인 안내
4. 원본 Mirror 경로의 인원 규칙을 대조해(최소 인원, `DynamicObj` 인원 오프셋, 도착 판정, 로비 최대 인원). 기존 키 재설정(`KeyRebindingManager`·`KeyBindItem`·`PlayerPrefs` 저장 형식)과 원본 게임패드 동작, 원본 `PlayerAnimationController`·`SoundManager`·카메라 추적 동작을 확인해.

## 사용자에게 먼저 물을 것

원본에서 정할 수 없는 다음 결정은 구현 전에 질문해. 그 밖의 결함 수정은 독립적으로 진행해.
- 한 PC의 로컬 플레이어 상한과 장치 배정 방식(키보드 분할 최대 2명 + 게임패드, 참가 확인 화면 여부)
- 인원이 시작점보다 적거나 많을 때의 처리(빈 시작점 허용, 시작점 4개 필수 여부, 2~3인 시작 허용)
- 공식 시험 맵 8종과 MapData 맵에 시작점을 더할지, 기존 2인 맵을 2인 전용으로 둘지(기존 해시를 바꾸게 되는지)

## 구현

- 인원은 RuntimeConfig/세션 설정 값으로 두고 시뮬레이션은 실제 참가 PlayerRef만 순회해. 같은 틱 처리 순서(PlayerRef 순)와 기존 1~14 규칙은 바꾸지 마.
- 2인 콘텐츠의 해시와 동작은 유지해. 바꿔야 하면 이유를 기록하고, 새 형식 번호는 새 데이터가 있을 때만 쓰는 기존 방식을 따라.
- 입력: 기존 키 재설정 저장값을 Quantum 입력에 연결하고 New Input System 게임패드를 장치 배정과 함께 지원해. UI에서 입력 액션을 따로 만들지 말라는 `ui.md` 규칙을 지켜. 일회성 입력(들기·리셋)은 현재처럼 틱 버튼으로 둬.
- 표현: 애니메이션·효과음·카메라는 Unity 쪽에서 예측/확정 프레임 상태만 읽어. 재시뮬레이션으로 효과음이 중복 재생되지 않게 사건을 프레임 상태(카운터·프레임 번호)에서 판정해. 카메라는 2~4인 범위를 담게 해.
- 판정 권위는 Quantum에 두고 Mirror 이동·Animator 경로를 다시 생성하지 마. 제품 자동 씬 로딩 Disabled를 유지해.
- 제품 합의 버전을 제품15 / `ff-product-15`로 올려.

## 검증과 완료

- 저장소 지정 dotnet 런타임·에디터 빌드와 Unity 컴파일을 확인해. 새 Play 세션에서 `QuantumProductChecks.RunOffline`을 다른 Main 검사보다 먼저 실행해.
- 3인·4인 오프라인 세션에서 실제로 공급한 입력을 기록하고 별도 러너에서 재생해 확정 체크섬, 운반·밟기·피격·완료 사건의 상대 틱을 비교해. 2인 기존 회귀(Tilemap·공식·경사·피격·스프링/바운스·이동 발판·스위치·컨베이어·대포 재생과 Main 왕복)를 모두 다시 실행하고 공식 해시 8종을 확인해.
- 키 재설정 값 적용·저장 형식 호환, 게임패드 연결/해제·장치 재배정, 효과음 중복 없음, 카메라 범위를 검사로 확인해. 실제 게임패드 조작은 실행 여부를 따로 적어.
- Main에서 인원 선택 → 플레이 → 전원 완료 → 복귀 → 재진입 → 메뉴 종료를 확인하고 러너·입력 구독·장치 구독 정리를 확인해. 개발 Photon은 동일 PC 연결 수를 늘려 확정 결과를 비교하되, 가능한 연결 수와 미실행 범위를 구분해.
- 실제로 실행하지 않은 검사(다른 PC·실제 3~4인·Steam 다계정/출시 인증·지연/손실 주입·배포 빌드)는 통과로 표시하지 마.
- 관련 기능 문서의 현재 상태·검증·다음 작업을 갱신하고, 원본과 달라진 규칙과 이유를 Notes에 남겨. 구현 범위, 검증 결과, 남은 제약을 한국어로 보고해. 완료 후 이 프롬프트 파일은 정책에 따라 지워.
