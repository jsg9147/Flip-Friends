using System;
using FlipFriends.Network;
using Photon.Deterministic;
using Quantum;
using Quantum.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class QuantumTestSceneSetup
{
    public const string ScenePath = "Assets/00_Scenes/QuantumMovementTest.unity";
    private const string MapPath = "Assets/QuantumUser/Resources/FlipFriendsTestMap.asset";

    [MenuItem("Tools/Flip Friends/Quantum/Create Movement Test Scene")]
    public static void CreateScene()
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty)
                throw new InvalidOperationException("저장되지 않은 씬이 있습니다. 먼저 저장한 뒤 테스트 씬을 생성하세요.");
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
            throw new InvalidOperationException("Quantum 테스트 씬이 이미 있습니다. 기존 씬을 열어 사용하세요.");

        QuantumEditorMenuCreateScene.CreateNewQuantumScene(ScenePath, MapPath, true, false, false);
        var debug = UnityEngine.Object.FindAnyObjectByType<QuantumRunnerLocalDebug>();
        var bootstrap = new GameObject("QuantumTestBootstrap").AddComponent<QuantumTestBootstrap>();
        bootstrap.Configure(debug.RuntimeConfig);
        UnityEngine.Object.DestroyImmediate(debug.gameObject);
        AddBox("테스트 바닥", new Vector2(0, -0.5f), new Vector2(24, 1));
        AddBox("테스트 왼쪽 벽", new Vector2(-12, 3), new Vector2(1, 8));
        AddBox("테스트 오른쪽 벽", new Vector2(12, 3), new Vector2(1, 8));
        AddBox("테스트 발판", new Vector2(4, 1.5f), new Vector2(3, 0.4f));
        Camera.main.orthographic = true;
        Camera.main.orthographicSize = 7;
        Camera.main.transform.position = new Vector3(0, 3, -20);
        Camera.main.transform.rotation = Quaternion.identity;
        var map = UnityEngine.Object.FindAnyObjectByType<QuantumMapData>();
        QuantumMapDataBaker.BakeMapData(map, true);
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), ScenePath);
        AssetDatabase.SaveAssets();
        EditorSceneManager.OpenScene(ScenePath);
        if (UnityEngine.Object.FindAnyObjectByType<QuantumTestBootstrap>() == null)
            throw new InvalidOperationException("테스트 씬 재로드 검증이 실패했습니다.");
        Debug.Log("[QuantumTest] 테스트 씬 저장·재로드 완료. 1P A/D/Space, 2P 방향키/위 화살표.");
    }

    private static void AddBox(string name, Vector2 position, Vector2 size)
    {
        var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        box.name = name;
        box.transform.position = position;
        box.transform.localScale = new Vector3(size.x, size.y, 1);
        UnityEngine.Object.DestroyImmediate(box.GetComponent<Collider>());
        box.AddComponent<QuantumStaticBoxCollider2D>().Size = FPVector2.One;
    }
}
