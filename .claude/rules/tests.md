---
paths:
  - "Assets/Tests/**/*.cs"
  - "Assets/Tests/**/*.asmdef"
---

# 테스트 규칙

- Edit Mode 테스트는 `Assets/Tests/EditMode`에 둔다. 새 테스트는 맞는 `.asmdef`에 넣고, 참조 어셈블리를 추가해야 하면 `.asmdef`의 `references`를 고친다.
- 파일명은 대상 이름을 따른다. 어셈블리별로 폴더를 나눈다: 루트는 `FlipFriends.MapTransferCore.Tests`, `MapEditorCore/`는 `FlipFriends.MapEditorCore.Tests`.
- 파일을 쓰는 테스트는 `MapDataRepository.SaveDirectoryOverride`로 임시 폴더를 쓰고 `TearDown`에서 지운다. 실제 `persistentDataPath`를 건드리지 않는다.
- 예상된 `Debug.LogError`·`LogException`은 `LogAssert.Expect`로 선언한다. 안 하면 테스트가 실패한다.
- Unity API에 의존하는 코드는 Edit Mode에서 테스트하기 어렵다. 순수 로직을 `FlipFriends.MapTransferCore`처럼 별도 어셈블리로 빼서 테스트한다.
- 테스트를 추가하거나 고치면 대상 기능 문서의 `검증` 절에 어떤 테스트가 무엇을 보장하는지 한 줄로 적는다.
