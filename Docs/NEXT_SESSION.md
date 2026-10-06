# 다음 세션 착수 큐

- 상태: Active
- 최종 갱신: 2026-10-06
- 목적: 다음 세션이 같은 조사를 반복하지 않도록, 남은 작업의 착수 순서와 이미 확인된 사실·먼저 정할 결정만 모아 둔다.
- 각 항목이 무엇이고 지금 구현이 어디까지 됐는지는 `Docs/UI_REBUILD.md`의 `현재 상태`와 `남은 작업`이 기준이다. 이 파일은 순서와 판단 재료만 담고 상태를 중복 기록하지 않는다.
- 항목을 끝내면 결과를 `Docs/UI_REBUILD.md`에 반영하고 이 파일에서 그 항목을 지운다. 큐가 비면 이 파일을 삭제한다.

## 시작 전 체크

1. `CLAUDE.md` 지침대로 `PROJECT_DOCUMENTATION.md` → `AGENTS.md` → `Docs/UI_REBUILD.md`를 읽고, 이 파일에서 할 항목을 고른다.
2. Unity MCP 연결을 `mcpforunity://editor/state`의 `data.advice.ready_for_tools`로 확인한다. 연결돼 있으면 플레이 모드로 검증하고, 끊겨 있으면 컴파일만 확인한다.
3. 컴파일 확인은 에디터가 프로젝트를 열고 있어도 된다.

   ```powershell
   & "C:\Program Files\Unity\Hub\Editor\6000.6.3f1\Editor\Data\DotNetSdk\dotnet.exe" build Assembly-CSharp.csproj
   ```

4. 커밋할 때 주의: 이 저장소에 git user 정보가 설정돼 있지 않아 그냥 `git commit`하면 실패한다. 기존 커밋과 같은 작성자를 쓰려면 `git -c user.name="JoSeunggeun" -c user.email="jsg9147@naver.com" commit ...` 형태로 지정한다.
5. 화면 전환을 건드렸으면 플레이 모드에서 단계마다 `ScreenNavigator.CurrentScreen`과 모든 `UIScreen`의 `activeSelf`·`CanvasGroup.alpha`·`blocksRaycasts`·`IsVisible`이 서로 맞는지 교차 확인한다. 이중 소유 버그를 잡아낸 방법이 이것이고, 같은 종류의 버그는 `activeSelf`만 보면 놓친다.

## 1. Steam 런타임 수동 검증

- Host 생성, 공개 로비 목록의 loading·empty·error·joining, 비공개 코드 참가, 각 실패 상태. MCP로 자동화할 수 없어 수동이다.
- 같이 확인할 것: Create·Join 클릭 한 번에 `HostLobby`·`JoinPrivateLobby`가 한 번만 호출되는지. Inspector 중복 리스너를 지워서 코드상으로는 1회다.

## 2. CP949 스크립트 UTF-8 변환

- `Assets/01_Scripts`에 15개 남아 있다. `GameManager`, `RopeCreatorNetwork`, `InputManager`, `ScrollViewController`, `KeyRebindingManager`, `GameRoomUI`, `Switch`, `LayerBasedSwitch`, `SavePoint`, `CameraController`, `PlayerSound`, `SteamLobbyInfo`, `RotatingObstacle`, `ObjSummonSwitch`, `PlayerSummonSwitch`.
- 목록은 다시 뽑을 수 있다.

  ```bash
  python -c "
  import os
  for root,_,fs in os.walk('Assets/01_Scripts'):
      for f in fs:
          if f.endswith('.cs'):
              p=os.path.join(root,f)
              try: open(p,'rb').read().decode('utf-8')
              except UnicodeDecodeError: print(p)
  "
  ```

- 변환은 `cp949`로 읽어 `utf-8`로 쓰고 CRLF를 유지한다. 기능 변경이 없으니 다른 이유로 그 파일을 열 때 함께 바꾸거나, 변환만 하는 커밋으로 분리한다.
