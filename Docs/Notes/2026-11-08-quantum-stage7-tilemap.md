# Quantum Stage7 Ground/Reset 작성기 검사

- 기준: 제품6/ff-product-6, Unity 6000.6.8f1, Quantum 3.0.14 Stable 2241. 버전은 바꾸지 않았다.
- 방법: 연결 에디터(정지, Main 보존)의 임시 Additive 씬에 `Stage 7.prefab` **전체**를 인스턴스화하고, `Grid/Ground`·`Grid/ResetTile`·`RespawnPoint`와 Grid 조상만 남긴 뒤 `Physics2D.SyncTransforms` → 기존 `QuantumTilemapSetup.ReadTerrain/ReadReset` → `QuantumMapTerrain.Apply`/`QuantumMapDevices.Apply`를 그대로 호출했다. 씬은 저장하지 않고 닫았다. 원본 프리팹·작성기 규칙은 고치지 않았다.
- 제외한 원본 요소: `Grid/RopeTile`(로프), MoveTraps, Switch, DoorSwitch, Door, Box, 텍스트, Background.

## Ground (`Grid/Ground`)

- Tag/Layer Ground, Merge+Kinematic Composite Outlines, 반경0, vertexDistance0.0005, offsetDistance5E-05, Transform 원점·XY 평면, Collider2개, MonoBehaviour 없음. 렌더러 범위 중심(29.5,28.5)·반 크기(60.5,32.5).
- 생성 윤곽 4개, 변 62개(상한 64/512 이내). 경사 거부 변 0, 교차/접촉·열린 윤곽·중복·비유한 값 거부 없음. 판정 범위는 렌더러 범위 안이다.

| 윤곽 | 꼭짓점 | 방향 | 포함 깊이 | 범위 | 오목 | 미세 기울기 변 | 최대 작은 축 변화 |
|---|---|---|---|---|---|---|---|
| 1 외곽 | 20 | CW | 0 | (-4,-4)~(83,21) | 8 | 19 | 4raw |
| 0 내부 빈 공간 | 34 | CCW | 1 | (-3,-3)~(82,20) | 15 | 32 | 2raw |
| 2 섬(속 빈 기둥) | 4 | CCW | 2 | (12,-2)~(15,16) | 0 | 2 | 2raw |
| 3 섬 안 구멍 | 4 | CW | 3 | (13,-1)~(14,15) | 0 | 2 | 2raw |

- 미세 기울기는 Composite 생성값이다(예: (11,2.93E-05)→(10.00003,0), (82,14.9999714)→(79.00003,15)). Q 규칙(16비트 소수 반올림)으로만 변환했고 스냅·AABB 채우기는 하지 않았다. 모든 변의 작은 축 변화가 8raw 이하라 거부 대상이 없다.
- 형상: 왼쪽 방(x -3~35, 바닥 -3)에 아래가 뚫린 벽(x10~11, y0~20), 바닥과 1칸 떨어진 속 빈 기둥(윤곽2/3), 창(y4~6)이 있는 벽(x16~17). 오른쪽 위는 x35~46 높이12 단, 구덩이A(x46~55, 바닥6), 단(x55~59, 높이12), 구덩이B(x59~74, 바닥4), 방(x74~82, 바닥12)이다. 천장은 x46~74에서 15다.

## Reset (`Grid/ResetTile`)

- 비합성 정적 Tilemap 트리거, Collider1개, 마스크 2304(Player+Pickable), onlyBoxReset=false, 목적지 `RespawnPoint`(57.13,12.5).
- 생성 Polygon 15조각, 모두 1×1 축 정렬 사각, 중심 (59.5~73.5, 4.5) 한 줄로 구덩이B 바닥 위다. 빈 구간이나 경계를 합치지 않았다.
- 렌더러 범위는 중심(39,1)·크기(78,8)로 조각 범위보다 넓다(Tilemap cellBounds 기준). 판정에는 쓰지 않는다.

## 조합 검증

- Stage7 Ground+ResetTile과 임시 배치(시작점 (40,12.7)/(41.5,12.7), 상자 (43.5,12.6), 도착 (48.5,6.6), 박스 베이크 경로 검사용 지형 (-35,-20))로 Convert→장치→지형 Apply까지 통과했다. 콘텐츠 버전 6, 해시 5454253e…, 경계 X -45.5~100·Y -30.5. 목적지 (57.13,12.5)는 단 윗면(y≈12)과 겹치지 않는다. 기존 6:6 manifest는 `6:6:3450d808…`로 그대로다.
- 콘텐츠 버전은 기능에서 결정된다(`HasTerrain`→6). Stage7 조합도 새 변환 규칙이 없으므로 6:6 형식이고 해시로만 구분된다.

## 배치 제약 (사용례 미추가 원인)

- MapData 검증 규칙(`MapValidationRules`)이 시작점·도착·상자 좌표를 x −50~50, y −30~30으로 제한한다. 이 규칙은 완화하지 않는다.
- Reset 조각(x59~74)과 목적지(57.13)에 닿는 구역은 x55~82의 단·구덩이B·방뿐이며 모두 x>50이다. x≤50 구역에서는 구덩이A(깊이6)와 구덩이B(폭15)를 건널 수 없다. 점프 높이 ≈1.98(13.2²/(2×44)), 밟기 ≈2.85, 이동 4~5.5이고, 원본은 이 구간을 제외한 로프로 건넌다.
- 결과: 작성기는 두 Collider를 모두 수용하지만, 규칙 안의 배치로는 실제 입력으로 Stage7 Reset을 밟는 사용례를 만들 수 없다. 따라서 새 콘텐츠·메뉴 등록·러너는 만들지 않았다.
- 필요한 최소 변경(미구현, 선택 필요): ① MapData 좌표 범위 밖 원본 배치를 허용하는 별도 작성 경로 설계, 또는 ② Stage7 Ground만 쓰고 Reset은 이미 지원하는 사용례와 조합, 또는 ③ 원본 로프(`Grid/RopeTile`, Tilemap 로프 변환은 현재 미지원)를 함께 옮기는 검토. ③도 x>50 배치 제약은 그대로 남는다.
