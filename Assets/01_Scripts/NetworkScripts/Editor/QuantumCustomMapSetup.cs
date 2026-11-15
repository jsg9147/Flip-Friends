using System;
using System.IO;
using FlipFriends.Maps;
using FlipFriends.Network;
using Quantum;
using Quantum.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class QuantumCustomMapSetup
{
    public const string ScenePath = "Assets/00_Scenes/QuantumCustomMapTest.unity";
    public const string SourcePath = "Assets/QuantumUser/Resources/FlipFriendsCustomMap.json";
    public const string ManifestPath = "Assets/QuantumUser/Resources/FlipFriendsCustomMapManifest.asset";
    public const string MapPath = "Assets/QuantumUser/Resources/FlipFriendsCustomMap.asset";

    [MenuItem("Tools/Flip Friends/Quantum/Create Custom Map Test Scene")]
    public static void Create()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Play를 종료한 뒤 생성하세요.");
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("저장되지 않은 씬이 있습니다.");
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null || File.Exists(SourcePath) ||
            AssetDatabase.LoadAssetAtPath<Quantum.Map>(MapPath) != null)
            throw new InvalidOperationException("기존 시험 자료를 덮어쓰지 않습니다.");
        var data = new MapData("Quantum 정적 지형 검증", "Flip Friends");
        Add(data, QuantumMapConverter.Spawn, -2, 0.75f, 1, 1);
        Add(data, QuantumMapConverter.Spawn, 2, 0.75f, 1, 1);
        Add(data, QuantumMapConverter.Finish, 4, 0.5f, 1, 1);
        Add(data, QuantumMapConverter.Ground, 0, -0.5f, 13, 1);
        Add(data, QuantumMapConverter.Ground, -6, 1.5f, 1, 3);
        Add(data, QuantumMapConverter.Ground, 6, 1.5f, 1, 3);
        Add(data, QuantumMapConverter.Ground, 0, 2.5f, -13, 1);
        Build(ScenePath, SourcePath, MapPath, ManifestPath, data);
    }

    public static void Build(string scenePath, string sourcePath, string mapPath, string manifestPath, MapData data)
    {
        if (EditorApplication.isPlaying || File.Exists(scenePath) || File.Exists(sourcePath) || File.Exists(mapPath) || File.Exists(manifestPath))
            throw new InvalidOperationException("정지 상태에서 새 경로로만 시험 자료를 생성합니다.");
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("저장되지 않은 씬이 있습니다.");
        var palette = AssetDatabase.LoadAssetAtPath<MapEditorPalette>(QuantumMapConverter.PaletteAssetPath);
        var conversion = QuantumMapConverter.Convert(data, palette);
        File.WriteAllText(sourcePath, JsonUtility.ToJson(data, true));
        AssetDatabase.ImportAsset(sourcePath);
        QuantumEditorMenuCreateScene.CreateNewQuantumScene(scenePath, mapPath, true, false, false);
        var debug = UnityEngine.Object.FindAnyObjectByType<QuantumRunnerLocalDebug>();
        var bootstrap = new GameObject("QuantumCustomMapBootstrap").AddComponent<QuantumTestBootstrap>();
        bootstrap.Configure(debug.RuntimeConfig);
        UnityEngine.Object.DestroyImmediate(debug.gameObject);
        foreach (var input in UnityEngine.Object.FindObjectsByType<QuantumDebugInput>(FindObjectsSortMode.None))
            UnityEngine.Object.DestroyImmediate(input.gameObject);
        int index = 0;
        foreach (var box in conversion.Boxes)
        {
            var root = new GameObject($"변환 지형 {index++:D3}");
            root.transform.position = new Vector3(box.Center.X.AsFloat, box.Center.Y.AsFloat, 0);
            root.AddComponent<QuantumStaticBoxCollider2D>().Size = box.Size;
            var view = GameObject.CreatePrimitive(PrimitiveType.Cube);
            view.name = "표시";
            view.transform.SetParent(root.transform, false);
            view.transform.localScale = new Vector3(box.Size.X.AsFloat, box.Size.Y.AsFloat, 1);
            UnityEngine.Object.DestroyImmediate(view.GetComponent<Collider>());
        }
        index = 0;
        foreach (var rope in conversion.Ropes)
        {
            var view = GameObject.CreatePrimitive(PrimitiveType.Cube);
            view.name = $"로프 표시 {index++:D3} (판정은 Quantum 설정)";
            view.transform.position = new Vector3(rope.Center.X.AsFloat, rope.Center.Y.AsFloat, 0);
            view.transform.localScale = new Vector3(rope.Size.X.AsFloat, rope.Size.Y.AsFloat, 0.15f);
            UnityEngine.Object.DestroyImmediate(view.GetComponent<Collider>());
        }
        var finish = GameObject.CreatePrimitive(PrimitiveType.Cube);
        finish.name = "도착점 표시 (클리어 판정 미이식)";
        finish.transform.position = new Vector3(4, 0.5f, 0);
        finish.transform.localScale = new Vector3(0.3f, 1, 0.3f);
        UnityEngine.Object.DestroyImmediate(finish.GetComponent<Collider>());
        var camera = Camera.main;
        camera.orthographic = true;
        camera.orthographicSize = 5;
        camera.transform.SetPositionAndRotation(new Vector3(0, 1, -20), Quaternion.identity);
        QuantumMapDataBaker.BakeMapData(UnityEngine.Object.FindAnyObjectByType<QuantumMapData>(), true);
        var manifest = ScriptableObject.CreateInstance<QuantumCustomMapAsset>();
        // SDK 씬 생성 중 미사용 에셋이 해제될 수 있어 저장 전에 팔레트를 다시 읽는다.
        palette = AssetDatabase.LoadAssetAtPath<MapEditorPalette>(QuantumMapConverter.PaletteAssetPath);
        manifest.Configure(AssetDatabase.LoadAssetAtPath<TextAsset>(sourcePath), palette,
            AssetDatabase.LoadAssetAtPath<Quantum.Map>(mapPath), conversion);
        AssetDatabase.CreateAsset(manifest, manifestPath);
        bootstrap.ConfigureCustomMap(manifest);
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), scenePath);
        AssetDatabase.SaveAssets();
        EditorSceneManager.OpenScene(scenePath);
        UnityEngine.Object.FindAnyObjectByType<QuantumTestBootstrap>().ValidateCustomMap();
        Debug.Log($"[QuantumMap] 저장·재로드 완료: {manifest.Agreement}, 정적 박스 {conversion.Boxes.Count}개");
    }

    private static void Add(MapData data, string id, float x, float y, float sx, float sy) =>
        data.objects.Add(new PlacedObjectData(id, new Vector3(x, y), 0, new Vector3(sx, sy, 1)));
}
