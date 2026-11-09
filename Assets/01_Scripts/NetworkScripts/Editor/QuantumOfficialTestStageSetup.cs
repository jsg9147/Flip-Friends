using System;
using System.IO;
using FlipFriends.Maps;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

// 공식 스테이지 경로 검증용 작은 프리팹. 일부러 MapData 배치 범위(x−50~50) 밖에 두어 공식 경로가 그 규칙을 거치지 않음을 확인한다.
public static class QuantumOfficialTestStageSetup
{
    public const string PrefabPath = "Assets/03_Prefabs/OfficialStages/Official Test Stage.prefab";
    public const string StageId = "official-test";

    public static void Create()
    {
        if (EditorApplication.isPlaying || File.Exists(PrefabPath)) throw new InvalidOperationException("정지 상태의 새 시험 프리팹 경로만 생성합니다.");
        var groundTile = FirstTile("Assets/03_Prefabs/Stage/Stage 1.prefab", "Ground");
        var resetTile = FirstTile("Assets/03_Prefabs/Stage/Stage 9.prefab", "Grid/First ResetTile");
        var checkpointSprite = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/03_Prefabs/Stage/Stage 15.prefab").GetComponentsInChildren<SavePoint>(true)[0].GetComponent<SpriteRenderer>().sprite;
        var summon = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/03_Prefabs/Obstacle/Box Switch Prefab.prefab").GetComponentInChildren<ObjSummonSwitch>(true);

        var previous = SceneManager.GetActiveScene();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(scene);
        try
        {
            var root = new GameObject("Official Test Stage");
            var stage = root.AddComponent<QuantumOfficialStage>();
            Set(stage, "stageId", StageId); Set(stage, "displayName", "공식 시험");
            var grid = Child(root, "Grid"); grid.AddComponent<Grid>();

            var ground = Child(grid, "Ground"); ground.tag = "Ground"; ground.layer = LayerMask.NameToLayer("Ground");
            var groundMap = ground.AddComponent<Tilemap>(); ground.AddComponent<TilemapRenderer>();
            ground.AddComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
            ground.AddComponent<TilemapCollider2D>().compositeOperation = Collider2D.CompositeOperation.Merge;
            ground.AddComponent<CompositeCollider2D>().geometryType = CompositeCollider2D.GeometryType.Outlines;
            for (int x = 98; x <= 141; x++) { Paint(groundMap, groundTile, x, -2); Paint(groundMap, groundTile, x, 12); if (x < 119 || x > 120) Paint(groundMap, groundTile, x, -1); }
            for (int y = -1; y <= 11; y++) { Paint(groundMap, groundTile, 98, y); Paint(groundMap, groundTile, 141, y); }
            for (int x = 133; x <= 140; x++) Paint(groundMap, groundTile, x, 0);
            // 속이 빈 섬: 바깥 윤곽과 안쪽 구멍 윤곽을 함께 만든다.
            for (int x = 125; x <= 129; x++) for (int y = 4; y <= 7; y++) if (x == 125 || x == 129 || y == 4 || y == 7) Paint(groundMap, groundTile, x, y);

            var reset = Child(grid, "Pit Reset"); reset.layer = LayerMask.NameToLayer("Reset");
            var resetMap = reset.AddComponent<Tilemap>(); reset.AddComponent<TilemapRenderer>();
            reset.AddComponent<TilemapCollider2D>().isTrigger = true;
            Paint(resetMap, resetTile, 119, -1); Paint(resetMap, resetTile, 120, -1);
            var resetHandler = reset.AddComponent<QuantumStageReset>();
            Set(resetHandler, "destination", Point(root, "Reset 목적지", 112.5f, 0.6f).transform);

            Point(root, "1P 시작", 104.5f, 0.6f).AddComponent<QuantumStageSpawnPoint>();
            Point(root, "2P 시작", 106.5f, 0.6f).AddComponent<QuantumStageSpawnPoint>();
            var box = Point(root, "상자", 102.5f, 0.5f).AddComponent<QuantumStageBox>();
            var goal = Point(root, "도착", 137, 1.5f).AddComponent<QuantumStageGoal>(); Set(goal, "size", new Vector2(2, 1));
            Point(root, "로프", 99.5f, 5).AddComponent<QuantumStageRope>();

            var checkpoint = Point(root, "체크포인트", 123.5f, 0.6f);
            checkpoint.AddComponent<SpriteRenderer>().sprite = checkpointSprite;
            Set(checkpoint.AddComponent<QuantumStageCheckpoint>(), "checkpointId", 1);

            var spawner = Point(root, "상자 소환", 100.5f, 0.5f);
            spawner.AddComponent<SpriteRenderer>().sprite = summon.defaultSprite;
            var spawnerComponent = spawner.AddComponent<QuantumStageBoxSpawner>();
            Set(spawnerComponent, "target", box); Set(spawnerComponent, "destination", Point(root, "소환 목적지", 110.5f, 0.5f).transform);
            Set(spawnerComponent, "pressedSprite", summon.pressedSprite);

            // 칠하기 전 원점이 Tilemap 범위에 남으면 렌더러 범위와 추락 경계가 원점까지 늘어난다.
            groundMap.CompressBounds(); resetMap.CompressBounds();
            Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out bool saved);
            if (!saved) throw new InvalidOperationException("시험 공식 스테이지 프리팹 저장 실패");
        }
        finally { SceneManager.SetActiveScene(previous); EditorSceneManager.CloseScene(scene, true); }
        QuantumOfficialStageWriter.Write(PrefabPath);
    }

    internal static TileBase FirstTile(string prefabPath, string path)
    {
        var map = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath).transform.Find(path).GetComponent<Tilemap>();
        foreach (var cell in map.cellBounds.allPositionsWithin) { var tile = map.GetTile(cell); if (tile != null) return tile; }
        throw new InvalidOperationException($"{prefabPath}/{path}: 타일이 없습니다.");
    }

    internal static void Paint(Tilemap map, TileBase tile, int x, int y) => map.SetTile(new Vector3Int(x, y, 0), tile);

    internal static GameObject Child(GameObject parent, string name)
    {
        var child = new GameObject(name); child.transform.SetParent(parent.transform, false); return child;
    }

    internal static GameObject Point(GameObject root, string name, float x, float y)
    {
        var point = Child(root, name); point.transform.position = new Vector3(x, y, 0); return point;
    }

    internal static void Set(UnityEngine.Object target, string field, object value)
    {
        var serialized = new SerializedObject(target); var property = serialized.FindProperty(field);
        switch (value)
        {
            case string text: property.stringValue = text; break;
            case int number: property.intValue = number; break;
            case float number: property.floatValue = number; break;
            case bool flag: property.boolValue = flag; break;
            case Vector2 vector: property.vector2Value = vector; break;
            case UnityEngine.Object reference: property.objectReferenceValue = reference; break;
            default: throw new ArgumentException(field);
        }
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
}
