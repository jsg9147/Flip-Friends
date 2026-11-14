using System;
using System.Collections.Generic;
using System.IO;
using FlipFriends.Maps;
using Quantum;
using Quantum.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class QuantumDeviceSetup
{
    public const string ManifestPath = "Assets/QuantumUser/Resources/FlipFriendsDeviceMapManifest.asset";
    public const string SourcePath = "Assets/QuantumUser/Resources/FlipFriendsDeviceMap.json";
    public const string DevicesPath = "Assets/QuantumUser/Resources/FlipFriendsDeviceContent.asset";
    public const string ScenePath = "Assets/00_Scenes/QuantumDeviceTest.unity";
    private const string MapPath = "Assets/QuantumUser/Resources/FlipFriendsDeviceMap.asset";

    public static void Create()
    {
        if (EditorApplication.isPlaying || File.Exists(ManifestPath) || File.Exists(SourcePath) || File.Exists(ScenePath) || File.Exists(MapPath) || File.Exists(DevicesPath))
            throw new InvalidOperationException("정지 상태에서 별도 새 경로만 생성합니다.");
        var previous = SceneManager.GetActiveScene();
        var data = new MapData("Quantum 체크포인트·구간·소환", "Flip Friends");
        Add(data, QuantumMapConverter.Spawn, -8, 0.75f, 1, 1);
        Add(data, QuantumMapConverter.Spawn, -7, 0.75f, 1, 1);
        Add(data, QuantumMapConverter.Finish, 9, 0.5f, 1, 1);
        Add(data, QuantumMapConverter.Ground, 0, -0.5f, 22, 1);
        Add(data, QuantumMapConverter.Box, -3, 0.5f, 1, 1);
        Add(data, QuantumMapConverter.Rope, -10, 2, 1, 4);
        var palette = AssetDatabase.LoadAssetAtPath<MapEditorPalette>(QuantumMapConverter.PaletteAssetPath);
        var conversion = QuantumMapConverter.Convert(data, palette, true);
        var created = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(created);
        try
        {
            var map = QuantumEditorMenuCreateScene.SetupNewQuantumScene(MapPath, null);
            foreach (var box in conversion.Boxes)
            {
                var root = new GameObject("장치 검증 지형"); root.transform.position = new Vector3(box.Center.X.AsFloat, box.Center.Y.AsFloat, 0);
                root.AddComponent<QuantumStaticBoxCollider2D>().Size = box.Size;
            }
            var sources = new List<QuantumDeviceSource>();
            var referenceCheckpoint = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/03_Prefabs/Stage/Stage 15.prefab").GetComponentsInChildren<SavePoint>(true)[0];
            for (int i = 0; i < 2; i++)
            {
                var checkpoint = UnityEngine.Object.Instantiate(referenceCheckpoint.gameObject);
                checkpoint.name = "체크포인트 " + i; checkpoint.transform.SetParent(null);
                checkpoint.transform.position = new Vector3(i == 0 ? -5 : 1, 0.5f);
                checkpoint.GetComponent<SavePoint>().savePointID = i;
                sources.Add(Read(checkpoint, "checkpoint-" + i));
            }
            var referenceReset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/03_Prefabs/Stage/Stage 10.prefab").GetComponentsInChildren<RespawnHandler>(true)[0];
            for (int i = 0; i < 2; i++)
            {
                var reset = new GameObject(i == 0 ? "구간 복귀" : "상자 운반 구간 복귀");
                reset.transform.position = new Vector3(i == 0 ? 4 : 7, 1.7f);
                var collider = reset.AddComponent<BoxCollider2D>(); collider.isTrigger = true; collider.size = new Vector2(0.4f, 1);
                var renderer = reset.AddComponent<SpriteRenderer>(); renderer.sprite = referenceCheckpoint.GetComponent<SpriteRenderer>().sprite;
                renderer.color = Color.red;
                reset.tag = "Reset";
                var handler = reset.AddComponent<RespawnHandler>(); handler.targetLayers = referenceReset.targetLayers;
                handler.onlyBoxReset = i == 1;
                var destination = new GameObject("구간 목적지"); destination.transform.SetParent(reset.transform);
                destination.transform.position = new Vector3(i == 0 ? -1 : 5, 0.5f); handler.resetPoint = destination.transform;
                sources.Add(Read(reset, "section-" + i));
            }
            var summonPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/03_Prefabs/Obstacle/Box Switch Prefab.prefab");
            var summonRoot = UnityEngine.Object.Instantiate(summonPrefab);
            summonRoot.name = "상자 소환 장치"; summonRoot.transform.position = new Vector3(-9, 0.5f);
            var summon = summonRoot.GetComponentInChildren<ObjSummonSwitch>();
            summon.targetObjTrans.position = new Vector3(-3, 0.5f);
            summon.summonLocation.position = new Vector3(2.5f, 0.5f);
            sources.Add(Read(summon.gameObject, "summon-0"));
            var devices = ScriptableObject.CreateInstance<QuantumMapDevices>(); devices.Configure(sources.ToArray());
            devices.Apply(conversion);
            File.WriteAllText(SourcePath, JsonUtility.ToJson(data, true)); AssetDatabase.ImportAsset(SourcePath);
            AssetDatabase.CreateAsset(devices, DevicesPath);
            EditorSceneManager.SaveScene(created, ScenePath);
            QuantumMapDataBaker.BakeMapData(map, true);
            var manifest = ScriptableObject.CreateInstance<QuantumCustomMapAsset>(); manifest.ConfigureDevices(devices);
            manifest.Configure(AssetDatabase.LoadAssetAtPath<TextAsset>(SourcePath), palette, AssetDatabase.LoadAssetAtPath<Quantum.Map>(MapPath), conversion);
            AssetDatabase.CreateAsset(manifest, ManifestPath); AssetDatabase.SaveAssets(); EditorSceneManager.SaveScene(created);
        }
        finally { SceneManager.SetActiveScene(previous); EditorSceneManager.CloseScene(created, true); }
        var reloaded = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        EditorSceneManager.CloseScene(reloaded, true); SceneManager.SetActiveScene(previous);
        var verified = AssetDatabase.LoadAssetAtPath<QuantumCustomMapAsset>(ManifestPath).ValidateAndApply(new RuntimeConfig());
        if (verified.Version != 5 || verified.Devices.Count != 5) throw new InvalidOperationException("장치 맵 재로드 실패");
    }

    public static QuantumDeviceSource Read(GameObject root, string id)
    {
        int kinds = root.GetComponents<SavePoint>().Length + root.GetComponents<RespawnHandler>().Length + root.GetComponents<ObjSummonSwitch>().Length;
        if (!root.activeInHierarchy || kinds != 1)
            throw new InvalidOperationException($"장치 {id} 위치={root.transform.position}: 비활성 또는 중복/누락된 장치 컴포넌트입니다.");
        var colliders = root.GetComponentsInChildren<Collider2D>(true);
        if (colliders.Length != 1 || colliders[0] is not BoxCollider2D box || !box.enabled || !box.isTrigger || box.edgeRadius != 0 ||
            box.transform != root.transform || root.GetComponentsInChildren<Rigidbody2D>(true).Length != 0 ||
            box.usedByEffector || box.compositeOperation != Collider2D.CompositeOperation.None || root.transform.rotation != Quaternion.identity || root.transform.lossyScale != Vector3.one)
            throw new InvalidOperationException($"장치 {id} 위치={root.transform.position}: 단일 원본 크기의 축 정렬 박스 트리거만 지원합니다. Tilemap/다중/동적 판정은 미지원입니다.");
        var renderer = root.GetComponent<SpriteRenderer>();
        if (renderer == null || renderer.sprite == null || !renderer.enabled) throw new InvalidOperationException($"장치 {id}: 활성 렌더러 누락");
        var source = new QuantumDeviceSource { id = id, center = (Vector2)root.transform.position + box.offset, size = box.size,
            renderCenter = renderer.bounds.center, renderSize = renderer.bounds.size, defaultSprite = renderer.sprite, detectPlayers = true };
        if (root.TryGetComponent<SavePoint>(out var checkpoint))
        { source.kind = 0; source.checkpointId = checkpoint.savePointID; source.destination = root.transform.position; }
        else if (root.TryGetComponent<RespawnHandler>(out var reset))
        {
            if (reset.resetPoint == null) throw new InvalidOperationException($"장치 {id}: resetPoint 누락");
            int player = 1 << UnityEngine.LayerMask.NameToLayer("Player"), pickable = 1 << UnityEngine.LayerMask.NameToLayer("Pickable");
            if ((reset.targetLayers.value & ~(player | pickable)) != 0) throw new InvalidOperationException($"장치 {id}: 미지원 Reset 대상 마스크");
            source.kind = 1; source.onlyBoxReset = reset.onlyBoxReset; source.destination = reset.resetPoint.position;
            source.detectPlayers = (reset.targetLayers.value & player) != 0;
            source.detectBoxes = reset.CompareTag("Reset") || (reset.targetLayers.value & pickable) != 0;
        }
        else if (root.TryGetComponent<ObjSummonSwitch>(out var summon))
        {
            if (summon.targetObjTrans == null || summon.targetObjTrans.GetComponent<PickupObj>() == null || summon.summonLocation == null)
                throw new InvalidOperationException($"장치 {id}: 소환 상자/목적지 연결 누락");
            source.kind = 2; source.destination = summon.summonLocation.position; source.targetBoxHome = summon.targetObjTrans.position;
            source.cooldownSeconds = summon.cooldownTime; source.defaultSprite = summon.defaultSprite; source.pressedSprite = summon.pressedSprite;
        }
        else throw new InvalidOperationException($"장치 {id}: 미지원 컴포넌트");
        return source;
    }

    private static void Add(MapData data, string id, float x, float y, float sx, float sy) =>
        data.objects.Add(new PlacedObjectData(id, new Vector3(x, y), 0, new Vector3(sx, sy, 1)));
}
