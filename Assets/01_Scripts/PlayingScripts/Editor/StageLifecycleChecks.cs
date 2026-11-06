using System;
using System.Collections.Generic;
using System.Reflection;
using Mirror;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class StageLifecycleChecks
{
    private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;

    [MenuItem("Tools/Flip Friends/Stages/Check Catalog Palette And Server Clear")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || NetworkServer.active || NetworkClient.active ||
            UnityEngine.Object.FindObjectsByType<PlayerController2D>().Length != 0)
            throw new InvalidOperationException("플레이어와 네트워크가 없는 편집 모드에서 검사합니다.");
        var catalog = AssetDatabase.LoadAssetAtPath<BuiltInMapCatalog>("Assets/07_ScriptableObject/BuiltInMapCatalog.asset");
        Require(catalog != null && catalog.Count == 21, "카탈로그 21개");
        var prefabs = new HashSet<GameObject>();
        var ids = new HashSet<string>();
        var projectiles = new HashSet<GameObject>();
        int cannons = 0, moving = 0, rotating = 0, dynamic = 0;
        for (int index = 0; index < catalog.Count; index++)
        {
            var entry = catalog.GetAt(index);
            Require(entry != null && entry.StagePrefab != null && ids.Add(entry.MapId) && prefabs.Add(entry.StagePrefab), "카탈로그 ID/프리팹 중복 없음");
            Require(catalog.TryGet(entry.MapId, out var found) && found == entry, "문자열 ID 조회");
            GameObject root = PrefabUtility.LoadPrefabContents(AssetDatabase.GetAssetPath(entry.StagePrefab));
            try
            {
                Require(root.GetComponent<NetworkIdentity>() != null && root.GetComponentsInChildren<NetworkIdentity>(true).Length == 1, "루트 Identity 하나");
                Require(root.GetComponentsInChildren<NetworkBehaviour>(true).Length <= 64, "Mirror 부품 개수 상한");
                bool hasFinish = false;
                foreach (var collider in root.GetComponentsInChildren<Collider2D>(true))
                    if (collider.CompareTag("Finish") && collider.isTrigger) hasFinish = true;
                Require(hasFinish, "도착 트리거 " + entry.MapId);
                Require(FallBoundary.TryCreate(new[] { root }, out var boundary) && boundary.MinX < boundary.MaxX, "스테이지 경계 생성 " + entry.MapId);
                foreach (var cannon in root.GetComponentsInChildren<CannonShooter>(true))
                {
                    Require(cannon.TryValidateConfiguration(out var problem), "대포 설정 " + problem);
                    projectiles.Add(cannon.ProjectilePrefab);
                    cannons++;
                }
                moving += root.GetComponentsInChildren<MovingTrap>(true).Length;
                rotating += root.GetComponentsInChildren<RotatingObstacle>(true).Length;
                dynamic += root.GetComponentsInChildren<DynamicObj>(true).Length;
                var report = new StageReferenceAudit.Report();
                StageReferenceAudit.AuditRoot(root, report);
                Require(report.issues.Count == 0, "저장 프리팹 참조 검사 " + entry.MapId);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        var palette = AssetDatabase.LoadAssetAtPath<MapEditorPalette>("Assets/07_ScriptableObject/MapEditorPalette.asset");
        Require(palette != null, "팔레트 연결");
        foreach (var entry in palette.entries)
        {
            Require(entry.prefab != null && entry.prefab.GetComponent<NetworkIdentity>() != null, "팔레트 독립 Identity " + entry.id);
            foreach (var cannon in entry.prefab.GetComponentsInChildren<CannonShooter>(true))
            {
                Require(cannon.TryValidateConfiguration(out var problem), "팔레트 대포 설정 " + problem);
                projectiles.Add(cannon.ProjectilePrefab);
            }
        }
        CheckSceneBindings(catalog, palette, prefabs, projectiles);
        CheckServerClear();
        Debug.Log($"StageLifecycleChecks: PASS (21개 카탈로그/격리 생성·경계·도착 참조·정리, 대포 {cannons}/이동 {moving}/회전 {rotating}/배치 {dynamic}, 팔레트 {palette.entries.Count}, 씬·spawn 등록, 서버 완료 판정 1회). 실제 클리어와 씬 전환은 별도입니다.");
    }

    private static void CheckSceneBindings(BuiltInMapCatalog catalog, MapEditorPalette palette,
        HashSet<GameObject> prefabs, HashSet<GameObject> projectiles)
    {
        Scene previous = SceneManager.GetActiveScene();
        foreach (string path in new[] { "Assets/00_Scenes/Main.unity", "Assets/00_Scenes/GamePlay.unity", "Assets/00_Scenes/GameRoom.unity" })
        {
            Scene scene = SceneManager.GetSceneByPath(path);
            bool opened = !scene.IsValid() || !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenPreviewScene(path);
            try
            {
                bool found = false;
                foreach (var root in scene.GetRootGameObjects())
                {
                    if (path.EndsWith("Main.unity"))
                    {
                        foreach (var manager in root.GetComponentsInChildren<NetworkManager>(true))
                        {
                            if (!manager.gameObject.activeInHierarchy) continue;
                            foreach (var prefab in prefabs) Require(manager.spawnPrefabs.Contains(prefab), "기본 스테이지 spawn 등록 " + prefab.name);
                            foreach (var prefab in projectiles) Require(manager.spawnPrefabs.Contains(prefab), "투사체 spawn 등록");
                            foreach (var entry in palette.entries) Require(manager.spawnPrefabs.Contains(entry.prefab), "팔레트 spawn 등록 " + entry.id);
                            found = true;
                        }
                    }
                    else if (path.EndsWith("GamePlay.unity"))
                    {
                        foreach (var manager in root.GetComponentsInChildren<StageManager>(true))
                        {
                            var so = new SerializedObject(manager);
                            Require(so.FindProperty("builtInMapCatalog").objectReferenceValue == catalog && so.FindProperty("palette").objectReferenceValue == palette, "GamePlay 카탈로그/팔레트 연결");
                            Require(so.FindProperty("mapDataSyncPrefab").objectReferenceValue != null, "맵 동기화 프리팹 연결");
                            found = true;
                        }
                    }
                    else
                    {
                        foreach (var selection in root.GetComponentsInChildren<MapSelectionManager>(true))
                        {
                            Require(new SerializedObject(selection).FindProperty("builtInMapCatalog").objectReferenceValue == catalog, "GameRoom 카탈로그 연결");
                            found = true;
                        }
                    }
                }
                Require(found, "씬 담당 컴포넌트 " + path);
            }
            finally
            {
                if (opened) EditorSceneManager.ClosePreviewScene(scene);
                if (previous.IsValid()) SceneManager.SetActiveScene(previous);
            }
        }
    }

    private static void CheckServerClear()
    {
        Scene previous = SceneManager.GetActiveScene();
        Scene scene = default;
        NetworkManager previousManager = NetworkManager.singleton;
        GameManager previousGameManager = GameManager.Instance;
        var active = typeof(NetworkServer).GetProperty("active");
        try
        {
            scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            var roomObject = new GameObject("ServerClearRoomHarness");
            var room = roomObject.AddComponent<StageLifecycleTestRoomManager>();
            typeof(NetworkManager).GetProperty("singleton").SetValue(null, room);
            var gameObject = new GameObject("ServerClearCheck");
            var identity = gameObject.AddComponent<NetworkIdentity>();
            var game = gameObject.AddComponent<GameManager>();
            typeof(NetworkIdentity).GetMethod("InitializeNetworkBehaviours", Hidden).Invoke(identity, null);
            typeof(NetworkIdentity).GetProperty("isServer").SetValue(identity, true);
            active.SetValue(null, true);
            Require(room != null && game != null && game.netIdentity != null, "클리어 하네스 컴포넌트 연결 room=" + (room != null) + " game=" + (game != null) + " identity=" + (game != null && game.netIdentity != null));
            Require(UnityEngine.Object.FindObjectsByType<PlayerController2D>().Length == 0, "클리어 하네스 외부 플레이어 없음");
            game.FinishCheck();
            Require(room.ReturnCount == 0, "빈 게임은 클리어하지 않음");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/03_Prefabs/Player.prefab");
            var first = ((GameObject)PrefabUtility.InstantiatePrefab(prefab, scene)).GetComponent<PlayerController2D>();
            var second = ((GameObject)PrefabUtility.InstantiatePrefab(prefab, scene)).GetComponent<PlayerController2D>();
            typeof(PlayerController2D).GetField("isFinish").SetValue(first, true);
            game.FinishCheck();
            Require(room.ReturnCount == 0, "미도착자가 남으면 대기");
            typeof(PlayerController2D).GetField("isFinish").SetValue(second, true);
            game.FinishCheck();
            game.FinishCheck();
            Require(room.ReturnCount == 1 && !NetworkClient.active, "클라이언트 Command 없이 서버 복귀 1회");
        }
        finally
        {
            active.SetValue(null, false);
            if (scene.IsValid()) EditorSceneManager.CloseScene(scene, true);
            typeof(NetworkManager).GetProperty("singleton").SetValue(null, previousManager);
            GameManager.Instance = previousGameManager;
            if (previous.IsValid()) SceneManager.SetActiveScene(previous);
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("스테이지 수명 검사 실패: " + message);
    }
}
