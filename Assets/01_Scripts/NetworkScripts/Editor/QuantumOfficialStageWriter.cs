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
        typeof(QuantumStageMotion), typeof(QuantumStageMotionReset),
        typeof(QuantumStageTerrainToggle), typeof(QuantumStageSwitch), typeof(QuantumStageConveyor),
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
            bool isMotion = collider.GetComponent<QuantumStageMotion>() != null || collider.GetComponent<QuantumStageTerrainToggle>() != null || collider.GetComponent<QuantumStageConveyor>() != null;
            bool isMotionReset = collider.GetComponent<QuantumStageMotionReset>() != null;
            bool isSwitch = collider.GetComponent<QuantumStageSwitch>() != null;
            if (isSwitch && (isGround || isReset || isTrap || isLauncher || isMotion || isMotionReset))
                throw new InvalidOperationException($"{id}/{PathOf(collider.transform, root)} 위치={collider.transform.position}: 스위치에 다른 판정 역할을 혼합할 수 없습니다.");
            if (isTrap && isLauncher) throw new InvalidOperationException($"{id}/{PathOf(collider.transform, root)}: 트랩과 스프링/바운스는 같은 오브젝트에 둘 수 없습니다.");
            if (isMotion && (isLauncher || isMotionReset) || isMotionReset && (isTrap || isLauncher)) throw new InvalidOperationException($"{id}/{PathOf(collider.transform, root)} 위치={collider.transform.position}: 이동/리셋/발사 장치를 혼합할 수 없습니다.");
            if (!isGround && !isReset && !isTrap && !isLauncher && !isMotion && !isMotionReset && !isSwitch)
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
            motions = Authoring<QuantumStageMotion>(root, id).Select(motion => ReadMotion(motion, root, id))
                .Concat(Authoring<QuantumStageTerrainToggle>(root, id).Select(toggle => ReadToggle(toggle, root, id)))
                .Concat(Authoring<QuantumStageConveyor>(root, id).Where(belt => belt.GetComponent<QuantumStageMotion>() == null)
                    .Select(belt => ReadFixedConveyor(belt, root, id))).ToArray(),
            conveyors = Authoring<QuantumStageConveyor>(root, id).Select(belt => ReadConveyor(belt, root, id)).ToArray(),
            motionResets = Authoring<QuantumStageMotionReset>(root, id).Select(button => ReadMotionReset(button, root, id)).ToArray(),
            switches = Authoring<QuantumStageSwitch>(root, id).Select(button => ReadSwitch(button, root, id)).ToArray(),
            renderBounds = bounds,
        };
        foreach (var motion in result.Data.motions)
        {
            float radius = (motion.renderCenter - motion.origin).magnitude + motion.renderSize.magnitude / 2;
            foreach (var offset in motion.waypoints ?? Array.Empty<Vector2>())
            {
                var center = motion.origin + offset;
                bounds.Encapsulate(new Vector3(center.x - radius, center.y - radius));
                bounds.Encapsulate(new Vector3(center.x + radius, center.y + radius));
            }
        }
        result.Data.renderBounds = bounds;

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

    private static QuantumMotionSource ReadMotion(QuantumStageMotion motion, GameObject root, string stageId)
    {
        string id = stageId + "/" + PathOf(motion.transform, root);
        var c = motion.GetComponent<BoxCollider2D>();
        var s = motion.Read(id, c != null ? c.size : Vector2.zero);
        var trap = motion.GetComponent<QuantumStageTrap>();
        if ((s.kind == 0) == (trap != null)) throw new InvalidOperationException($"{id} 위치={s.origin}: 발판에는 트랩 금지, 이동/회전 트랩에는 QuantumStageTrap 필요");
        if (s.kind == 0 && (motion.GetComponents<Collider2D>().Length != 1 || c == null || !c.enabled || c.isTrigger || c.offset != Vector2.zero || c.edgeRadius != 0 || c.usedByEffector || c.compositeOperation != Collider2D.CompositeOperation.None))
            throw new InvalidOperationException($"{id} 위치={s.origin}: 발판은 활성 단일 고체 Box·오프셋0·Effector/Composite 없음만 지원합니다.");
        var renderer = motion.GetComponent<SpriteRenderer>();
        if (renderer == null || renderer.sprite == null || !renderer.enabled) throw new InvalidOperationException($"{id} 위치={s.origin}: 활성 SpriteRenderer 필요");
        s.renderCenter = renderer.bounds.center; s.renderSize = renderer.bounds.size; s.sprite = renderer.sprite;
        return s;
    }

    // 원본 벨트는 오프셋(1.5,0)인 트리거 Box였지만 플레이어 레이에는 고체였다. 작성 경로는 오프셋0 고체 Box로 판정을 명시한다.
    private static QuantumConveyorSource ReadConveyor(QuantumStageConveyor belt, GameObject root, string stageId)
    {
        string id = stageId + "/" + PathOf(belt.transform, root);
        string at = $"{id} 위치={belt.transform.position}";
        var c = belt.GetComponent<BoxCollider2D>();
        if (belt.GetComponent<QuantumStageTerrainToggle>() != null || belt.GetComponent<QuantumStageTrap>() != null)
            throw new InvalidOperationException($"{at}: 벨트에는 전환 지형·트랩을 함께 둘 수 없습니다.");
        if (c == null || belt.GetComponents<Collider2D>().Length != 1 || !c.enabled || c.isTrigger || c.offset != Vector2.zero || c.edgeRadius != 0 || c.usedByEffector || c.compositeOperation != Collider2D.CompositeOperation.None)
            throw new InvalidOperationException($"{at}: 벨트는 오프셋0인 활성 단일 고체 Box·Effector/Composite 없음만 지원합니다.");
        var motion = belt.GetComponent<QuantumStageMotion>();
        if (motion != null && motion.Read(id, c.size).kind != 0) throw new InvalidOperationException($"{at}: 이동 벨트의 QuantumStageMotion은 Platform 종류여야 합니다.");
        return belt.Read(id);
    }

    private static QuantumMotionSource ReadFixedConveyor(QuantumStageConveyor belt, GameObject root, string stageId)
    {
        string id = stageId + "/" + PathOf(belt.transform, root);
        var c = belt.GetComponent<BoxCollider2D>();
        var r = belt.GetComponent<SpriteRenderer>();
        if (r == null || r.sprite == null || !r.enabled) throw new InvalidOperationException($"{id} 위치={belt.transform.position}: 활성 SpriteRenderer 필요");
        // 고정 벨트는 원본 Stage20 속도0 이동 벨트와 같은 정지 발판으로 변환한다.
        return new QuantumMotionSource { id = id, kind = 0, origin = belt.transform.position, size = c != null ? c.size : Vector2.zero,
            waypoints = new[] { Vector2.zero, Vector2.zero }, cyclic = true,
            renderCenter = r.bounds.center, renderSize = r.bounds.size, sprite = r.sprite };
    }

    private static QuantumMotionSource ReadToggle(QuantumStageTerrainToggle toggle, GameObject root, string stageId)
    {
        string id = stageId + "/" + PathOf(toggle.transform, root); var c = toggle.GetComponent<BoxCollider2D>();
        if (toggle.GetComponent<QuantumStageMotion>() != null || toggle.GetComponent<QuantumStageTrap>() != null || c == null || toggle.GetComponents<Collider2D>().Length != 1 || !c.enabled || c.isTrigger || c.offset != Vector2.zero || c.edgeRadius != 0 || c.usedByEffector || c.compositeOperation != Collider2D.CompositeOperation.None)
            throw new InvalidOperationException($"{id} 위치={toggle.transform.position}: 전환 지형은 단일 고체 Box·오프셋0·다른 장치 없음 필요");
        var r = toggle.GetComponent<SpriteRenderer>();
        if (r == null || r.sprite == null || !r.enabled) throw new InvalidOperationException($"{id} 위치={toggle.transform.position}: 활성 SpriteRenderer 필요");
        var s = toggle.Read(id, c.size); s.renderCenter = r.bounds.center; s.renderSize = r.bounds.size; s.sprite = r.sprite; return s;
    }

    private static QuantumMotionResetSource ReadMotionReset(QuantumStageMotionReset button, GameObject root, string stageId)
    {
        string id = stageId + "/" + PathOf(button.transform, root);
        var c = button.GetComponent<BoxCollider2D>();
        if (button.Target == null || !button.Target.transform.IsChildOf(root.transform) || c == null || !c.enabled || button.GetComponents<Collider2D>().Length != 1 || c.offset != Vector2.zero || c.edgeRadius != 0 || c.usedByEffector || c.compositeOperation != Collider2D.CompositeOperation.None)
            throw new InvalidOperationException($"{id} 위치={button.transform.position}: 이 스테이지의 발판 참조·단일 활성 Box·오프셋0 필요");
        return new QuantumMotionResetSource { id = id, target = stageId + "/" + PathOf(button.Target.transform, root), center = button.transform.position, size = c.size, isTrigger = c.isTrigger };
    }

    private static QuantumStageSwitchSource ReadSwitch(QuantumStageSwitch button, GameObject root, string stageId)
    {
        string id = stageId + "/" + PathOf(button.transform, root);
        string at = $"{id} 위치={button.transform.position}";
        var collider = button.GetComponent<BoxCollider2D>();
        var reference = button.Target != null ? (Component)button.Target : button.Conveyor;
        if ((button.Target == null) == (button.Conveyor == null) || !reference.transform.IsChildOf(root.transform))
            throw new InvalidOperationException($"{at}: 이 스테이지의 전환 지형 또는 그룹 벨트 중 하나만 참조해야 합니다.");
        if (button.Conveyor != null && string.IsNullOrEmpty(button.Conveyor.Group))
            throw new InvalidOperationException($"{at}: 참조한 벨트에 그룹이 없습니다.");
        if (collider == null || !collider.enabled || button.GetComponents<Collider2D>().Length != 1 ||
            collider.offset != Vector2.zero || collider.edgeRadius != 0 || collider.usedByEffector ||
            collider.compositeOperation != Collider2D.CompositeOperation.None)
            throw new InvalidOperationException($"{at}: 스위치는 오프셋0인 단일 활성 Box가 필요합니다.");
        var source = button.Read(id, collider.size);
        if (source.isDoor != collider.isTrigger)
            throw new InvalidOperationException($"{at}: 문은 트리거, OnOff는 고체 Box가 필요합니다.");
        return source;
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

    // 원형은 원본 이동 트랩 Variant에만 허용하고 정적 트랩의 작성 규칙은 유지한다.
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
        float radius = 0; Vector2 center = default;
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
        else if (collider is CircleCollider2D circle)
        {
            if (trap.GetComponent<QuantumStageMotion>() == null || circle.radius <= 0) throw new InvalidOperationException($"{at}: CircleCollider2D 트랩은 이동/회전 장치·반지름>0만 지원합니다.");
            radius = circle.radius; center = circle.transform.TransformPoint(circle.offset); local = Array.Empty<Vector2>();
        }
        else throw new InvalidOperationException($"{at}: 미지원 트랩 판정 {collider.GetType().Name}입니다. Box·볼록 Polygon·이동 원형만 지원합니다.");
        var renderer = trap.GetComponent<SpriteRenderer>();
        if (renderer == null || renderer.sprite == null || !renderer.enabled)
            throw new InvalidOperationException($"{at}: 트랩 표시와 추락 경계에 쓸 활성 SpriteRenderer가 필요합니다.");
        return new QuantumTrapSource { id = id, origin = trap.transform.position, direction = direction,
            radius = radius, center = center,
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
