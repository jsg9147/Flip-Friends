using System;
using System.IO;
using FlipFriends.Maps;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using static QuantumOfficialTestStageSetup;

public static class QuantumLauncherSetup
{
    public const string PrefabPath = "Assets/03_Prefabs/OfficialStages/Official Launcher Test.prefab";
    public const string StageId = "official-launcher-test";
    public const string StagePath = QuantumOfficialStageWriter.ContentRoot + "/" + StageId + "/Stage.asset";
    public const string SpritePath = "Assets/02_Sprites/Obstacle/bouncy ball.png";

    public static void Create()
    {
        if (EditorApplication.isPlaying || File.Exists(PrefabPath)) throw new InvalidOperationException("정지 상태의 새 스프링/바운스 시험 프리팹 경로만 생성합니다.");
        var tile = FirstTile("Assets/03_Prefabs/Stage/Stage 1.prefab", "Ground");
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath);
        if (sprite == null) throw new InvalidOperationException("원본 바운스 스프라이트가 없습니다.");
        var previous = SceneManager.GetActiveScene();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(scene);
        try
        {
            var root = new GameObject("Official Launcher Test");
            var stage = root.AddComponent<QuantumOfficialStage>();
            Set(stage, "stageId", StageId); Set(stage, "displayName", "스프링/바운스 시험");
            var grid = Child(root, "Grid"); grid.AddComponent<Grid>();
            var ground = Child(grid, "Ground"); ground.tag = "Ground"; ground.layer = LayerMask.NameToLayer("Ground");
            var map = ground.AddComponent<Tilemap>(); ground.AddComponent<TilemapRenderer>();
            ground.AddComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
            ground.AddComponent<TilemapCollider2D>().compositeOperation = Collider2D.CompositeOperation.Merge;
            ground.AddComponent<CompositeCollider2D>().geometryType = CompositeCollider2D.GeometryType.Outlines;
            for (int x = -18; x <= 18; x++) { Paint(map, tile, x, -2); Paint(map, tile, x, -1); }
            map.CompressBounds();
            Point(root, "1P 시작", -3, 0.6f).AddComponent<QuantumStageSpawnPoint>();
            Point(root, "2P 시작", -10, 0.6f).AddComponent<QuantumStageSpawnPoint>();
            Point(root, "상자", -4, 0.5f).AddComponent<QuantumStageBox>();
            Set(Point(root, "도착", -1, 1).AddComponent<QuantumStageGoal>(), "size", new Vector2(2, 1));
            var bounce = Point(root, "바운스", 4, 0.5f);
            bounce.AddComponent<SpriteRenderer>().sprite = sprite;
            bounce.AddComponent<CircleCollider2D>().isTrigger = true;
            bounce.GetComponent<CircleCollider2D>().radius = 1;
            bounce.AddComponent<QuantumStageLauncher>();
            var spring = Point(root, "스프링", -6, -0.3f);
            spring.AddComponent<SpriteRenderer>().sprite = sprite;
            var box = spring.AddComponent<BoxCollider2D>(); box.size = new Vector2(2, 1); box.isTrigger = true;
            Set(spring.AddComponent<QuantumStageLauncher>(), "kind", 1);
            // 스프링과 겹친 로프에서 진입해도 등반 상태/차단 타이머를 새로 바꾸지 않는다.
            Set(Point(root, "로프", -6, 4).AddComponent<QuantumStageRope>(), "size", new Vector2(1, 8));
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out bool saved);
            if (!saved) throw new InvalidOperationException("스프링/바운스 시험 프리팹 저장 실패");
        }
        finally { SceneManager.SetActiveScene(previous); EditorSceneManager.CloseScene(scene, true); }
        QuantumOfficialStageWriter.Write(PrefabPath);
    }
}
