# Main 메뉴 UI 재구축 현재 상태

- 상태: Active
- 최종 갱신: 2026-08-04
- 범위: `Main.unity` 화면 전환, 로비 진입 UI, 설정과 키 바인딩

## 현재 상태

- 기존 Steam, Mirror, 입력, 사운드와 게임플레이는 유지하고 메뉴/UI 계층만 점진적으로 교체한다.
- `Assets/01_Scripts/UI Scripts/Rebuild`에 공통 화면·내비게이션과 Main, ModeSelect, HostRoom, PublicLobby, PrivateJoin 전용 화면이 있다.
- `Main.unity`의 `MainUIManager` 오브젝트에 새 내비게이터가 연결되어 있다.
- Main UI와 GameMode UI는 새 화면 흐름에 연결됐다.
- Start는 `mode-select`, Setting은 `settings`, Host/Public/Private 버튼은 각 화면 ID로 이동한다.
- Host 설정은 기존 `SteamRoomManager.HostLobby`에 연결되고, 공개 로비는 loading·empty·error·joining 상태를 구분한다.
- 비공개 참가는 `TMP_InputField` 기반 코드 입력과 기존 참가 로직을 연결한다.
- 기존 UI를 한 번에 제거하지 않고 새 흐름이 확인된 화면부터 교체한다.
- 공개 로비 항목은 선택 맵 종류·이름과 최소 인원을 입장 전에 표시한다.
- `PublicLobbyScreen`은 맵 종류와 현재 파티 인원 기준 필터를 주입할 수 있는 경계를 제공한다.
- HostRoom은 공개/비공개 설정, 선택 맵, 최대 인원, 맵 검증과 현재 리비전 완료 상태를 함께 반영한다.
- 공개방에서 현재 리비전 완료를 확인하지 못하면 생성 버튼을 비활성화하고 비공개 방에서 먼저 완료해야 한다는 이유를 표시한다. 비공개 설정으로 자동 전환하지 않으며 완료 저장 이벤트에 즉시 다시 판정한다.

## 핵심 흐름

```text
ScreenNavigationButton
  → ScreenNavigator
  → UIScreen 파생 화면
  → 기존 SteamRoomManager 및 설정 서비스
```

## 검증

- 스크립트와 씬 연결의 컴파일 검증은 완료됐다.
- 전체 마우스·키보드·게임패드 이동과 런타임 왕복은 수동 검증이 남아 있다.
- 일부 버튼에는 기존 `MainUIManager` listener와 새 listener가 함께 있을 가능성이 있다.
- Main 씬의 HostRoom 버튼·선택 맵·검증 메시지 참조와 `MapSelectionList`의 HostRoom 오버라이드를 YAML로 확인했다.
- 8단계 Unity 배치 컴파일과 전체 Edit Mode 테스트 64개가 오류 없이 통과했다.

## 남은 작업

- 새 경로로 대체된 버튼의 중복 legacy listener 감사
- Host, 공개 로비, 비공개 참가 화면의 전체 런타임 왕복 및 실패 상태 수동 검증
- Setting과 Key Rebinding 화면 구조 재구축
- 새 흐름 확인 후에만 구형 메뉴 스크립트 제거

## 다음 작업

`Main.unity`의 Main UI와 GameMode UI 버튼 listener를 감사하고, 새 경로와 중복되는 legacy listener만 제거한다.
