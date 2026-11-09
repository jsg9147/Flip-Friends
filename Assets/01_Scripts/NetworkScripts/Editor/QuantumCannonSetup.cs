using System;
using System.IO;
using FlipFriends.Maps;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using static QuantumOfficialTestStageSetup;

// 대포/투사체 검증용 별도 공식 시험 프리팹. 원본 Cannon.prefab(몸체 트리거 Box (1,0.81)/오프셋(0,-0.095), Trap)과
// CannonBall(반지름0.25) 값을 쓰고, 원본 스테이지에 있던 조합(좌우·아래, 지연 0.2 배수, 간격 2~3, 거리 5~20)을 넣는다.
// 빠른 대포는 원본 최대 탄속30과 정수 틱으로 나눠지지 않는 간격(0.125초=7.5틱)을 함께 본다.
public static class QuantumCannonSetup
{
    public const string StageId = "official-cannon-test";
    public const string PrefabPath = "Assets/03_Prefabs/OfficialStages/Official Cannon Test.prefab";
    public const string StagePath = QuantumOfficialStageWriter.ContentRoot + "/" + StageId + "/Stage.asset";
    public const string CannonSpriteGuid = "73d84cbed98011845993eae6ebbbb6ef";
    public const string BallSpriteGuid = "d9118376f675c14438ab1859cb4622cd";
    public const int GoalX = -14;
    public static readonly Vector2 GoalPosition = new(GoalX, 1);
    public const int WallX = 25;

    public static void Create()
    {
        if (EditorApplication.isPlaying || File.Exists(PrefabPath)) throw new InvalidOperationException("정지 상태의 새 대포 시험 프리팹 경로만 생성합니다.");
        var tile = FirstTile("Assets/03_Prefabs/Stage/Stage 1.prefab", "Ground");
        var cannonSprite = AssetDatabase.LoadAssetAtPath<Sprite>(AssetDatabase.GUIDToAssetPath(CannonSpriteGuid));
        var ballSprite = AssetDatabase.LoadAssetAtPath<Sprite>(AssetDatabase.GUIDToAssetPath(BallSpriteGuid));
        if (cannonSprite == null || ballSprite == null) throw new InvalidOperationException("원본 대포/탄 스프라이트가 없습니다.");
        var previous = SceneManager.GetActiveScene();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(scene);
        try
        {
            var root = new GameObject("Official Cannon Test");
            var stage = root.AddComponent<QuantumOfficialStage>();
            Set(stage, "stageId", StageId); Set(stage, "displayName", "대포 시험");
            var grid = Child(root, "Grid"); grid.AddComponent<Grid>();
            var ground = Child(grid, "Ground"); ground.tag = "Ground"; ground.layer = LayerMask.NameToLayer("Ground");
            var map = ground.AddComponent<Tilemap>(); ground.AddComponent<TilemapRenderer>();
            ground.AddComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
            ground.AddComponent<TilemapCollider2D>().compositeOperation = Collider2D.CompositeOperation.Merge;
            ground.AddComponent<CompositeCollider2D>().geometryType = CompositeCollider2D.GeometryType.Outlines;
            for (int x = -21; x <= 30; x++) { Paint(map, tile, x, -2); Paint(map, tile, x, -1); }
            for (int y = 0; y <= 6; y++) { Paint(map, tile, -21, y); Paint(map, tile, 30, y); Paint(map, tile, WallX, y); }
            map.CompressBounds();

            Point(root, "1P 시작", -4, 0.6f).AddComponent<QuantumStageSpawnPoint>();
            Point(root, "2P 시작", -2, 0.6f).AddComponent<QuantumStageSpawnPoint>();
            Point(root, "상자", -6, 0.5f).AddComponent<QuantumStageBox>();
            Set(Point(root, "도착", GoalPosition.x, GoalPosition.y).AddComponent<QuantumStageGoal>(), "size", new Vector2(2, 1));

            // 바닥을 따라 오른쪽으로 쏘는 원본 기본값 대포. 플레이어가 도착점으로 가는 길을 지나간다.
            Cannon(root, "왼쪽 대포", new Vector2(-19, 0.5f), Vector2.right, 0, 2, 10, 20, cannonSprite, ballSprite);
            // 벽 밖에서 왼쪽으로 쏴 지형을 통과하는 대포(Stage20 지연0.6·간격3·속도8·거리15 조합).
            Cannon(root, "벽 관통 대포", new Vector2(28, 0.5f), Vector2.left, 0.6f, 3, 8, 15, cannonSprite, ballSprite);
            // 아래로 쏴 바닥을 통과하는 대포(Stage20 회전270 대포의 방향(0,-1)·지연1.2).
            Cannon(root, "하강 대포", new Vector2(10, 8), Vector2.down, 1.2f, 3, 8, 20, cannonSprite, ballSprite);
            // 원본 최대 탄속30·짧은 거리5(Stage20)와 7.5틱 간격.
            Cannon(root, "빠른 대포", new Vector2(20, 3), Vector2.left, 0, 0.125f, 30, 5, cannonSprite, ballSprite);

            Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out bool saved);
            if (!saved) throw new InvalidOperationException("대포 시험 프리팹 저장 실패");
        }
        finally { SceneManager.SetActiveScene(previous); EditorSceneManager.CloseScene(scene, true); }
        QuantumOfficialStageWriter.Write(PrefabPath);
    }

    public static GameObject Cannon(GameObject root, string name, Vector2 position, Vector2 direction, float delay, float interval, float speed, float distance, Sprite body, Sprite ball)
    {
        var go = Point(root, name, position.x, position.y);
        var renderer = go.AddComponent<SpriteRenderer>(); renderer.sprite = body; renderer.flipX = direction.x < 0;
        var box = go.AddComponent<BoxCollider2D>(); box.size = new Vector2(1, 0.81040955f); box.offset = new Vector2(0, -0.09479523f); box.isTrigger = true;
        go.AddComponent<QuantumStageTrap>();
        var cannon = go.AddComponent<QuantumStageCannon>();
        Set(cannon, "firstShootDelay", delay); Set(cannon, "shootInterval", interval); Set(cannon, "projectileSpeed", speed);
        Set(cannon, "maxProjectileDistance", distance); Set(cannon, "shootDirection", direction); Set(cannon, "projectileSprite", ball);
        return go;
    }
}
