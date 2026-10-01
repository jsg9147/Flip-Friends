---
paths:
  - "Assets/01_Scripts/MapEditorScripts/**/*.cs"
---

# 맵 에디터 규칙

- 저장 형식, 팔레트, 검증 규칙의 기준 문서는 `Docs/MAP_EDITOR.md`다. 데이터 형식을 바꾸기 전에 읽는다.
- 저장 형식을 바꾸면 런타임 전송·생성도 깨진다. `Docs/CUSTOM_MAP_NETWORK.md`를 함께 확인하고 두 문서를 같은 작업에서 갱신한다.
- `MapTransferCore`는 `FlipFriends.MapTransferCore.asmdef`로 분리된 어셈블리다. Unity API에 의존하지 않는 코드만 넣는다. 테스트가 `Assets/Tests/EditMode`에서 이 어셈블리를 참조한다.
- 기존 맵 파일을 못 읽게 만드는 변경은 버전 필드와 마이그레이션 경로를 함께 넣는다.
