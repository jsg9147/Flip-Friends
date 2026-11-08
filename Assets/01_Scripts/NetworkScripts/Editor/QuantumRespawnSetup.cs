using System;
using FlipFriends.Maps;
using Quantum;
using Quantum.Editor;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class QuantumRespawnSetup
{
    public const string ManifestPath = "Assets/QuantumUser/Resources/FlipFriendsRespawnMapManifest.asset";
    public const string SourcePath = "Assets/QuantumUser/Resources/FlipFriendsRespawnMap.json";
    public const string ScenePath = "Assets/00_Scenes/QuantumRespawnTest.unity";
    public const string MapPath = "Assets/QuantumUser/Resources/FlipFriendsRespawnMap.asset";

    public static void Create()
    {
        var previous = SceneManager.GetActiveScene();
        if (EditorApplication.isPlaying || File.Exists(SourcePath) || File.Exists(ScenePath) || File.Exists(MapPath) || File.Exists(ManifestPath))
            throw new InvalidOperationException("정지 상태의 새 경로만 생성합니다.");
        var data = new MapData("Quantum 리셋·추락 복귀", "Flip Friends");
        Add(data, QuantumMapConverter.Spawn, -1, 0.75f, 1, 1);
        Add(data, QuantumMapConverter.Spawn, 0, 0.75f, 1, 1);
        Add(data, QuantumMapConverter.Finish, 6, 0.5f, 1, 1);
        Add(data, QuantumMapConverter.Ground, 0, -0.5f, 13, 1);


        Add(data, QuantumMapConverter.Ground, 0, 5.5f, 17, 1);
        Add(data, QuantumMapConverter.Rope, -5, 2, 1, 4);
        Add(data, QuantumMapConverter.Box, 3, 0.5f, 1, 1);
        var palette = AssetDatabase.LoadAssetAtPath<MapEditorPalette>("Assets/07_ScriptableObject/MapEditorPalette.asset");
        var conversion = QuantumMapConverter.Convert(data, palette, true);
        File.WriteAllText(SourcePath, JsonUtility.ToJson(data, true));
        AssetDatabase.ImportAsset(SourcePath);
        // 원래 씬은 열린 채 두어 미저장 변경과 Undo 상태를 보존한다.
        var created = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(created);
        try
        {
            var map = QuantumEditorMenuCreateScene.SetupNewQuantumScene(MapPath, null);
            foreach (var box in conversion.Boxes)
            {
                var root = new GameObject("복귀 검증 지형");
                root.transform.position = new Vector3(box.Center.X.AsFloat, box.Center.Y.AsFloat, 0);
                root.AddComponent<QuantumStaticBoxCollider2D>().Size = box.Size;
            }
            EditorSceneManager.SaveScene(created, ScenePath);
            QuantumMapDataBaker.BakeMapData(map, true);
            var manifest = ScriptableObject.CreateInstance<QuantumCustomMapAsset>();
            manifest.Configure(AssetDatabase.LoadAssetAtPath<TextAsset>(SourcePath), palette, AssetDatabase.LoadAssetAtPath<Quantum.Map>(MapPath), conversion);
            AssetDatabase.CreateAsset(manifest, ManifestPath);
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(created);
        }
        finally
        {
            SceneManager.SetActiveScene(previous);
            EditorSceneManager.CloseScene(created, true);
        }
        var reloaded = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        EditorSceneManager.CloseScene(reloaded, true);
        var config = new RuntimeConfig();
        var result = AssetDatabase.LoadAssetAtPath<QuantumCustomMapAsset>(ManifestPath).ValidateAndApply(config);
        if (result.MovableBoxes.Count != 1 || !config.CustomMapAgreement.StartsWith("4:4:")) throw new InvalidOperationException("새 상자 맵 재로드 검증 실패");
        SceneManager.SetActiveScene(previous);
    }

    private static void Add(MapData data, string id, float x, float y, float sx, float sy) =>
        data.objects.Add(new PlacedObjectData(id, new Vector3(x, y), 0, new Vector3(sx, sy, 1)));
}
