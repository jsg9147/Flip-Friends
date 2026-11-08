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

// 공식 스테이지 프리팹 하나를 검증·베이크해 Quantum 콘텐츠와 공식 목록 등록까지 만든다.
public static class QuantumOfficialStageWriter
{
    public const string ContentRoot = "Assets/QuantumUser/Resources/OfficialStages";
    public const string SceneRoot = "Assets/00_Scenes/QuantumOfficialStages";
    public const string CatalogPath = "Assets/QuantumUser/Resources/" + QuantumOfficialStageCatalog.ResourceName + ".asset";

    private static readonly HashSet<Type> AllowedComponents = new()
    {
        typeof(Transform), typeof(Grid), typeof(Tilemap), typeof(TilemapRenderer), typeof(TilemapCollider2D), typeof(CompositeCollider2D),
        typeof(Rigidbody2D), typeof(SpriteRenderer), typeof(QuantumOfficialStage), typeof(QuantumStageSpawnPoint), typeof(QuantumStageGoal),
        typeof(QuantumStageBox), typeof(QuantumStageRope), typeof(QuantumStageReset), typeof(QuantumStageCheckpoint), typeof(QuantumStageBoxSpawner),
        typeof(QuantumStageTrap), typeof(QuantumStageLauncher), typeof(BoxCollider2D), typeof(PolygonCollider2D), typeof(CircleCollider2D),
    };

    public sealed class ReadResult
    {
        public string DisplayName;
        public QuantumOfficialStageData Data;
        public QuantumTerrainSource[] Terrain;
        public Bounds TerrainBounds;
        public string TerrainSignature;
        public QuantumDeviceSource[] Devices;
    }

    [MenuItem("Tools/Flip Friends/Quantum/선택한 공식 스테이지 변환")]
    private static void WriteSelected()
    {
        var paths = Selection.objects.OfType<GameObject>().Select(AssetDatabase.GetAssetPath).Where(p => p.EndsWith(".prefab")).ToArray();
        if (paths.Length == 0) { Debug.LogWarning("공식 스테이지 프리팹을 Project 창에서 선택하세요."); return; }
        foreach (var path in paths)
        {
            try { var stage = Write(path); Debug.Log($"공식 스테이지 변환 완료: {stage.StageId} 해시={stage.ContentHash}", stage); }
            catch (Exception error) { Debug.LogError($"공식 스테이지 변환 실패: {path}\n{error.Message}", AssetDatabase.LoadAssetAtPath<GameObject>(path)); }
        }
    }

    public static QuantumOfficialStageAsset Write(string prefabPath)
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("정지 상태에서만 공식 스테이지를 변환합니다.");
        var read = ReadPrefab(prefabPath);
        string id = read.Data.stageId, folder = ContentRoot + "/" + id;
        var catalog = LoadOrCreateCatalog();
        var existing = catalog.Stages.FirstOrDefault(s => s.StageId == id);
        string stagePath = folder + "/Stage.asset";
        if (existing != null && AssetDatabase.GetAssetPath(existing) != stagePath)
            throw new InvalidOperationException($"공식 스테이지 ID {id}가 다른 콘텐츠에 이미 등록되어 있습니다.");
        // 변환 전 검증을 끝내고 나서야 자산을 만든다. 실패한 프리팹이 부분 콘텐츠를 남기지 않게 한다.
        var terrain = ScriptableObject.CreateInstance<QuantumMapTerrain>();
        terrain.Configure(read.Terrain, read.TerrainBounds, read.TerrainSignature);
        var devices = read.Devices.Length == 0 ? null : ScriptableObject.CreateInstance<QuantumMapDevices>();
        devices?.Configure(read.Devices);
        QuantumOfficialStageConverter.Convert(read.Data, terrain, devices);

        Directory.CreateDirectory(folder); Directory.CreateDirectory(SceneRoot);
        terrain = Store(terrain, folder + "/Terrain.asset");
        devices = devices == null ? Remove<QuantumMapDevices>(folder + "/Devices.asset") : Store(devices, folder + "/Devices.asset");
        var map = BakeEmptyMap(folder + "/Map.asset", SceneRoot + "/" + id + ".unity");
        var stage = AssetDatabase.LoadAssetAtPath<QuantumOfficialStageAsset>(stagePath);
        if (stage == null) { stage = ScriptableObject.CreateInstance<QuantumOfficialStageAsset>(); AssetDatabase.CreateAsset(stage, stagePath); }
        stage.Configure(read.DisplayName, read.Data, map, terrain, devices);
        EditorUtility.SetDirty(stage);
        if (existing == null) { catalog.Configure(catalog.Stages.Append(stage).ToArray()); EditorUtility.SetDirty(catalog); }
        AssetDatabase.SaveAssets();
        stage.ValidateAndApply(new RuntimeConfig());
        return stage;
    }

    public static ReadResult ReadPrefab(string prefabPath)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null) throw new InvalidOperationException($"프리팹이 없습니다: {prefabPath}");
        var previous = SceneManager.GetActiveScene();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(scene);
        try
        {
            // Tilemap 자식만 만들면 Grid 조상이 없어 생성 형상이 달라지므로 프리팹 전체를 인스턴스화한다.
            var instance = UnityEngine.Object.Instantiate(prefab);
            Physics2D.SyncTransforms();
            return Read(instance);
        }
        finally { SceneManager.SetActiveScene(previous); EditorSceneManager.CloseScene(scene, true); }
    }

    public static ReadResult Read(GameObject root)
    {
        var stage = root.GetComponent<QuantumOfficialStage>();
        if (stage == null || root.GetComponentsInChildren<QuantumOfficialStage>(true).Length != 1)
            throw new InvalidOperationException($"{root.name}: 루트에 QuantumOfficialStage가 정확히 하나 있어야 합니다.");
        string id = stage.StageId;
        if (!QuantumOfficialStageConverter.IsValidStageId(id)) throw new InvalidOperationException($"{root.name}: 스테이지 ID '{id}'는 소문자·숫자·하이픈(1~48자)이어야 합니다.");
        if (string.IsNullOrWhiteSpace(stage.DisplayName)) throw new InvalidOperationException($"{id}: 표시 이름이 비어 있습니다.");
        ValidateComponents(root, id);

        var colliders = root.GetComponentsInChildren<Collider2D>(true);
        var grounds = colliders.OfType<TilemapCollider2D>().Where(c => c.CompareTag("Ground")).ToArray();
        if (grounds.Length != 1) throw new InvalidOperationException($"{id}: Ground 태그 Tilemap 지형이 정확히 하나 있어야 합니다(현재 {grounds.Length}).");
        var ground = grounds[0];
        var resets = Authoring<QuantumStageReset>(root, id);
        foreach (var collider in colliders)
        {
            bool isGround = collider.transform == ground.transform;
            bool isReset = collider is TilemapCollider2D && collider.GetComponent<QuantumStageReset>() != null;
            bool isTrap = collider.GetComponent<QuantumStageTrap>() != null;
            bool isLauncher = collider.GetComponent<QuantumStageLauncher>() != null;
            if (isTrap && isLauncher) throw new InvalidOperationException($"{id}/{PathOf(collider.transform, root)}: 트랩과 스프링/바운스는 같은 오브젝트에 둘 수 없습니다.");
            if (!isGround && !isReset && !isTrap && !isLauncher)
                throw new InvalidOperationException($"{id}/{PathOf(collider.transform, root)}: 지원하지 않는 판정입니다. Ground/QuantumStageReset Tilemap, QuantumStageTrap의 Box/Polygon, QuantumStageLauncher의 원/Box/Polygon만 Collider를 둡니다.");
        }
        foreach (var body in root.GetComponentsInChildren<Rigidbody2D>(true))
            if (body.transform != ground.transform && body.GetComponent<QuantumStageReset>() == null)
                throw new InvalidOperationException($"{id}/{PathOf(body.transform, root)}: Rigidbody2D는 Ground·Reset Tilemap에만 둘 수 있습니다.");

        var result = new ReadResult { DisplayName = stage.DisplayName, Devices = Array.Empty<QuantumDeviceSource>() };
        string groundId = id + "/" + PathOf(ground.transform, root);
        result.Terrain = QuantumTilemapReader.ReadTerrain(ground, groundId);
        result.TerrainBounds = ground.GetComponent<TilemapRenderer>().bounds;
        result.TerrainSignature = QuantumTilemapReader.TerrainSignature(ground, groundId);

        var boxes = Authoring<QuantumStageBox>(root, id);
        if (!FallBoundary.TryCalculateStageBounds(new[] { root }, out var bounds)) throw new InvalidOperationException($"{id}: 추락 경계를 계산할 렌더러가 없습니다.");
        result.Data = new QuantumOfficialStageData
        {
            stageId = id,
            spawns = Authoring<QuantumStageSpawnPoint>(root, id).Select(s => (Vector2)s.transform.position).ToArray(),
            boxes = boxes.Select(b => (Vector2)b.transform.position).ToArray(),
            goals = Authoring<QuantumStageGoal>(root, id).Select(g => new QuantumStageArea { center = g.transform.position, size = g.Size }).ToArray(),
            ropes = Authoring<QuantumStageRope>(root, id).Select(r => new QuantumStageArea { center = r.transform.position, size = r.Size }).ToArray(),
            traps = Authoring<QuantumStageTrap>(root, id).Select(trap => ReadTrap(trap, root, id, trap.KnockbackDirection)).ToArray(),
            launchers = Authoring<QuantumStageLauncher>(root, id).Select(launcher => ReadLauncher(launcher, root, id)).ToArray(),
            renderBounds = bounds,
        };

        var devices = new List<QuantumDeviceSource>();
        foreach (var checkpoint in Authoring<QuantumStageCheckpoint>(root, id))
        {
            var source = SpriteDevice(checkpoint, root, id);
            source.kind = 0; source.checkpointId = checkpoint.CheckpointId; source.size = checkpoint.Size; source.destination = checkpoint.transform.position;
            devices.Add(source);
        }
        foreach (var reset in resets) devices.Add(ReadReset(reset, root, id));
        foreach (var spawner in Authoring<QuantumStageBoxSpawner>(root, id))
        {
            string deviceId = id + "/" + PathOf(spawner.transform, root);
            if (spawner.Target == null || !boxes.Contains(spawner.Target) || spawner.Destination == null || !spawner.Destination.IsChildOf(root.transform))
                throw new InvalidOperationException($"{deviceId}: 소환 대상 상자와 목적지는 이 스테이지 안의 QuantumStageBox·Transform이어야 합니다.");
            var source = SpriteDevice(spawner, root, id);
            source.kind = 2; source.size = spawner.Size; source.destination = spawner.Destination.position; source.targetBoxHome = spawner.Target.transform.position;
            source.cooldownSeconds = spawner.CooldownSeconds; source.pressedSprite = spawner.PressedSprite;
            devices.Add(source);
        }
        result.Devices = devices.ToArray();
        return result;
    }

    private static QuantumDeviceSource ReadReset(QuantumStageReset reset, GameObject root, string stageId)
    {
        string id = stageId + "/" + PathOf(reset.transform, root);
        if (reset.Destination == null || !reset.Destination.IsChildOf(root.transform))
            throw new InvalidOperationException($"{id}: Reset 목적지는 이 스테이지 안의 Transform이어야 합니다.");
        var tile = reset.GetComponent<TilemapCollider2D>();
        if (tile != null)
            return QuantumTilemapReader.ReadTilemapReset(tile, reset.Destination, id, reset.DetectPlayers, reset.DetectBoxes, reset.OnlyBoxReset,
                $"공식Reset;대상={reset.DetectPlayers}/{reset.DetectBoxes};상자만={reset.OnlyBoxReset};목적지참조=");
        var source = SpriteDevice(reset, root, stageId);
        source.kind = 1; source.size = reset.Size; source.destination = reset.Destination.position;
        source.detectPlayers = reset.DetectPlayers; source.detectBoxes = reset.DetectBoxes; source.onlyBoxReset = reset.OnlyBoxReset;
        return source;
    }

    // 원본 정적 가시(트리거 Polygon)와 같은 형상만 받는다. 원·캡슐·Edge·Composite·다중 경로·움직임은 다음 묶음이라 거부한다.
    private static QuantumTrapSource ReadTrap(Component trap, GameObject root, string stageId, Vector2 direction)
    {
        string id = stageId + "/" + PathOf(trap.transform, root);
        var colliders = trap.GetComponents<Collider2D>();
        if (colliders.Length != 1) throw new InvalidOperationException($"{id} 위치={(Vector2)trap.transform.position}: 트랩 판정 Collider2D가 정확히 하나여야 합니다(현재 {colliders.Length}).");
        var collider = colliders[0];
        string at = $"{id} 위치={(Vector2)trap.transform.position}";
        if (!collider.enabled || !collider.isTrigger) throw new InvalidOperationException($"{at}: 트랩 판정은 활성 트리거여야 합니다.");
        if (collider.compositeOperation != Collider2D.CompositeOperation.None || collider.usedByEffector)
            throw new InvalidOperationException($"{at}: Composite·Effector에 쓰이는 트랩 판정은 지원하지 않습니다.");
        Vector2[] local;
        if (collider is BoxCollider2D box)
        {
            if (box.edgeRadius != 0 || box.size.x <= 0 || box.size.y <= 0) throw new InvalidOperationException($"{at}: Box 트랩은 크기>0·edgeRadius0만 지원합니다.");
            var h = box.size / 2;
            local = new[] { new Vector2(-h.x, -h.y), new Vector2(h.x, -h.y), new Vector2(h.x, h.y), new Vector2(-h.x, h.y) };
        }
        else if (collider is PolygonCollider2D polygon)
        {
            if (polygon.pathCount != 1) throw new InvalidOperationException($"{at}: Polygon 트랩은 경로 하나만 지원합니다(현재 {polygon.pathCount}).");
            local = polygon.GetPath(0);
        }
        else throw new InvalidOperationException($"{at}: 미지원 트랩 판정 {collider.GetType().Name}입니다. Box·볼록 Polygon만 지원합니다.");
        var renderer = trap.GetComponent<SpriteRenderer>();
        if (renderer == null || renderer.sprite == null || !renderer.enabled)
            throw new InvalidOperationException($"{at}: 트랩 표시와 추락 경계에 쓸 활성 SpriteRenderer가 필요합니다.");
        return new QuantumTrapSource { id = id, origin = trap.transform.position, direction = direction,
            vertices = local.Select(p => (Vector2)trap.transform.TransformPoint(p + collider.offset)).ToArray(),
            renderCenter = renderer.bounds.center, renderSize = renderer.bounds.size, sprite = renderer.sprite };
    }

    private static QuantumLauncherSource ReadLauncher(QuantumStageLauncher launcher, GameObject root, string stageId)
    {
        string id = stageId + "/" + PathOf(launcher.transform, root);
        var circle = launcher.GetComponent<CircleCollider2D>();
        if (circle == null)
        {
            var shape = ReadTrap(launcher, root, stageId, default);
            return new QuantumLauncherSource { id = id, kind = (int)launcher.Kind, origin = shape.origin, vertices = shape.vertices,
                renderCenter = shape.renderCenter, renderSize = shape.renderSize, sprite = shape.sprite };
        }
        if (launcher.GetComponents<Collider2D>().Length != 1) throw new InvalidOperationException($"{id}: 스프링/바운스 판정은 정확히 하나여야 합니다.");
        if (!circle.enabled || !circle.isTrigger || circle.radius <= 0 || circle.compositeOperation != Collider2D.CompositeOperation.None || circle.usedByEffector)
            throw new InvalidOperationException($"{id}: 원형 스프링/바운스는 활성 트리거·반지름>0·Composite/Effector 없음이어야 합니다.");
        var renderer = launcher.GetComponent<SpriteRenderer>();
        if (renderer == null || renderer.sprite == null || !renderer.enabled) throw new InvalidOperationException($"{id}: 활성 SpriteRenderer가 필요합니다.");
        return new QuantumLauncherSource { id = id, kind = (int)launcher.Kind, origin = launcher.transform.position,
            center = launcher.transform.TransformPoint(circle.offset), radius = circle.radius,
            renderCenter = renderer.bounds.center, renderSize = renderer.bounds.size, sprite = renderer.sprite };
    }

    private static QuantumDeviceSource SpriteDevice(Component device, GameObject root, string stageId)
    {
        string id = stageId + "/" + PathOf(device.transform, root);
        var renderer = device.GetComponent<SpriteRenderer>();
        if (renderer == null || renderer.sprite == null || !renderer.enabled)
            throw new InvalidOperationException($"{id}: 장치 표시와 추락 경계에 쓸 활성 SpriteRenderer가 필요합니다.");
        return new QuantumDeviceSource { id = id, center = device.transform.position, renderCenter = renderer.bounds.center, renderSize = renderer.bounds.size,
            defaultSprite = renderer.sprite, detectPlayers = true };
    }

    private static T[] Authoring<T>(GameObject root, string stageId) where T : Component
    {
        var items = root.GetComponentsInChildren<T>(true);
        foreach (var item in items)
        {
            string id = stageId + "/" + PathOf(item.transform, root);
            if (!item.gameObject.activeInHierarchy) throw new InvalidOperationException($"{id}: 비활성 오브젝트의 {typeof(T).Name}는 지원하지 않습니다.");
            QuantumTilemapReader.ValidatePlane(item.transform, id);
            // Tilemap 판정은 Grid 셀 형상을 그대로 쓰므로 배율 검사는 크기를 직접 두는 작성 컴포넌트에만 적용한다.
            if (item.GetComponent<TilemapCollider2D>() == null && (item.transform.eulerAngles.z != 0 || (Vector2)item.transform.lossyScale != Vector2.one))
                throw new InvalidOperationException($"{id}: {typeof(T).Name}는 회전0·배율1만 지원합니다. 크기는 컴포넌트 값으로 정합니다.");
        }
        return items;
    }

    private static void ValidateComponents(GameObject root, string stageId)
    {
        foreach (var component in root.GetComponentsInChildren<Component>(true))
        {
            if (component == null)
                throw new InvalidOperationException($"{stageId}: 누락된 스크립트가 있습니다.");
            if (!AllowedComponents.Contains(component.GetType()))
                throw new InvalidOperationException($"{stageId}/{PathOf(component.transform, root)}: 미지원 컴포넌트 {component.GetType().Name}입니다. Mirror·원본 장치 대신 Quantum 작성 컴포넌트를 쓰세요.");
        }
    }

    private static string PathOf(Transform transform, GameObject root) =>
        transform == root.transform ? root.name : AnimationUtility.CalculateTransformPath(transform, root.transform);

    private static T Store<T>(T value, string path) where T : ScriptableObject
    {
        var stored = AssetDatabase.LoadAssetAtPath<T>(path);
        if (stored == null) { AssetDatabase.CreateAsset(value, path); return value; }
        EditorUtility.CopySerialized(value, stored); UnityEngine.Object.DestroyImmediate(value); EditorUtility.SetDirty(stored);
        return stored;
    }

    private static T Remove<T>(string path) where T : ScriptableObject
    {
        if (AssetDatabase.LoadAssetAtPath<T>(path) != null) AssetDatabase.DeleteAsset(path);
        return null;
    }

    // 공식 지형은 FP 윤곽으로 전달하므로 SDK 맵에는 정적 Collider가 없다. SDK가 요구하는 맵 자산만 만든다.
    private static Quantum.Map BakeEmptyMap(string mapPath, string scenePath)
    {
        var previous = SceneManager.GetActiveScene();
        bool isNew = !File.Exists(scenePath);
        var scene = isNew ? EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive) : EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
        SceneManager.SetActiveScene(scene);
        try
        {
            var mapData = isNew ? QuantumEditorMenuCreateScene.SetupNewQuantumScene(mapPath, null)
                : scene.GetRootGameObjects().Select(r => r.GetComponentInChildren<QuantumMapData>()).Single(m => m != null);
            if (isNew) EditorSceneManager.SaveScene(scene, scenePath);
            QuantumMapDataBaker.BakeMapData(mapData, true);
            EditorSceneManager.SaveScene(scene);
        }
        finally { SceneManager.SetActiveScene(previous); EditorSceneManager.CloseScene(scene, true); }
        var map = AssetDatabase.LoadAssetAtPath<Quantum.Map>(mapPath);
        if (map == null) throw new InvalidOperationException($"Quantum 맵 자산을 만들지 못했습니다: {mapPath}");
        return map;
    }

    private static QuantumOfficialStageCatalog LoadOrCreateCatalog()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<QuantumOfficialStageCatalog>(CatalogPath);
        if (catalog != null) return catalog;
        catalog = ScriptableObject.CreateInstance<QuantumOfficialStageCatalog>();
        AssetDatabase.CreateAsset(catalog, CatalogPath);
        return catalog;
    }
}
