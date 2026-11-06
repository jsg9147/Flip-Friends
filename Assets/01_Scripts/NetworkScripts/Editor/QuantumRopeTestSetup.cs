using System;
using FlipFriends.Maps;
using FlipFriends.Network;
using Quantum;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class QuantumRopeTestSetup
{
    public const string ScenePath = "Assets/00_Scenes/QuantumRopeTest.unity";
    public const string SourcePath = "Assets/QuantumUser/Resources/FlipFriendsRopeMap.json";
    public const string ManifestPath = "Assets/QuantumUser/Resources/FlipFriendsRopeMapManifest.asset";
    public const string MapPath = "Assets/QuantumUser/Resources/FlipFriendsRopeMap.asset";

    [MenuItem("Tools/Flip Friends/Quantum/Create Rope Test Scene")]
    public static void Create()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Play를 종료하세요.");
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("저장되지 않은 씬이 있습니다.");
        var data = new MapData("Quantum 로프 검증", "Flip Friends");
        Add(data, QuantumMapConverter.Spawn, -2, 0.75f, 1, 1);
        Add(data, QuantumMapConverter.Spawn, 2, 0.75f, 1, 1);
        Add(data, QuantumMapConverter.Finish, 4, 0.5f, 1, 1);
        Add(data, QuantumMapConverter.Ground, 0, -0.5f, 13, 1);
        Add(data, QuantumMapConverter.Ground, -6, 2.5f, 1, 5);
        Add(data, QuantumMapConverter.Ground, 6, 2.5f, 1, 5);
        Add(data, QuantumMapConverter.Ground, 0, 5.5f, -13, 1);
        Add(data, QuantumMapConverter.Rope, -2, 3, 1, 3);
        Add(data, QuantumMapConverter.Rope, 2, 3, 1, 3);
        // 벽·바닥·천장과 등반 이동의 충돌을 독립적으로 확인하는 긴 로프다.
        Add(data, QuantumMapConverter.Rope, -5.1f, 5, 1, 5);
        Add(data, QuantumMapConverter.Rope, 5.1f, 5, 1, 5);
        QuantumCustomMapSetup.Build(ScenePath, SourcePath, MapPath, ManifestPath, data);
    }

    [MenuItem("Tools/Flip Friends/Quantum/Upgrade Static Test Manifest")]
    public static void UpgradeStaticManifest()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Play를 종료하세요.");
        var bootstrap = UnityEngine.Object.FindAnyObjectByType<QuantumTestBootstrap>();
        if (SceneManager.GetActiveScene().path != QuantumCustomMapSetup.ScenePath || bootstrap == null)
            throw new InvalidOperationException("기존 정적 시험 씬을 열고 실행하세요.");
        var source = AssetDatabase.LoadAssetAtPath<TextAsset>(QuantumCustomMapSetup.SourcePath);
        var palette = AssetDatabase.LoadAssetAtPath<MapEditorPalette>("Assets/07_ScriptableObject/MapEditorPalette.asset");
        var conversion = QuantumMapConverter.Convert(JsonUtility.FromJson<MapData>(source.text), palette);
        var manifest = AssetDatabase.LoadAssetAtPath<QuantumCustomMapAsset>(QuantumCustomMapSetup.ManifestPath);
        manifest.Configure(source, palette, AssetDatabase.LoadAssetAtPath<Quantum.Map>(QuantumCustomMapSetup.MapPath), conversion);
        EditorUtility.SetDirty(manifest);
        bootstrap.ConfigureCustomMap(manifest);
        EditorUtility.SetDirty(bootstrap);
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        EditorSceneManager.OpenScene(QuantumCustomMapSetup.ScenePath);
        UnityEngine.Object.FindAnyObjectByType<QuantumTestBootstrap>().ValidateCustomMap();
    }

    private static void Add(MapData data, string id, float x, float y, float sx, float sy) =>
        data.objects.Add(new PlacedObjectData(id, new Vector3(x, y), 0, new Vector3(sx, sy, 1)));
}
