using System;
using System.IO;
using FlipFriends.Maps;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using static QuantumOfficialTestStageSetup;

// 고정·이동 벨트, 벨트만 있는 수동 그룹, 지형과 묶인 자동 그룹을 한 시험 경로에 둔다.
public static class QuantumConveyorSetup
{
    public const string StageId = "official-conveyor-test";
    public const string PrefabPath = "Assets/03_Prefabs/OfficialStages/Official Conveyor Test.prefab";
    public const string StagePath = QuantumOfficialStageWriter.ContentRoot + "/" + StageId + "/Stage.asset";
    public const int GoalX = 20;

    public static void Create()
    {
        if (EditorApplication.isPlaying || File.Exists(PrefabPath))
            throw new InvalidOperationException("정지 상태의 새 벨트 시험 경로만 생성합니다.");
        var tile = FirstTile("Assets/03_Prefabs/Stage/Stage 1.prefab", "Ground");
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(QuantumLauncherSetup.SpritePath);
        var previous = SceneManager.GetActiveScene();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(scene);
        try
        {
            var root = new GameObject("Official Conveyor Test");
            var stage = new SerializedObject(root.AddComponent<QuantumOfficialStage>());
            stage.FindProperty("stageId").stringValue = StageId;
            stage.FindProperty("displayName").stringValue = "컨베이어 벨트 시험";
            stage.ApplyModifiedPropertiesWithoutUndo();
            var grid = Child(root, "Grid");
            grid.AddComponent<Grid>();
            var ground = Child(grid, "Ground");
            ground.tag = "Ground";
            ground.layer = LayerMask.NameToLayer("Ground");
            var map = ground.AddComponent<Tilemap>();
            ground.AddComponent<TilemapRenderer>();
            ground.AddComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
            ground.AddComponent<TilemapCollider2D>().compositeOperation = Collider2D.CompositeOperation.Merge;
            ground.AddComponent<CompositeCollider2D>().geometryType = CompositeCollider2D.GeometryType.Outlines;
            // 벨트 자리(윗줄 3칸)는 비워 벨트 윗면이 지면 높이와 같게 한다.
            bool IsBelt(int x) => x >= -1 && x <= 1 || x >= 4 && x <= 6 || x >= 9 && x <= 11;
            for (int x = -12; x <= 28; x++) { Paint(map, tile, x, -2); if (!IsBelt(x)) Paint(map, tile, x, -1); }
            map.CompressBounds();
            Point(root, "1P 시작", -3, .55f).AddComponent<QuantumStageSpawnPoint>();
            Point(root, "2P 시작", -5, .55f).AddComponent<QuantumStageSpawnPoint>();
            Point(root, "상자", -7, .55f).AddComponent<QuantumStageBox>();
            var goal = new SerializedObject(Point(root, "도착", GoalX, 1).AddComponent<QuantumStageGoal>());
            goal.FindProperty("size").vector2Value = new Vector2(4, 2);
            goal.ApplyModifiedPropertiesWithoutUndo();

            GameObject Part(string name, float x, float y, Vector2 size, bool trigger)
            {
                var go = Point(root, name, x, y);
                var renderer = go.AddComponent<SpriteRenderer>();
                renderer.sprite = sprite;
                renderer.drawMode = SpriteDrawMode.Sliced;
                renderer.size = size;
                var collider = go.AddComponent<BoxCollider2D>();
                collider.size = size;
                collider.isTrigger = trigger;
                return go;
            }

            QuantumStageConveyor Belt(GameObject go, QuantumConveyorDirection direction, string group, float interval)
            {
                var belt = go.AddComponent<QuantumStageConveyor>();
                var serialized = new SerializedObject(belt);
                serialized.FindProperty("initialDirection").enumValueIndex = (int)direction;
                serialized.FindProperty("group").stringValue = group;
                serialized.FindProperty("interval").floatValue = interval;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                return belt;
            }

            // 지형 홈과 경계를 공유하면 겹침으로 거부되므로 옆·아래에 0.01 틈을 두고 윗면은 지면 높이(0)에 맞춘다.
            var beltSize = new Vector2(2.98f, .99f);
            const float beltY = -.495f;
            Belt(Part("고정 벨트", .5f, beltY, beltSize, false), QuantumConveyorDirection.Counterclockwise, "", 0);
            var manual = Belt(Part("수동 벨트", 5.5f, beltY, beltSize, false), QuantumConveyorDirection.Clockwise, "belt-manual", 0);
            Belt(Part("혼합 벨트", 10.5f, beltY, beltSize, false), QuantumConveyorDirection.Counterclockwise, "mixed", 3);
            var toggle = new SerializedObject(Part("혼합 지형", 10.5f, 4.5f, new Vector2(2, .5f), false).AddComponent<QuantumStageTerrainToggle>());
            toggle.FindProperty("group").stringValue = "mixed";
            toggle.FindProperty("interval").floatValue = 3;
            toggle.FindProperty("initialActive").boolValue = true;
            toggle.ApplyModifiedPropertiesWithoutUndo();
            var button = new SerializedObject(Part("벨트 버튼", -3, 1.6f, new Vector2(1.25f, .4f), false).AddComponent<QuantumStageSwitch>());
            button.FindProperty("conveyor").objectReferenceValue = manual;
            button.ApplyModifiedPropertiesWithoutUndo();
            var moving = Part("이동 벨트", 24.5f, 2, new Vector2(3, .5f), false);
            var motion = new SerializedObject(moving.AddComponent<QuantumStageMotion>());
            motion.FindProperty("kind").enumValueIndex = (int)QuantumMotionKind.Platform;
            var points = motion.FindProperty("waypoints");
            points.arraySize = 2;
            points.GetArrayElementAtIndex(0).vector2Value = Vector2.zero;
            points.GetArrayElementAtIndex(1).vector2Value = new Vector2(0, 2);
            motion.FindProperty("speed").floatValue = .5f;
            motion.FindProperty("cyclic").boolValue = true;
            motion.ApplyModifiedPropertiesWithoutUndo();
            Belt(moving, QuantumConveyorDirection.Clockwise, "", 0);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out bool saved);
            if (!saved) throw new InvalidOperationException("벨트 시험 프리팹 저장 실패");
        }
        finally
        {
            SceneManager.SetActiveScene(previous);
            EditorSceneManager.CloseScene(scene, true);
        }
        QuantumOfficialStageWriter.Write(PrefabPath);
    }
}
