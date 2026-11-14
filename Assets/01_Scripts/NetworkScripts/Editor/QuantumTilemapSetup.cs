using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FlipFriends.Maps;
using Quantum;
using Quantum.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

public static class QuantumTilemapSetup
{
    public const string ManifestPath = "Assets/QuantumUser/Resources/FlipFriendsTilemapMapManifest.asset";
    public const string ScenePath = "Assets/00_Scenes/QuantumTilemapTest.unity";
    private const string Root = "Assets/QuantumUser/Resources/FlipFriendsTilemap";

    public static void Create()
    {
        if (EditorApplication.isPlaying || File.Exists(ManifestPath) || File.Exists(ScenePath)) throw new InvalidOperationException("정지 상태의 새 경로만 생성합니다.");
        var previous = SceneManager.GetActiveScene();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive); SceneManager.SetActiveScene(scene);
        try
        {
            var stage1 = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/03_Prefabs/Stage/Stage 1.prefab"));
            var terrainObject = stage1.transform.Find("Ground").gameObject;
            var stage9 = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/03_Prefabs/Stage/Stage 9.prefab"));
            var reset = stage9.transform.Find("Grid/First ResetTile").GetComponent<RespawnHandler>();
            Physics2D.SyncTransforms();
            var terrain = ScriptableObject.CreateInstance<QuantumMapTerrain>();
            terrain.Configure(QuantumTilemapReader.ReadTerrain(terrainObject.GetComponent<TilemapCollider2D>(), "stage-01/Ground"), terrainObject.GetComponent<TilemapRenderer>().bounds,
                QuantumTilemapReader.TerrainSignature(terrainObject.GetComponent<TilemapCollider2D>(), "stage-01/Ground"));
            var devices = ScriptableObject.CreateInstance<QuantumMapDevices>(); devices.Configure(new[] { QuantumTilemapReader.ReadReset(reset, "stage-09/Grid/First ResetTile") });
            var data = new MapData("Quantum Tilemap 지형·다중 Reset 사용례", "Flip Friends");
            Add(data, QuantumMapConverter.Spawn, 10, -2.3f); Add(data, QuantumMapConverter.Spawn, 12, -2.3f);
            Add(data, QuantumMapConverter.Finish, -10, -2.4f); Add(data, QuantumMapConverter.Box, 14.5f, -2.4f);
            Add(data, QuantumMapConverter.Ground, -35, -20);
            var palette = AssetDatabase.LoadAssetAtPath<MapEditorPalette>(QuantumMapConverter.PaletteAssetPath);
            var conversion = QuantumMapConverter.Convert(data, palette, true); devices.Apply(conversion); terrain.Apply(conversion);
            // 원본 전체 스테이지를 지원하는 것처럼 저장하지 않고 선택한 사용례만 별도 작성 씬에 둔다.
            Prune(stage1.transform, new[] { terrainObject.transform });
            Prune(stage9.transform, new[] { reset.transform, reset.resetPoint });
            stage1.name = "Stage1 Ground 원본 사용례"; stage9.name = "Stage9 First ResetTile 원본 사용례";
            var map = QuantumEditorMenuCreateScene.SetupNewQuantumScene(Root + "Map.asset", null);
            foreach (var box in conversion.Boxes)
            { var go = new GameObject("기존 박스 베이크"); go.transform.position = new Vector3(box.Center.X.AsFloat, box.Center.Y.AsFloat); go.AddComponent<QuantumStaticBoxCollider2D>().Size = box.Size; }
            File.WriteAllText(Root + "Map.json", JsonUtility.ToJson(data, true)); AssetDatabase.ImportAsset(Root + "Map.json");
            AssetDatabase.CreateAsset(terrain, Root + "Terrain.asset"); AssetDatabase.CreateAsset(devices, Root + "Devices.asset");
            EditorSceneManager.SaveScene(scene, ScenePath); QuantumMapDataBaker.BakeMapData(map, true);
            var manifest = ScriptableObject.CreateInstance<QuantumCustomMapAsset>(); manifest.ConfigureDevices(devices); manifest.ConfigureTerrain(terrain);
            manifest.Configure(AssetDatabase.LoadAssetAtPath<TextAsset>(Root + "Map.json"), palette, AssetDatabase.LoadAssetAtPath<Quantum.Map>(Root + "Map.asset"), conversion);
            AssetDatabase.CreateAsset(manifest, ManifestPath); AssetDatabase.SaveAssets(); EditorSceneManager.SaveScene(scene);
        }
        finally { SceneManager.SetActiveScene(previous); EditorSceneManager.CloseScene(scene, true); }
        var reloaded = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive); EditorSceneManager.CloseScene(reloaded, true); SceneManager.SetActiveScene(previous);
        AssetDatabase.LoadAssetAtPath<QuantumCustomMapAsset>(ManifestPath).ValidateAndApply(new RuntimeConfig());
    }
    private static void Prune(Transform root, Transform[] keep)
    {
        foreach (Transform child in root.Cast<Transform>().ToArray())
        {
            if (!keep.Any(t => t == child || t.IsChildOf(child))) UnityEngine.Object.DestroyImmediate(child.gameObject);
            else if (!keep.Contains(child)) Prune(child, keep);
        }
    }

    public static void RefreshDerived()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("정지 상태에서 재검증합니다.");
        var previous = SceneManager.GetActiveScene(); var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        try
        {
            var roots = scene.GetRootGameObjects();
            var ground = roots.SelectMany(r => r.GetComponentsInChildren<TilemapCollider2D>()).Single(t => t.CompareTag("Ground"));
            var reset = roots.SelectMany(r => r.GetComponentsInChildren<RespawnHandler>()).Single();
            var terrain = AssetDatabase.LoadAssetAtPath<QuantumMapTerrain>(Root + "Terrain.asset");
            terrain.Configure(QuantumTilemapReader.ReadTerrain(ground, "stage-01/Ground"), ground.GetComponent<TilemapRenderer>().bounds,
                QuantumTilemapReader.TerrainSignature(ground, "stage-01/Ground"));
            var devices = AssetDatabase.LoadAssetAtPath<QuantumMapDevices>(Root + "Devices.asset"); devices.Configure(new[] { QuantumTilemapReader.ReadReset(reset, "stage-09/Grid/First ResetTile") });
            var source = AssetDatabase.LoadAssetAtPath<TextAsset>(Root + "Map.json"); var palette = AssetDatabase.LoadAssetAtPath<MapEditorPalette>(QuantumMapConverter.PaletteAssetPath);
            var conversion = QuantumMapConverter.Convert(JsonUtility.FromJson<MapData>(source.text), palette, true); devices.Apply(conversion); terrain.Apply(conversion);
            var manifest = AssetDatabase.LoadAssetAtPath<QuantumCustomMapAsset>(ManifestPath);
            manifest.Configure(source, palette, AssetDatabase.LoadAssetAtPath<Quantum.Map>(Root + "Map.asset"), conversion);
            EditorUtility.SetDirty(terrain); EditorUtility.SetDirty(devices); EditorUtility.SetDirty(manifest); AssetDatabase.SaveAssets();
            manifest.ValidateAndApply(new RuntimeConfig());
        }
        finally { SceneManager.SetActiveScene(previous); EditorSceneManager.CloseScene(scene, true); }
    }
    private static void Add(MapData data, string id, float x, float y) => data.objects.Add(new PlacedObjectData(id, new Vector3(x, y), 0, Vector3.one));
}
