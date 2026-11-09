using System;
using System.Collections.Generic;
using System.Linq;
using FlipFriends.Maps;
using FlipFriends.Network;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class QuantumProductSetup
{
    public const string MainPath = "Assets/00_Scenes/Main.unity";
    public const string PlayPath = "Assets/00_Scenes/QuantumPlay.unity";

    [MenuItem("Tools/Flip Friends/Quantum/Install Product Entry")]
    public static void Install()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Play를 종료하세요.");
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("미저장 씬이 있습니다.");
        EditorSceneManager.OpenScene(MainPath);
        if (UnityEngine.Object.FindAnyObjectByType<QuantumGameFlow>(FindObjectsInactive.Include) != null ||
            AssetDatabase.LoadAssetAtPath<SceneAsset>(PlayPath) != null)
            throw new InvalidOperationException("설치된 제품 경로를 덮어쓰지 않습니다.");
        var navigator = UnityEngine.Object.FindAnyObjectByType<ScreenNavigator>();
        var main = UnityEngine.Object.FindObjectsByType<UIScreen>(FindObjectsInactive.Include).Single(x => x.ScreenId == "main-menu");
        var font = AssetDatabase.FindAssets("Danjo t:TMP_FontAsset").Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<TMP_FontAsset>).FirstOrDefault(x => x != null);
        // 기존 플레이어의 그림만 재사용한다. Animator의 콜라이더 변경 및 Mirror 동작은 복제하지 않는다.
        var sprite = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/03_Prefabs/Player.prefab").GetComponent<SpriteRenderer>().sprite;
        var flow = new GameObject("QuantumGameFlow").AddComponent<QuantumGameFlow>();
        var flowData = new SerializedObject(flow);
        var maps = flowData.FindProperty("supportedMaps");
        maps.arraySize = 2;
        maps.GetArrayElementAtIndex(0).objectReferenceValue = AssetDatabase.LoadAssetAtPath<QuantumCustomMapAsset>(QuantumCustomMapSetup.ManifestPath);
        maps.GetArrayElementAtIndex(1).objectReferenceValue = AssetDatabase.LoadAssetAtPath<QuantumCustomMapAsset>(QuantumRopeTestSetup.ManifestPath);
        flowData.ApplyModifiedPropertiesWithoutUndo();
        var panel = Rect("Quantum 준비", main.transform.parent, Vector2.zero, new Vector2(1300, 780));
        panel.gameObject.AddComponent<Image>().color = new Color(0.06f, 0.16f, 0.19f, 0.98f);
        panel.gameObject.AddComponent<CanvasGroup>();
        Text("Quantum 지원 맵", panel, new Vector2(0, 320), new Vector2(1100, 65), font, 42);
        var staticButton = Button("정적 지형", "정적 맵 선택", panel, new Vector2(-270, 220), font);
        var ropeButton = Button("로프", "로프 맵 선택", panel, new Vector2(270, 220), font);
        var status = Text("준비 중", panel, new Vector2(0, 45), new Vector2(1150, 210), font, 28);
        var offline = Button("오프라인 2인 개발 실행", "오프라인 시작", panel, new Vector2(-270, -130), font);
        var online = Button("Steam 인증 온라인", "온라인 시작", panel, new Vector2(270, -130), font);
        var legacy = Button("기존 Steam 게임 (Mirror)", "기존 경로", panel, new Vector2(-270, -245), font);
        var cancel = Button("뒤로 / 연결 취소", "취소", panel, new Vector2(270, -245), font);
        var screen = panel.gameObject.AddComponent<QuantumPrepareScreen>();
        Set(screen, "screenId", "quantum-prepare");
        Set(screen, "navigator", navigator);
        Set(screen, "canvasGroup", panel.GetComponent<CanvasGroup>());
        Set(screen, "firstSelectedObject", staticButton.gameObject);
        Set(screen, "staticMapButton", staticButton);
        Set(screen, "ropeMapButton", ropeButton);
        Set(screen, "offlineButton", offline);
        Set(screen, "onlineButton", online);
        Set(screen, "legacyButton", legacy);
        Set(screen, "cancelButton", cancel);
        Set(screen, "status", status);
        panel.gameObject.AddComponent<ButtonSelectController>().tagetButtonList = new List<Selectable> { staticButton, ropeButton, offline, online, legacy, cancel };
        navigator.Register(screen);
        var start = main.GetComponentsInChildren<ScreenNavigationButton>(true).Single(x => x.name == "Start Button");
        Set(start, "targetScreenId", "quantum-prepare");
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var camera = new GameObject("Main Camera").AddComponent<Camera>();
        camera.tag = "MainCamera";
        camera.orthographic = true;
        camera.orthographicSize = 6;
        camera.transform.position = new Vector3(0, 2, -20);
        camera.backgroundColor = new Color(0.08f, 0.12f, 0.17f);
        camera.clearFlags = CameraClearFlags.SolidColor;
        var canvas = new GameObject("Quantum HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster)).GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvas.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1600, 900);
        var hud = Text("", canvas.transform, new Vector2(0, 340), new Vector2(1250, 100), font, 26);
        var back = Button("메뉴로 종료", "플레이 종료", canvas.transform, new Vector2(550, -360), font);
        new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        var view = new GameObject("QuantumGameView").AddComponent<QuantumGameView>();
        Set(view, "playerSprite", sprite);
        SetPresentation(view);
        Set(view, "status", hud);
        Set(view, "returnButton", back);
        EditorSceneManager.SaveScene(scene, PlayPath);
        var scenes = EditorBuildSettings.scenes.ToList();
        scenes.Add(new EditorBuildSettingsScene(PlayPath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
        EditorApplication.ExecuteMenuItem("File/Save Project");
        EditorSceneManager.OpenScene(PlayPath);
        if (UnityEngine.Object.FindAnyObjectByType<QuantumGameView>() == null) throw new InvalidOperationException("플레이 씬 재로드 실패");
        EditorSceneManager.OpenScene(MainPath);
        if (UnityEngine.Object.FindAnyObjectByType<QuantumGameFlow>() == null) throw new InvalidOperationException("Main 재로드 실패");
        Debug.Log("[Quantum 제품] Main/플레이 씬 저장·재로드 완료. 첫 빌드 씬=" + EditorBuildSettings.scenes[0].path);
    }

    public static void SetPresentation(QuantumGameView view)
    {
        var camera = view.gameObject.scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<Camera>(true)).Single(item => item.CompareTag("MainCamera"));
        if (camera.GetComponent<AudioListener>() == null) Undo.AddComponent<AudioListener>(camera.gameObject);
        const string animationPath = "Assets/02_Sprites/PlayerSprites/Animation/";
        const string soundPath = "Assets/08_Sound/Player/";
        var serialized = new SerializedObject(view);
        var clips = serialized.FindProperty("playerClips");
        string[] names = { "Idle", "Walk", "Jump", "Falling", "Climb", "Damaged", "Shrink" };
        clips.arraySize = names.Length;
        for (int i = 0; i < names.Length; i++)
            clips.GetArrayElementAtIndex(i).objectReferenceValue = AssetDatabase.LoadAssetAtPath<AnimationClip>(animationPath + names[i] + ".anim");
        serialized.FindProperty("jumpClip").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(soundPath + "Jump.wav");
        serialized.FindProperty("shrinkClip").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(soundPath + "Shrink.wav");
        serialized.FindProperty("finishClip").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(soundPath + "Enter.wav");
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }

    private static TMP_Text Text(string text, Transform parent, Vector2 position, Vector2 size, TMP_FontAsset font, float fontSize)
    {
        var label = Rect("안내", parent, position, size).gameObject.AddComponent<TextMeshProUGUI>();
        label.font = font;
        label.fontSize = fontSize;
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.white;
        label.text = text;
        label.raycastTarget = false;
        return label;
    }

    private static Button Button(string label, string name, Transform parent, Vector2 position, TMP_FontAsset font)
    {
        var rect = Rect(name, parent, position, new Vector2(500, 80));
        rect.gameObject.AddComponent<Image>().color = new Color(0.17f, 0.4f, 0.44f);
        var button = rect.gameObject.AddComponent<Button>();
        Text(label, rect, Vector2.zero, new Vector2(470, 70), font, 28);
        return button;
    }

    private static void Set(UnityEngine.Object target, string field, object value)
    {
        var serialized = new SerializedObject(target);
        var property = serialized.FindProperty(field);
        if (value is string text) property.stringValue = text;
        else property.objectReferenceValue = (UnityEngine.Object)value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
}
