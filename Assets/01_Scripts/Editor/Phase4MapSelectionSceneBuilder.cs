#if UNITY_EDITOR
using TMPro;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static class Phase4MapSelectionSceneBuilder
{
    private const string MainScenePath = "Assets/00_Scenes/Main.unity";
    private const string GameRoomScenePath = "Assets/00_Scenes/GameRoom.unity";
    private const string PrefabPath = "Assets/03_Prefabs/UI/MapSelectionList.prefab";
    private const string CatalogPath = "Assets/07_ScriptableObject/OfficialMapCatalog.asset";
    private const string PalettePath = "Assets/07_ScriptableObject/MapEditorPalette.asset";
    private const string ItemPrefabPath = "Assets/03_Prefabs/UI/LoadListItem.prefab";

    public static void BuildAndValidate()
    {
        Build();
        OfficialMapCatalog catalog = AssetDatabase.LoadAssetAtPath<OfficialMapCatalog>(CatalogPath);
        if (catalog.Entries.Count != 21 || catalog.GetValidationErrors().Count != 0)
            throw new InvalidOperationException("공식맵 카탈로그 검증에 실패했습니다.");

        foreach (string path in new[] { MainScenePath, GameRoomScenePath, "Assets/00_Scenes/GamePlay.unity" })
        {
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            foreach (GameObject root in scene.GetRootGameObjects())
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(child.gameObject) != 0)
                    throw new InvalidOperationException($"누락 스크립트: {path}/{child.name}");
            }

            UnityEngine.Object owner = path == MainScenePath
                ? UnityEngine.Object.FindAnyObjectByType<SteamRoomManager>(FindObjectsInactive.Include)
                : path == GameRoomScenePath
                    ? UnityEngine.Object.FindAnyObjectByType<MapSelectionManager>(FindObjectsInactive.Include)
                    : UnityEngine.Object.FindAnyObjectByType<StageManager>(FindObjectsInactive.Include);
            if (owner == null || new SerializedObject(owner).FindProperty("officialMapCatalog").objectReferenceValue != catalog)
                throw new InvalidOperationException($"공식맵 카탈로그 연결 실패: {path}");
        }

        EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Single);
        foreach (SteamRoomManager manager in UnityEngine.Object.FindObjectsByType<SteamRoomManager>(FindObjectsInactive.Include))
        foreach (OfficialMapEntry entry in catalog.Entries)
        {
            if (!manager.spawnPrefabs.Contains(entry.StagePrefab))
                throw new InvalidOperationException($"스폰 목록에 공식맵이 없습니다: {entry.MapId}");
        }
        CaptureHostPreview(catalog);
        Debug.Log("INTEGRATION_VALIDATION_OK: 카탈로그 21개, 3개 씬 스크립트·참조·스폰 목록 검증 통과");
    }

    private static void CaptureHostPreview(OfficialMapCatalog catalog)
    {
        // 미리보기는 사용자의 실제 맵과 완료 기록을 읽거나 마이그레이션하지 않는다.
        string validationDirectory = Path.GetFullPath(".git/preview-data");
        Directory.CreateDirectory(validationDirectory);
        PropertyInfo mapDirectory = typeof(MapDataRepository).GetProperty("SaveDirectoryOverride", BindingFlags.Static | BindingFlags.NonPublic);
        FieldInfo progress = typeof(MapCompletionProgress).GetField("repository", BindingFlags.Static | BindingFlags.NonPublic);
        object previousDirectory = mapDirectory.GetValue(null);
        object previousProgress = progress.GetValue(null);
        GameObject cameraObject = new GameObject("통합 검증 카메라");
        RenderTexture texture = new RenderTexture(1600, 900, 24);
        try
        {
            mapDirectory.SetValue(null, validationDirectory);
            progress.SetValue(null, new MapCompletionRepository(validationDirectory));
            HostRoomScreen host = UnityEngine.Object.FindAnyObjectByType<HostRoomScreen>(FindObjectsInactive.Include);
            foreach (UIScreen screen in UnityEngine.Object.FindObjectsByType<UIScreen>(FindObjectsInactive.Include))
                screen.gameObject.SetActive(false);
            for (Transform parent = host.transform; parent != null; parent = parent.parent)
                parent.gameObject.SetActive(true);
            host.Show();
            MapListUI list = host.GetComponentInChildren<MapListUI>(true);
            typeof(MapListUI).GetMethod("Start", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(list, null);
            host.SelectOfficialMap(catalog.Entries[0]);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.transform.position = new Vector3(0f, 0f, -100f);
            camera.targetTexture = texture;
            foreach (Canvas canvas in host.GetComponentsInParent<Canvas>(true))
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 1f;
            }
            Canvas.ForceUpdateCanvases();
            camera.Render();
            RenderTexture.active = texture;
            Texture2D image = new Texture2D(1600, 900, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0f, 0f, 1600f, 900f), 0, 0);
            image.Apply();
            File.WriteAllBytes(".git/host-map-selection.png", image.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(image);
        }
        finally
        {
            RenderTexture.active = null;
            UnityEngine.Object.DestroyImmediate(cameraObject);
            UnityEngine.Object.DestroyImmediate(texture);
            mapDirectory.SetValue(null, previousDirectory);
            progress.SetValue(null, previousProgress);
        }
    }

    [MenuItem("Flip Friends/Setup Phase 4 Map Selection UI")]
    public static void Build()
    {
        GameObject prefab = BuildPrefab();
        ConfigureMainScene(prefab);
        ConfigureGameRoomScene(prefab);
        ConfigureGameplayScene();
        AssetDatabase.SaveAssets();
        Debug.Log("4단계 맵 선택 UI 구성을 완료했습니다.");
    }

    private static GameObject BuildPrefab()
    {
        GameObject root = CreateUiObject("MapSelectionList");
        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.sizeDelta = new Vector2(760f, 700f);
        root.AddComponent<Image>().color = new Color(0.08f, 0.12f, 0.18f, 0.96f);

        Button officialButton = CreateButton(root.transform, "OfficialMapsButton", "공식맵", new Vector2(-190f, 310f));
        Button customButton = CreateButton(root.transform, "CustomMapsButton", "커스텀맵", new Vector2(190f, 310f));
        TMP_Text selectionText = CreateText(root.transform, "SelectionText", "선택된 맵 없음", 25f);
        SetRect(selectionText.rectTransform, new Vector2(0f, 250f), new Vector2(700f, 72f));

        GameObject scrollObject = CreateUiObject("MapScroll", root.transform);
        RectTransform scrollRectTransform = scrollObject.GetComponent<RectTransform>();
        SetRect(scrollRectTransform, new Vector2(0f, -5f), new Vector2(700f, 420f));
        scrollObject.AddComponent<Image>().color = new Color(0.03f, 0.05f, 0.08f, 0.75f);
        ScrollRect scrollRect = scrollObject.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;

        GameObject viewport = CreateUiObject("Viewport", scrollObject.transform);
        Stretch(viewport.GetComponent<RectTransform>(), 8f);
        viewport.AddComponent<Image>().color = Color.clear;
        viewport.AddComponent<RectMask2D>();

        GameObject content = CreateUiObject("Content", viewport.transform);
        RectTransform contentRect = content.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.sizeDelta = Vector2.zero;
        VerticalLayoutGroup layout = content.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 8f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        ContentSizeFitter fitter = content.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scrollRect.viewport = viewport.GetComponent<RectTransform>();
        scrollRect.content = contentRect;
        ScrollViewController scrollController = scrollObject.AddComponent<ScrollViewController>();
        SetComponentReference(scrollController, "scrollRect", scrollRect);
        SetComponentReference(scrollController, "content", contentRect);

        TMP_Text messageText = CreateText(root.transform, "MessageText", "맵을 선택하세요.", 20f);
        SetRect(messageText.rectTransform, new Vector2(0f, -250f), new Vector2(700f, 58f));
        Button startButton = CreateButton(root.transform, "StartButton", "게임 시작", new Vector2(0f, -315f));

        MapListUI presenter = root.AddComponent<MapListUI>();
        SerializedObject serializedPresenter = new SerializedObject(presenter);
        SetReference(serializedPresenter, "panel", root);
        SetReference(serializedPresenter, "listContainer", content.transform);
        SetReference(serializedPresenter, "listItemPrefab", AssetDatabase.LoadAssetAtPath<GameObject>(ItemPrefabPath));
        SetReference(serializedPresenter, "officialMapCatalog", AssetDatabase.LoadAssetAtPath<OfficialMapCatalog>(CatalogPath));
        SetReference(serializedPresenter, "mapEditorPalette", AssetDatabase.LoadAssetAtPath<MapEditorPalette>(PalettePath));
        SetReference(serializedPresenter, "messageText", messageText);
        SetReference(serializedPresenter, "officialMapsButton", officialButton);
        SetReference(serializedPresenter, "customMapsButton", customButton);
        SetReference(serializedPresenter, "startButton", startButton);
        serializedPresenter.ApplyModifiedPropertiesWithoutUndo();

        GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        return savedPrefab;
    }

    private static void ConfigureMainScene(GameObject prefab)
    {
        var scene = EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Single);
        HostRoomScreen hostScreen = Object.FindFirstObjectByType<HostRoomScreen>(FindObjectsInactive.Include);
        GameObject instance = ReplaceSceneInstance(prefab, hostScreen.transform, new Vector2(300f, 0f));
        MapListUI presenter = instance.GetComponent<MapListUI>();
        SetComponentReference(presenter, "hostRoomScreen", hostScreen);

        SerializedObject serializedHost = new SerializedObject(hostScreen);
        SetReference(serializedHost, "selectedMapText", FindChild<TMP_Text>(instance, "SelectionText"));
        SetReference(serializedHost, "validationMessageText", FindChild<TMP_Text>(instance, "MessageText"));
        serializedHost.ApplyModifiedPropertiesWithoutUndo();
        string[] buttonNames = { "roomTypeButton", "maxPlayerCountButton", "createButton", "cancelButton" };
        float[] buttonPositions = { 150f, 10f, -160f, -270f };
        for (int i = 0; i < buttonNames.Length; i++)
        {
            Button button = serializedHost.FindProperty(buttonNames[i]).objectReferenceValue as Button;
            button.transform.SetParent(hostScreen.transform, false);
            RectTransform rect = button.GetComponent<RectTransform>();
            rect.pivot = new Vector2(0.5f, 0.5f);
            SetRect(rect, new Vector2(-500f, buttonPositions[i]), new Vector2(330f, 70f));
            TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
            label.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Font/Danjo-bold-Regular SDF.asset");
            label.fontSize = 28f;
            label.enableAutoSizing = false;
            label.color = Color.white;
            Stretch(label.rectTransform, 8f);
            button.colors = ColorBlock.defaultColorBlock;
            if (button.targetGraphic is Image image)
            {
                image.sprite = null;
                image.type = Image.Type.Simple;
                image.color = new Color(0.2f, 0.3f, 0.45f, 1f);
            }
        }
        Transform legacyOptions = hostScreen.transform.Find("Frame/Room Options");
        if (legacyOptions != null) legacyOptions.gameObject.SetActive(false);
        Transform oldLabels = hostScreen.transform.Find("MapHostLabels");
        if (oldLabels != null) Object.DestroyImmediate(oldLabels.gameObject);
        GameObject labels = CreateUiObject("MapHostLabels", hostScreen.transform);
        Stretch(labels.GetComponent<RectTransform>(), 0f);
        TMP_Text roomLabel = CreateText(labels.transform, "RoomVisibilityLabel", "공개 여부", 24f);
        roomLabel.color = new Color(0.2f, 0.3f, 0.45f);
        SetRect(roomLabel.rectTransform, new Vector2(-500f, 220f), new Vector2(330f, 50f));
        TMP_Text playerLabel = CreateText(labels.transform, "MaximumPlayersLabel", "최대 인원", 24f);
        playerLabel.color = roomLabel.color;
        SetRect(playerLabel.rectTransform, new Vector2(-500f, 80f), new Vector2(330f, 50f));
        ButtonSelectController selector = hostScreen.GetComponent<ButtonSelectController>();
        AddListButtons(selector, instance, false);
        foreach (SteamRoomManager manager in Object.FindObjectsByType<SteamRoomManager>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            SetComponentReference(manager, "officialMapCatalog", AssetDatabase.LoadAssetAtPath<OfficialMapCatalog>(CatalogPath));
        EditorSceneManager.SaveScene(scene);
    }

    private static void ConfigureGameRoomScene(GameObject prefab)
    {
        var scene = EditorSceneManager.OpenScene(GameRoomScenePath, OpenSceneMode.Single);
        MapSelectionManager manager = Object.FindFirstObjectByType<MapSelectionManager>(FindObjectsInactive.Include);
        MapListUI legacyPresenter = manager.GetComponent<MapListUI>();
        if (legacyPresenter != null)
            Object.DestroyImmediate(legacyPresenter);

        Transform parent = manager.transform;
        SerializedObject serializedManager = new SerializedObject(manager);
        GameObject screen = serializedManager.FindProperty("mapSelectScreen").objectReferenceValue as GameObject;
        if (screen != null)
            parent = screen.transform;

        foreach (Transform child in parent)
            child.gameObject.SetActive(false);

        Transform legacyPanel = parent.Find("Custom Map List Panel");
        if (legacyPanel != null)
            Object.DestroyImmediate(legacyPanel.gameObject);

        GameObject instance = ReplaceSceneInstance(prefab, parent, Vector2.zero);
        MapListUI presenter = instance.GetComponent<MapListUI>();
        SetComponentReference(presenter, "mapSelectionManager", manager);
        SetComponentReference(manager, "officialMapCatalog", AssetDatabase.LoadAssetAtPath<OfficialMapCatalog>(CatalogPath));
        ButtonSelectController selector = parent.GetComponent<ButtonSelectController>();
        if (selector == null) selector = parent.gameObject.AddComponent<ButtonSelectController>();
        selector.tagetButtonList = new System.Collections.Generic.List<Selectable>();
        AddListButtons(selector, instance, true);
        SerializedObject stageButtons = new SerializedObject(manager);
        StageSelectBtnEvent events = stageButtons.FindProperty("stageSelectBtnEvent").objectReferenceValue as StageSelectBtnEvent;
        if (events != null)
        {
            SerializedObject eventData = new SerializedObject(events);
            SerializedProperty elements = eventData.FindProperty("uiElements");
            elements.arraySize = selector.tagetButtonList.Count;
            for (int i = 0; i < elements.arraySize; i++) elements.GetArrayElementAtIndex(i).objectReferenceValue = selector.tagetButtonList[i];
            eventData.ApplyModifiedPropertiesWithoutUndo();
        }
        EditorSceneManager.SaveScene(scene);
    }

    private static void ConfigureGameplayScene()
    {
        var scene = EditorSceneManager.OpenScene("Assets/00_Scenes/GamePlay.unity", OpenSceneMode.Single);
        StageManager manager = Object.FindFirstObjectByType<StageManager>(FindObjectsInactive.Include);
        SetComponentReference(manager, "officialMapCatalog", AssetDatabase.LoadAssetAtPath<OfficialMapCatalog>(CatalogPath));
        EditorSceneManager.SaveScene(scene);
    }

    private static void AddListButtons(ButtonSelectController selector, GameObject instance, bool includeStart)
    {
        if (selector == null) return;
        foreach (Button button in instance.GetComponentsInChildren<Button>(true))
        {
            if (button.name == "StartButton" && !includeStart) continue;
            if (!selector.tagetButtonList.Contains(button)) selector.tagetButtonList.Add(button);
        }
    }

    private static GameObject ReplaceSceneInstance(GameObject prefab, Transform parent, Vector2 position)
    {
        Transform existing = parent.Find("MapSelectionList");
        if (existing != null)
            Object.DestroyImmediate(existing.gameObject);

        GameObject instance = PrefabUtility.InstantiatePrefab(prefab, parent) as GameObject;
        RectTransform rect = instance.GetComponent<RectTransform>();
        rect.anchoredPosition = position;
        return instance;
    }

    private static GameObject CreateUiObject(string name, Transform parent = null)
    {
        GameObject gameObject = new GameObject(name, typeof(RectTransform));
        gameObject.layer = LayerMask.NameToLayer("UI");
        if (parent != null)
            gameObject.transform.SetParent(parent, false);
        return gameObject;
    }

    private static Button CreateButton(Transform parent, string name, string label, Vector2 position)
    {
        GameObject gameObject = CreateUiObject(name, parent);
        SetRect(gameObject.GetComponent<RectTransform>(), position, new Vector2(330f, 56f));
        Image image = gameObject.AddComponent<Image>();
        image.color = new Color(0.2f, 0.3f, 0.45f, 1f);
        Button button = gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        TMP_Text text = CreateText(gameObject.transform, "Label", label, 22f);
        Stretch(text.rectTransform, 8f);
        return button;
    }

    private static TMP_Text CreateText(Transform parent, string name, string value, float fontSize)
    {
        GameObject gameObject = CreateUiObject(name, parent);
        TextMeshProUGUI text = gameObject.AddComponent<TextMeshProUGUI>();
        text.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Font/Danjo-bold-Regular SDF.asset");
        text.text = value;
        text.fontSize = fontSize;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        return text;
    }

    private static void SetRect(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private static void Stretch(RectTransform rect, float inset)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(inset, inset);
        rect.offsetMax = new Vector2(-inset, -inset);
    }

    private static T FindChild<T>(GameObject root, string name) where T : Component
    {
        foreach (T component in root.GetComponentsInChildren<T>(true))
        {
            if (component.name == name)
                return component;
        }
        return null;
    }

    private static void SetComponentReference(Object target, string propertyName, Object value)
    {
        SerializedObject serializedObject = new SerializedObject(target);
        SetReference(serializedObject, propertyName, value);
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetReference(SerializedObject serializedObject, string propertyName, Object value)
    {
        serializedObject.FindProperty(propertyName).objectReferenceValue = value;
    }
}
#endif
