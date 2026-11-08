using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FlipFriends.Maps;
using Photon.Deterministic;
using Quantum;
using Quantum.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using LayerMask = UnityEngine.LayerMask;

public static class QuantumTilemapSetup
{
    public const string ManifestPath = "Assets/QuantumUser/Resources/FlipFriendsTilemapMapManifest.asset";
    public const string ScenePath = "Assets/00_Scenes/QuantumTilemapTest.unity";
    private const string Root = "Assets/QuantumUser/Resources/FlipFriendsTilemap";

    public static QuantumTerrainSource[] ReadTerrain(TilemapCollider2D tile, string id)
    {
        var composite = tile.GetComponent<CompositeCollider2D>();
        ValidatePlane(tile.transform, id);
        if (!tile.enabled || !tile.gameObject.activeInHierarchy || !tile.CompareTag("Ground") || tile.gameObject.layer != LayerMask.NameToLayer("Ground") ||
            tile.compositeOperation != Collider2D.CompositeOperation.Merge || composite == null || !composite.enabled || composite.isTrigger ||
            composite.geometryType != CompositeCollider2D.GeometryType.Outlines || composite.edgeRadius != 0 || tile.usedByEffector || composite.usedByEffector ||
            tile.GetComponentsInChildren<Collider2D>(true).Length != 2 || tile.attachedRigidbody == null || tile.attachedRigidbody.bodyType != RigidbodyType2D.Kinematic ||
            tile.GetComponentsInChildren<MonoBehaviour>().Length != 0)
            throw new InvalidOperationException($"지형 {id} 위치={tile.transform.position}: 정적 Ground Tilemap+Kinematic Composite Outline만 지원합니다.");
        tile.ProcessTilemapChanges(); composite.GenerateGeometry();
        var group = new PhysicsShapeGroup2D(); composite.GetShapes(group);
        var paths = new SortedDictionary<string, Vector2[]>(StringComparer.Ordinal);
        for (int i = 0; i < group.shapeCount; i++)
        {
            var shape = group.GetShape(i); var local = new List<Vector2>(); group.GetShapeVertices(i, local);
            if (shape.shapeType != PhysicsShapeType2D.Edges || shape.radius != 0 || local.Count < 4 || !local[0].Equals(local[^1]))
                throw new InvalidOperationException($"지형 {id} 조각{i}: 닫힌 반경0 윤곽이 아닙니다.");
            var points = local.Take(local.Count - 1).Select(v => (Vector2)group.localToWorldMatrix.MultiplyPoint3x4(v)).ToArray();
            var candidates = new List<Vector2[]>();
            for (int start = 0; start < points.Length; start++)
            foreach (int direction in new[] { -1, 1 })
                candidates.Add(Enumerable.Range(0, points.Length).Select(k => points[(start + direction * k + points.Length) % points.Length]).ToArray());
            var canonical = candidates.OrderBy(Key, StringComparer.Ordinal).First();
            if (!paths.TryAdd(Key(canonical), canonical)) throw new InvalidOperationException($"지형 {id}: 중복 윤곽입니다.");
        }
        return paths.Select(p => new QuantumTerrainSource { id = id + "/" + QuantumMapConverter.Hash(p.Key), vertices = p.Value }).ToArray();
    }

    public static QuantumDeviceSource ReadReset(RespawnHandler reset, string id)
    {
        ValidatePlane(reset.transform, id);
        var tile = reset.GetComponent<TilemapCollider2D>();
        if (tile == null || !tile.enabled || !tile.isTrigger || tile.compositeOperation != Collider2D.CompositeOperation.None || tile.usedByEffector ||
            !reset.CompareTag("Reset") || reset.resetPoint == null || reset.GetComponentsInChildren<Collider2D>(true).Length != 1 || !reset.gameObject.activeInHierarchy ||
            tile.attachedRigidbody != null && tile.attachedRigidbody.bodyType != RigidbodyType2D.Kinematic)
            throw new InvalidOperationException($"Reset {id} 위치={reset.transform.position}: 원본 정적 Tilemap 트리거/목적지를 확인하세요.");
        int player = 1 << LayerMask.NameToLayer("Player"), box = 1 << LayerMask.NameToLayer("Pickable");
        if ((reset.targetLayers.value & ~(player | box)) != 0) throw new InvalidOperationException($"Reset {id}: 미지원 마스크입니다.");
        tile.ProcessTilemapChanges(); var group = new PhysicsShapeGroup2D(); tile.GetShapes(group);
        if (group.shapeCount == 0 || group.shapeCount > 256) throw new InvalidOperationException($"Reset {id}: 조각 수 제한입니다.");
        var centers = new List<Vector2>(); var sizes = new List<Vector2>();
        for (int i = 0; i < group.shapeCount; i++)
        {
            var shape = group.GetShape(i); var v = new List<Vector2>(); group.GetShapeVertices(i, v);
            var points = v.Select(p => (Vector2)group.localToWorldMatrix.MultiplyPoint3x4(p)).ToArray();
            var min = new Vector2(points.Min(p => p.x), points.Min(p => p.y)); var max = new Vector2(points.Max(p => p.x), points.Max(p => p.y));
            if (shape.shapeType != PhysicsShapeType2D.Polygon || shape.radius != 0 || points.Length != 4 || points.Distinct().Count() != 4 ||
                points.Any(p => (p.x != min.x && p.x != max.x) || (p.y != min.y && p.y != max.y)))
                throw new InvalidOperationException($"Reset {id} 조각{i} 위치={min}: 축 정렬 사각 Polygon만 지원합니다.");
            centers.Add((min + max) / 2); sizes.Add(max - min);
        }
        var renderer = reset.GetComponent<TilemapRenderer>();
        if (renderer == null || !renderer.enabled) throw new InvalidOperationException($"Reset {id}: 렌더러 누락입니다.");
        return new QuantumDeviceSource { id = id, kind = 1, center = centers[0], size = sizes[0], contactCenters = centers.ToArray(), contactSizes = sizes.ToArray(),
            destination = reset.resetPoint.position, detectPlayers = (reset.targetLayers.value & player) != 0, detectBoxes = true,
            onlyBoxReset = reset.onlyBoxReset, renderCenter = renderer.bounds.center, renderSize = renderer.bounds.size,
            authoringSignature = FormattableString.Invariant($"{reset.targetLayers.value};Reset;Trigger;None;목적지참조=") +
                AnimationUtility.CalculateTransformPath(reset.resetPoint, reset.transform.root) + ";" + Signature(reset.GetComponent<Tilemap>()) };
    }

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
            terrain.Configure(ReadTerrain(terrainObject.GetComponent<TilemapCollider2D>(), "stage-01/Ground"), terrainObject.GetComponent<TilemapRenderer>().bounds,
                "stage-01/Ground;Merge;Outlines;Kinematic;Ground;반경0;" + Signature(terrainObject.GetComponent<Tilemap>()));
            var devices = ScriptableObject.CreateInstance<QuantumMapDevices>(); devices.Configure(new[] { ReadReset(reset, "stage-09/Grid/First ResetTile") });
            var data = new MapData("Quantum Tilemap 지형·다중 Reset 사용례", "Flip Friends");
            Add(data, QuantumMapConverter.Spawn, 10, -2.3f); Add(data, QuantumMapConverter.Spawn, 12, -2.3f);
            Add(data, QuantumMapConverter.Finish, -10, -2.4f); Add(data, QuantumMapConverter.Box, 14.5f, -2.4f);
            Add(data, QuantumMapConverter.Ground, -35, -20);
            var palette = AssetDatabase.LoadAssetAtPath<MapEditorPalette>("Assets/07_ScriptableObject/MapEditorPalette.asset");
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
    private static string Key(Vector2[] points) => string.Join(";", points.Select(p => FormattableString.Invariant($"{QuantumMapConverter.Q(p.x).RawValue},{QuantumMapConverter.Q(p.y).RawValue}")));
    private static void Prune(Transform root, Transform[] keep)
    {
        foreach (Transform child in root.Cast<Transform>().ToArray())
        {
            if (!keep.Any(t => t == child || t.IsChildOf(child))) UnityEngine.Object.DestroyImmediate(child.gameObject);
            else if (!keep.Contains(child)) Prune(child, keep);
        }
    }
    private static string Signature(Tilemap tilemap)
    {
        var values = new List<string>();
        foreach (var cell in tilemap.cellBounds.allPositionsWithin)
        {
            var tile = tilemap.GetTile(cell); if (tile == null) continue;
            values.Add(FormattableString.Invariant($"{cell.x},{cell.y},{cell.z}:") + AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(tile)) + ":" + tilemap.GetColliderType(cell));
        }
        var matrix = tilemap.transform.localToWorldMatrix;
        var tileCollider = tilemap.GetComponent<TilemapCollider2D>(); var composite = tilemap.GetComponent<CompositeCollider2D>();
        string settings = FormattableString.Invariant($"오프셋={tileCollider.offset.x:R},{tileCollider.offset.y:R};합성={tileCollider.compositeOperation};돌출={tileCollider.extrusionFactor:R};");
        if (composite != null) settings += FormattableString.Invariant($"윤곽={composite.geometryType};꼭짓점거리={composite.vertexDistance:R};오프셋거리={composite.offsetDistance:R};반경={composite.edgeRadius:R};");
        return QuantumMapConverter.Hash(settings + string.Join(",", Enumerable.Range(0, 16).Select(i => QuantumMapConverter.Q(matrix[i]).RawValue.ToString(System.Globalization.CultureInfo.InvariantCulture))) +
            ";" + string.Join(";", values.OrderBy(v => v, StringComparer.Ordinal)));
    }
    private static void ValidatePlane(Transform transform, string id)
    {
        if (transform.position.z != 0 || transform.eulerAngles.x != 0 || transform.eulerAngles.y != 0 || transform.lossyScale.z != 1)
            throw new InvalidOperationException($"{id} 위치={transform.position}: XY 평면만 지원합니다.");
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
            terrain.Configure(ReadTerrain(ground, "stage-01/Ground"), ground.GetComponent<TilemapRenderer>().bounds,
                "stage-01/Ground;Merge;Outlines;Kinematic;Ground;반경0;" + Signature(ground.GetComponent<Tilemap>()));
            var devices = AssetDatabase.LoadAssetAtPath<QuantumMapDevices>(Root + "Devices.asset"); devices.Configure(new[] { ReadReset(reset, "stage-09/Grid/First ResetTile") });
            var source = AssetDatabase.LoadAssetAtPath<TextAsset>(Root + "Map.json"); var palette = AssetDatabase.LoadAssetAtPath<MapEditorPalette>("Assets/07_ScriptableObject/MapEditorPalette.asset");
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
