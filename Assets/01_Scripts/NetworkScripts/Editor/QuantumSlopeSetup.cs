using System;
using System.IO;
using FlipFriends.Maps;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

public static class QuantumSlopeSetup
{
    public const string PrefabPath = "Assets/03_Prefabs/OfficialStages/Official Slope Test.prefab";
    public const string StagePath = QuantumOfficialStageWriter.ContentRoot + "/official-slope-test/Stage.asset";

    public static void RebuildTiles()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("정지 상태에서만 타일을 갱신합니다.");
        Triangle(null, false); Triangle(null, true);
        var root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            BuildGround(root.transform.Find("Grid/Ground").GetComponent<Tilemap>());
            root.transform.Find("도착").position = new Vector3(25, 0.6f);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        QuantumOfficialStageWriter.Write(PrefabPath);
    }

    public static void Create()
    {
        if (EditorApplication.isPlaying || File.Exists(PrefabPath)) throw new InvalidOperationException("정지 상태의 새 경사 프리팹만 생성합니다.");
        var previous = SceneManager.GetActiveScene();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(scene);
        try
        {
            var root = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(QuantumOfficialTestStageSetup.PrefabPath));
            root.name = "Official Slope Test";
            var stage = new SerializedObject(root.GetComponent<QuantumOfficialStage>());
            stage.FindProperty("stageId").stringValue = "official-slope-test";
            stage.FindProperty("displayName").stringValue = "경사 시험";
            stage.ApplyModifiedPropertiesWithoutUndo();
            var ground = root.transform.Find("Grid/Ground").GetComponent<Tilemap>();
            TileBase full = null; Sprite sprite = null;
            foreach (var cell in ground.cellBounds.allPositionsWithin)
                if (ground.GetTile(cell) != null) { full = ground.GetTile(cell); sprite = ground.GetSprite(cell); break; }
            Triangle(sprite, false); Triangle(sprite, true); BuildGround(ground);
            for (int i = root.transform.childCount - 1; i >= 0; i--)
            {
                var child = root.transform.GetChild(i);
                if (child.name != "Grid") UnityEngine.Object.DestroyImmediate(child.gameObject);
            }
            UnityEngine.Object.DestroyImmediate(root.transform.Find("Grid/Pit Reset").gameObject);
            Point(root, "1P 시작", -4, 0.6f).AddComponent<QuantumStageSpawnPoint>();
            Point(root, "2P 시작", -2, 0.6f).AddComponent<QuantumStageSpawnPoint>();
            Point(root, "도착", 25, 0.6f).AddComponent<QuantumStageGoal>();
            Point(root, "상자", -5, 0.5f).AddComponent<QuantumStageBox>();
            ground.CompressBounds();
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out bool saved);
            if (!saved) throw new InvalidOperationException("경사 프리팹 저장 실패");
        }
        finally { SceneManager.SetActiveScene(previous); EditorSceneManager.CloseScene(scene, true); }
        QuantumOfficialStageWriter.Write(PrefabPath);
    }

    private static void BuildGround(Tilemap ground)
    {
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(QuantumOfficialTestStageSetup.PrefabPath).transform.Find("Grid/Ground").GetComponent<Tilemap>();
        TileBase full = null;
        foreach (var cell in source.cellBounds.allPositionsWithin) if (source.GetTile(cell) != null) { full = source.GetTile(cell); break; }
        var up = AssetDatabase.LoadAssetAtPath<Tile>("Assets/03_Prefabs/OfficialStages/SlopeUp.asset");
        var down = AssetDatabase.LoadAssetAtPath<Tile>("Assets/03_Prefabs/OfficialStages/SlopeDown.asset");
        ground.ClearAllTiles();
        for (int x = -6; x < 30; x++)
        {
            int height = x < 0 || x >= 20 ? 0 : x < 8 ? x / 2 : x < 12 ? 4 : (19 - x) / 2;
            for (int y = -3; y < height; y++) ground.SetTile(new Vector3Int(x, y), full);
            if (x >= 0 && x < 8 && x % 2 == 0 || x >= 12 && x < 20 && x % 2 == 0)
            {
                var cell = new Vector3Int(x, height); ground.SetTile(cell, x < 8 ? up : down);
                ground.SetTileFlags(cell, TileFlags.None);
                ground.SetTransformMatrix(cell, Matrix4x4.TRS(new Vector3(0.5f, 0), Quaternion.identity, new Vector3(2, 1, 1)));
            }
        }
        ground.CompressBounds();
    }

    private static Tile Triangle(Sprite reference, bool down)
    {
        var tile = ScriptableObject.CreateInstance<Tile>();
        var vertices = down ? new[] { new Vector2(-0.5f, -0.5f), new Vector2(0.5f, -0.5f), new Vector2(-0.5f, 0.5f) }
            : new[] { new Vector2(-0.5f, -0.5f), new Vector2(0.5f, -0.5f), new Vector2(0.5f, 0.5f) };
        string path = "Assets/03_Prefabs/OfficialStages/Slope" + (down ? "Down" : "Up") + ".asset";
        string texturePath = path.Replace(".asset", ".png");
        var texture = new Texture2D(32, 32);
        for (int y = 0; y < 32; y++) for (int x = 0; x < 32; x++)
            texture.SetPixel(x, y, (down ? x + y <= 31 : y <= x) ? new Color(0.35f, 0.7f, 0.5f) : Color.clear);
        File.WriteAllBytes(texturePath, texture.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(texturePath);
        var importer = (TextureImporter)AssetImporter.GetAtPath(texturePath);
        importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 32; importer.alphaIsTransparency = true; importer.SaveAndReimport();
        // Sprite.OverridePhysicsShape의 런타임 데이터 대신 importer의 물리 윤곽을 저장해야 재로드 뒤에도 삼각형이다.
        var factories = new UnityEditor.U2D.Sprites.SpriteDataProviderFactories(); factories.Init();
        var provider = factories.GetSpriteEditorDataProviderFromObject(importer); provider.InitSpriteEditorDataProvider();
        var outline = provider.GetDataProvider<UnityEditor.U2D.Sprites.ISpritePhysicsOutlineDataProvider>();
        var rect = provider.GetSpriteRects()[0];
        outline.SetOutlines(rect.spriteID, new System.Collections.Generic.List<Vector2[]> { Array.ConvertAll(vertices, v => v * 32) });
        provider.Apply(); importer.SaveAndReimport();
        tile.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(texturePath); tile.colliderType = Tile.ColliderType.Sprite;
        var existing = AssetDatabase.LoadAssetAtPath<Tile>(path);
        if (existing == null) AssetDatabase.CreateAsset(tile, path);
        else { EditorUtility.CopySerialized(tile, existing); EditorUtility.SetDirty(existing); UnityEngine.Object.DestroyImmediate(tile); tile = existing; }
        AssetDatabase.SaveAssets(); return tile;
    }

    private static GameObject Point(GameObject root, string name, float x, float y)
    {
        var point = new GameObject(name); point.transform.SetParent(root.transform); point.transform.position = new Vector3(x, y);
        return point;
    }
}
