# 2026-11-15 Mirror 제거 착수 기준값

기준 커밋 `4355286`. Mirror 제거 후 같은 값이 나와야 한다. 각 콘텐츠는 `ValidateAndApply(new RuntimeConfig())`로 재변환한 해시와 저장 해시가 같았다(전부 OK).

## 공식 시험 8종 (Agreement)

| StageId | Agreement |
|---|---|
| official-test | 공식1:f7695175f228c1e20b9481517d73898374aab6ea96eaedde885bf36cf49442c9 |
| official-slope-test | 공식1:ead65eb3cba1920f83885f660ce53f09e8153d85cbd5001c616196139d230287 |
| official-hazard-test | 공식1:5100d2c66f594f23cc9ee3cff626338745182cc457d17557b68b4408cedd1c19 |
| official-launcher-test | 공식1:3384b7e4105060e0f0b32cc0a92be8dfdaa0b969b7d242fd553cc745336bb89e |
| official-motion-test | 공식1:c9bd60ef653e25285cfe6d41c67b758d33582a45977e5ee34ac24b57b592354b |
| official-switch-test | 공식2:1505d624064ea08219dd263ccf445283857e87d1e408ffcf34bacd4e3bed5ad1 |
| official-conveyor-test | 공식3:12b0726fcfb2ccb82fa0b4dcbcd6e3882e7b6911aaaff2a60b8cb1f88d6f3292 |
| official-cannon-test | 공식4:65a51a85e15dcb0eca0eae55ef43956898fcf2cda342a001631955c05477f239 |

## Quantum 매니페스트 6종 (Agreement)

| 에셋 | Agreement |
|---|---|
| FlipFriendsCustomMapManifest | 2:2:1853f2f5245ad4570894b368d4953d8e4447a653f688b0e028b8854171b0b052 |
| FlipFriendsDeviceMapManifest | 5:5:f751db1cf99dc6cf253e461a0f5b029403af9cb21a45be6e9d0d74be75be9e45 |
| FlipFriendsInteractionMapManifest | 3:3:cb7434191c38af6019ea3865b7d0855f5af5d17aa3d59c1b31f3a5a1094b03d5 |
| FlipFriendsRespawnMapManifest | 4:4:5efdc1a8661063e52fd6e32132ff2280b4e761769d7a2cb09d6a74cd37c32e27 |
| FlipFriendsRopeMapManifest | 2:2:e5ab54034f8b698c0f62cba13acc46cabc46d82178e1605b691ac71d596fea3c |
| FlipFriendsTilemapMapManifest | 6:6:3450d8081224b87f132fdc40dfec90479c91c50e059817a915a2ded6eb995930 |

매니페스트 팔레트는 전부 `QuantumMapEditorPalette.asset`이다.

## 새 Play 첫 Main `QuantumProductChecks.RunOffline`

통과. 장치·복귀·상호작용·이동·정적/로프 재생, Main 왕복, 실패/취소/중복/씬 왕복, Unity 오류 0.
