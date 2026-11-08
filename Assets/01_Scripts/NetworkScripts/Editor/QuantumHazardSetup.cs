using System;
using System.IO;
using FlipFriends.Maps;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using static QuantumOfficialTestStageSetup;

// 피격·넉백 검증용 별도 공식 시험 프리팹. 기존 official-test·official-slope-test는 건드리지 않는다.
// 오른쪽 가시는 원본 Stage3 가시(삼각 Polygon 트리거, 위치 기반 방향), 공중 사각은 원본 회전 트랩에 가장 많은 knockbackDir(-1,0) 지정 방향이다.
public static class QuantumHazardSetup
{
    public const string PrefabPath = "Assets/03_Prefabs/OfficialStages/Official Hazard Test.prefab";
    public const string StageId = "official-hazard-test";
    public const string StagePath = QuantumOfficialStageWriter.ContentRoot + "/" + StageId + "/Stage.asset";
    private const string ThornSpritePath = "Assets/02_Sprites/Obstacle/Thron1 16x16.png";
    public static readonly Vector2 ThornPosition = new(4, 0.5f);
    public static readonly Vector2 DirectedPosition = new(-8, 2.3f);
    public static readonly Vector2 GoalPosition = new(-13, 1);
    public static readonly Vector2 WallTrapPosition = new(24.5f, 0.5f);

    public static void Create()
    {
        if (EditorApplication.isPlaying || File.Exists(PrefabPath)) throw new InvalidOperationException("정지 상태의 새 피격 시험 프리팹 경로만 생성합니다.");
        var groundTile = FirstTile("Assets/03_Prefabs/Stage/Stage 1.prefab", "Ground");
        var thorn = AssetDatabase.LoadAssetAtPath<Sprite>(ThornSpritePath);
        if (thorn == null) throw new InvalidOperationException("원본 가시 스프라이트가 없습니다: " + ThornSpritePath);

        var previous = SceneManager.GetActiveScene();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(scene);
        try
        {
            var root = new GameObject("Official Hazard Test");
            var stage = root.AddComponent<QuantumOfficialStage>();
            Set(stage, "stageId", StageId); Set(stage, "displayName", "피격 시험");
            var grid = Child(root, "Grid"); grid.AddComponent<Grid>();
            var ground = Child(grid, "Ground"); ground.tag = "Ground"; ground.layer = LayerMask.NameToLayer("Ground");
            var groundMap = ground.AddComponent<Tilemap>(); ground.AddComponent<TilemapRenderer>();
            ground.AddComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
            ground.AddComponent<TilemapCollider2D>().compositeOperation = Collider2D.CompositeOperation.Merge;
            ground.AddComponent<CompositeCollider2D>().geometryType = CompositeCollider2D.GeometryType.Outlines;
            for (int x = -17; x <= 25; x++) { Paint(groundMap, groundTile, x, -2); Paint(groundMap, groundTile, x, -1); }
            for (int y = 0; y <= 6; y++) { Paint(groundMap, groundTile, -17, y); Paint(groundMap, groundTile, 25, y); }
            groundMap.CompressBounds();

            Point(root, "1P 시작", -4, 0.6f).AddComponent<QuantumStageSpawnPoint>();
            Point(root, "2P 시작", -2, 0.6f).AddComponent<QuantumStageSpawnPoint>();
            Point(root, "상자", -5, 0.5f).AddComponent<QuantumStageBox>();
            Set(Point(root, "도착", GoalPosition.x, GoalPosition.y).AddComponent<QuantumStageGoal>(), "size", new Vector2(2, 1));

            var spike = Point(root, "가시", ThornPosition.x, ThornPosition.y);
            spike.AddComponent<SpriteRenderer>().sprite = thorn;
            spike.AddComponent<PolygonCollider2D>().SetPath(0, new[] { new Vector2(0, 0.5f), new Vector2(-0.5f, -0.5f), new Vector2(0.5f, -0.5f) });
            spike.GetComponent<PolygonCollider2D>().isTrigger = true;
            spike.AddComponent<QuantumStageTrap>();

            var directed = Point(root, "방향 트랩", DirectedPosition.x, DirectedPosition.y);
            directed.AddComponent<SpriteRenderer>().sprite = thorn;
            var box = directed.AddComponent<BoxCollider2D>(); box.size = Vector2.one; box.isTrigger = true;
            Set(directed.AddComponent<QuantumStageTrap>(), "knockbackDirection", Vector2.left);

            AddWallTrap(root, thorn);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out bool saved);
            if (!saved) throw new InvalidOperationException("피격 시험 프리팹 저장 실패");
        }
        finally { SceneManager.SetActiveScene(previous); EditorSceneManager.CloseScene(scene, true); }
        QuantumOfficialStageWriter.Write(PrefabPath);
    }

    // 오른쪽 벽에 붙은 지정 방향(1,0) 트랩. 넉백이 벽에 막혀 접촉이 이어지므로 무적 60틱 뒤 재피격과 벽 인접 비관통을 실제 입력으로 본다.
    public static void AddWallTrap(GameObject root, Sprite sprite)
    {
        var wall = Point(root, "벽 트랩", WallTrapPosition.x, WallTrapPosition.y);
        wall.AddComponent<SpriteRenderer>().sprite = sprite;
        var box = wall.AddComponent<BoxCollider2D>(); box.size = Vector2.one; box.isTrigger = true;
        Set(wall.AddComponent<QuantumStageTrap>(), "knockbackDirection", Vector2.right);
    }
}
