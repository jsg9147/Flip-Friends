using System;
using System.IO;
using FlipFriends.Maps;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using static QuantumOfficialTestStageSetup;

public static class QuantumSwitchSetup
{
    public const string StageId = "official-switch-test";
    public const string PrefabPath = "Assets/03_Prefabs/OfficialStages/Official Switch Test.prefab";

    public static void Create()
    {
        if (EditorApplication.isPlaying || File.Exists(PrefabPath))
            throw new InvalidOperationException("정지 상태의 새 스위치 시험 경로만 생성합니다.");
        var tile = FirstTile("Assets/03_Prefabs/Stage/Stage 1.prefab", "Ground");
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(QuantumLauncherSetup.SpritePath);
        var previous = SceneManager.GetActiveScene();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(scene);
        try
        {
            var root = new GameObject("Official Switch Test");
            var stage = new SerializedObject(root.AddComponent<QuantumOfficialStage>());
            stage.FindProperty("stageId").stringValue = StageId;
            stage.FindProperty("displayName").stringValue = "스위치·문 시험";
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
            for (int x = -12; x <= 16; x++) { Paint(map, tile, x, -2); Paint(map, tile, x, -1); }
            map.CompressBounds();
            Point(root, "1P 시작", -3, .55f).AddComponent<QuantumStageSpawnPoint>();
            Point(root, "2P 시작", -5, .55f).AddComponent<QuantumStageSpawnPoint>();
            Point(root, "상자", -7, .55f).AddComponent<QuantumStageBox>();
            var goal = new SerializedObject(Point(root, "도착", 11, 1).AddComponent<QuantumStageGoal>());
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

            QuantumStageTerrainToggle Terrain(string name, string group, float x, float y, Vector2 size, float interval, bool active)
            {
                var target = Part(name, x, y, size, false).AddComponent<QuantumStageTerrainToggle>();
                var serialized = new SerializedObject(target);
                serialized.FindProperty("group").stringValue = group;
                serialized.FindProperty("interval").floatValue = interval;
                serialized.FindProperty("initialActive").boolValue = active;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                return target;
            }

            void Button(string name, float x, float y, Vector2 size, QuantumStageTerrainToggle target, bool door)
            {
                var button = new SerializedObject(Part(name, x, y, size, door).AddComponent<QuantumStageSwitch>());
                button.FindProperty("target").objectReferenceValue = target;
                button.FindProperty("isDoor").boolValue = door;
                button.FindProperty("detectBoxes").boolValue = door;
                button.ApplyModifiedPropertiesWithoutUndo();
            }

            var manual = Terrain("수동 지형 A", "manual", 1, 1.75f, new Vector2(.5f, 3.5f), 0, true);
            Terrain("수동 지형 B", "manual", 1, 4, new Vector2(2, .5f), 0, false);
            Button("수동 버튼", -3, 1.6f, new Vector2(1.25f, .4f), manual, false);
            var door = Terrain("문", "door", 6, 1.75f, new Vector2(.5f, 3.5f), 0, true);
            Button("문 감지", 6, .35f, new Vector2(3, .7f), door, true);
            var mixed = Terrain("자동 수동 혼합 지형", "mixed", 0, 6, new Vector2(2, .5f), 3, true);
            Button("혼합 버튼", -9, 1.6f, new Vector2(1.25f, .4f), mixed, false);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out bool saved);
            if (!saved) throw new InvalidOperationException("스위치 시험 프리팹 저장 실패");
        }
        finally
        {
            SceneManager.SetActiveScene(previous);
            EditorSceneManager.CloseScene(scene, true);
        }
        QuantumOfficialStageWriter.Write(PrefabPath);
    }
}
