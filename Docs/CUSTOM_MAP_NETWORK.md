# 커스텀 맵 네트워크 현재 상태

- 상태: Active
- 문서 정리: 2026-11-15 Mirror 제거 반영 (실행 검증 날짜는 검증 절 참조)
- 범위: Quantum 준비 화면의 저장 맵 선택, Steam Quantum 로비·Photon 친구 방의 맵 합의와 전송

## 현재 상태

- Main의 Quantum 준비 화면에서 저장 맵을 순환 선택한다. MapData 2.1과 변환기 지원 범위를 검증하고 미지원 맵은 위치·사유를 표시한다. 2.0 파일은 맵 에디터에서 먼저 변환해야 하며 준비 화면은 파일을 쓰지 않는다. 선택 파일을 시작 직전에 다시 검증하며, 저장 맵의 오프라인 인원은 1~4명이다.

- 온라인 준비 화면은 Steam Quantum 로비를 만든 뒤 공식·저장 맵을 고르는 흐름을 사용한다. 코드 입력은 기존 지역 포함 코드로 숨김 Photon 친구 방에 입장하는 호환 경로도 제공한다. 출시 앱 미설정 또는 개발 앱 지정 시 접속을 거부한다.


- Steam Quantum 로비의 방장이 저장 맵을 고르면 검증한 스냅샷과 해시를 선택에 묶는다. 기존 Photon 보유 확인과 필요 전송을 재사용하며 수신 결과를 시작에 확정한 해시와 대조한다.
- Quantum 방장은 MapId·스키마 버전·SHA-256·크기를 전달한다. 일치하는 로컬 맵을 가진 참가자는 전송을 생략하고, 나머지 참가자만 8 KiB 청크를 받는다. 전송은 최대 512 KiB와 30초로 제한한다. 참가자 이탈이나 취소 시 시작을 중단한다.
- 참가자 전원이 정규 UTF-8 MapData와 FP 변환 결과·변환/팔레트/제품 규칙을 확인한다. 맵 합의가 끝나면 입장을 닫고 러너를 시작한다. 수신 버퍼와 동적 맵은 세션에만 유지한다. 받은 맵은 자동 저장하지 않는다.
- 맵 에디터 HUD의 테스트 플레이는 편집본을 QuantumPlay 오프라인 세션으로 시작한다. [맵 에디터 상태](MAP_EDITOR.md#현재-상태).
- 2026-11-15 Mirror GameRoom 경로(서버 세션 저장소·클라이언트 캐시·`MapDataNetworkSync`·GamePlay 런타임 생성)를 제거했다. 진행 중 입장 제한은 Quantum 로비가 시작 시 Steam 입장과 Photon 방을 닫는 방식으로 맡는다.

## 핵심 흐름

```text
QuantumPrepareScreen(저장 맵 선택)
  → QuantumGameFlow → SavedMapCatalog(검증·크기 한계 MapSessionSnapshot.MaximumContentBytes)
  → QuantumLobby / QuantumSavedMapTransfer(보유 확인·청크 전송·MapSessionContentValidator)
  → QuantumGameSession(콘텐츠 합의 → 러너 시작)
```

주요 위치:

- `Assets/01_Scripts/NetworkScripts/QuantumGameFlow.cs`, `QuantumLobby.cs`, `QuantumSavedMapTransfer.cs`, `SteamLobbyService.cs`
- `Assets/01_Scripts/MapEditorScripts/SavedMapCatalog.cs`, `MapEditorScripts/Network/MapSessionContent.cs`
- `Assets/01_Scripts/MapEditorScripts/Network/Core/` (`MapTransferCore`, `MapAvailabilityCheck`, Unity·네트워크 비의존)
- `Assets/Tests/EditMode/MapTransferCoreTests.cs`, `MapAvailabilityCheckTests.cs`

## 검증

- `QuantumLobbyChecks.Run`은 Steam을 모의하고 동일 PC의 실제 개발 Photon 두 연결을 사용했다. 저장 맵 미보유 게스트가 맵을 받아 양쪽 확정 프레임에 도달했고, 메뉴 종료 후 같은 로비 복귀·동일 맵 재시작 및 세션 자원 정리를 확인했다. 같은 Play에서 기존 `RunSavedMapSharing`도 재실행해 보유 맵 전송 생략·필요 청크 전송·이탈·해시 불일치·시간 초과 후 재시도와 자동 저장 없음이 통과했다.

- 2026-11-10 Unity 6000.6.8f1 재컴파일 오류 0건. 새 Play 세션에서 첫 Main 검사인 `QuantumProductChecks.RunOffline`을 통과한 뒤 `RunSavedMapSharing`을 실행했다.
- 저장 맵 선택·오프라인 시작·중복 클릭, 미지원 및 잘못된 버전 거부, 2.0 파일의 내용 보존, 실패 뒤 재선택을 확인했다. 같은 PC의 개발 Photon 두 연결에서 보유 맵 청크 0개, 미보유 맵 다중 청크, 합의 뒤 방 닫기와 양쪽 확정 프레임을 확인했다.
- 대기 및 전송 중 취소, 참가자 이탈, 수신 해시 불일치, 시간 초과 뒤 재시도를 확인했다. 러너·Photon 연결·입력 구독·동적 맵 정리, 저장 파일 바이트 유지와 자동 수신 파일 0개를 확인했다. 출시 경로는 개발 앱을 거부했다.
- Steam 실계정·오버레이 초대·출시 Photon 인증, 다른 PC, 실제 다인 조작, 배포 빌드은 미실행이다.
- 제품5 콘텐츠 합의·불일치 거부·개발 Photon 두 연결·종료 정리는 [공통 검증](QUANTUM_MIGRATION.md#검증)을 따른다. Steam/친구 전송 전체 이식이나 출시 인증 통과를 의미하지 않는다.
- 청크/제한시간/세대·취소/ID 테스트와 카탈로그·UI 참조 상세는 [과거 상세 검증](Notes/2026-11-08-documentation-reference-history.md)에 보존한다.
- 2026-11-15 Mirror 제거 후 새 Play 첫 Main `RunOffline`과 `QuantumLobbyChecks`(저장 맵 미보유 게스트 전송·두 연결·복귀·재시작 포함) PASS, EditMode 102개 통과.
- Mirror 경로의 과거 검증(2026-10 Steam2인 전송, KCP 스테이지 왕복)은 Mirror와 함께 폐기했다. 기록은 [Notes](Notes/2026-10-26-stage-live-validation.md)에 남아 있다.

## 남은 작업

- 최대 4인 실제 조작에서 저장 맵 전송 검증
- 상세 전송 진행률 UI는 안정성 검증 이후 진행

## 다음 작업

출시 Steam 앱과 Photon 소유권 설정을 준비하고 Steam 실계정 및 별도 PC에서 방 입장·맵 전송·재시도를 검증한다. [전환 순서](NEXT_SESSION.md#다음-작업).
