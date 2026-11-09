using System;
using System.IO;
using FlipFriends.Maps;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using static QuantumOfficialTestStageSetup;

public static class QuantumMotionSetup
{
    public const string StageId = "official-motion-test";
    public const string PrefabPath = "Assets/03_Prefabs/OfficialStages/Official Motion Test.prefab";
    public const string StagePath = QuantumOfficialStageWriter.ContentRoot + "/" + StageId + "/Stage.asset";

    private static void Set(UnityEngine.Object target, string field, object value)
    {
        var member = target.GetType().GetField(field, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        if (member.FieldType.IsEnum) value = Enum.ToObject(member.FieldType, value);
        member.SetValue(target, value); EditorUtility.SetDirty(target);
    }

    public static void Create()
    {
        if (EditorApplication.isPlaying || File.Exists(PrefabPath)) throw new InvalidOperationException("정지 상태의 새 이동 장치 시험 경로만 생성합니다.");
        var tile = FirstTile("Assets/03_Prefabs/Stage/Stage 1.prefab", "Ground");
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(QuantumLauncherSetup.SpritePath);
        var previous = SceneManager.GetActiveScene(); var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(scene);
        try
        {
            var root = new GameObject("Official Motion Test"); var stage = root.AddComponent<QuantumOfficialStage>();
            Set(stage, "stageId", StageId); Set(stage, "displayName", "이동 발판·트랩 시험");
            var grid = Child(root, "Grid"); grid.AddComponent<Grid>(); var ground = Child(grid, "Ground");
            ground.tag = "Ground"; ground.layer = LayerMask.NameToLayer("Ground");
            var map = ground.AddComponent<Tilemap>(); ground.AddComponent<TilemapRenderer>();
            ground.AddComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
            ground.AddComponent<TilemapCollider2D>().compositeOperation = Collider2D.CompositeOperation.Merge;
            ground.AddComponent<CompositeCollider2D>().geometryType = CompositeCollider2D.GeometryType.Outlines;
            for (int x = -20; x <= 20; x++) { Paint(map, tile, x, -2); Paint(map, tile, x, -1); }
            map.CompressBounds();
            Point(root, "1P 시작", -1, 1.8f).AddComponent<QuantumStageSpawnPoint>();
            Point(root, "2P 시작", 1, 1.8f).AddComponent<QuantumStageSpawnPoint>();
            Point(root, "상자", -1.9f, 1.8f).AddComponent<QuantumStageBox>();
            Set(Point(root, "도착", 16, 1).AddComponent<QuantumStageGoal>(), "size", new Vector2(4, 2));
            QuantumStageMotion Motion(string name, Vector2 position, Vector2 size, int kind)
            {
                var go = Point(root, name, position.x, position.y); var renderer = go.AddComponent<SpriteRenderer>(); renderer.sprite = sprite;
                renderer.drawMode = SpriteDrawMode.Sliced; renderer.size = size;
                if (kind == 1) { var circle = go.AddComponent<CircleCollider2D>(); circle.radius = .5f; circle.isTrigger = true; }
                else { var collider = go.AddComponent<BoxCollider2D>(); collider.size = size; collider.isTrigger = kind != 0; }
                if (kind != 0) go.AddComponent<QuantumStageTrap>();
                var motion = go.AddComponent<QuantumStageMotion>(); Set(motion, "kind", kind); return motion;
            }
            var platform = Motion("왕복 발판", new Vector2(0, 1), new Vector2(6, .5f), 0);
            Set(platform, "waypoints", new[] { Vector2.zero, new Vector2(6, 0) }); Set(platform, "speed", .25f); Set(platform, "waitTime", .2f); Set(platform, "easeAmount", .2f);
            var detected = Motion("감지 전환 발판", new Vector2(-10, 1), new Vector2(4, .5f), 0);
            Set(detected, "waypoints", new[] { Vector2.zero, new Vector2(0, 3), new Vector2(4, 3) });
            Set(detected, "cyclic", false); Set(detected, "detectPlayer", true); Set(detected, "initialWaitTime", .3f);
            Set(detected, "returnDelay", .5f); Set(detected, "disappearAfterComplete", true); Set(detected, "disableColliderWhenFalling", true);
            var button = Point(root, "발판 리셋", -14, 1); button.AddComponent<BoxCollider2D>().isTrigger = true;
            Set(button.AddComponent<QuantumStageMotionReset>(), "target", detected);
            var moving = Motion("이동 트랩", new Vector2(9, 1), new Vector2(1, 1), 1);
            Set(moving, "waypoints", new[] { Vector2.zero, new Vector2(0, 3) }); Set(moving, "speed", 2f); Set(moving, "rotationSpeed", 180f);
            var rotating = Motion("랜덤 회전 트랩", new Vector2(12, 2), new Vector2(3, .3f), 2);
            Set(rotating, "rotationSpeed", 120f); Set(rotating, "randomRotation", true); Set(rotating, "randomRange", 20f);
            var path = Motion("경로 트랩", new Vector2(-16, 5), new Vector2(1, 1), 3);
            Set(path, "waypoints", new[] { Vector2.zero, new Vector2(3, 0), new Vector2(3, 2) });
            Set(path, "cyclic", false); Set(path, "repeat", true); Set(path, "speed", .5f);
            for (int i = 0; i < 2; i++)
            {
                var go = Point(root, "자동 전환 지형 " + i, -7 + i * 4, 4);
                var renderer = go.AddComponent<SpriteRenderer>(); renderer.sprite = sprite; renderer.drawMode = SpriteDrawMode.Sliced; renderer.size = new Vector2(3, .5f);
                go.AddComponent<BoxCollider2D>().size = new Vector2(3, .5f);
                var toggle = go.AddComponent<QuantumStageTerrainToggle>(); Set(toggle, "initialActive", i == 0);
            }
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out bool saved); if (!saved) throw new InvalidOperationException("이동 시험 저장 실패");
        }
        finally { SceneManager.SetActiveScene(previous); EditorSceneManager.CloseScene(scene, true); }
        QuantumOfficialStageWriter.Write(PrefabPath);
    }
}
