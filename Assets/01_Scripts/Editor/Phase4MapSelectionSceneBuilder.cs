#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class Phase4MapSelectionSceneBuilder
{
    private const string MainScenePath = "Assets/00_Scenes/Main.unity";
    private const string GameRoomScenePath = "Assets/00_Scenes/GameRoom.unity";
    private const string PrefabPath = "Assets/03_Prefabs/UI/MapSelectionList.prefab";
    private const string CatalogPath = "Assets/07_ScriptableObject/OfficialMapCatalog.asset";
    private const string PalettePath = "Assets/07_ScriptableObject/MapEditorPalette.asset";
    private const string ItemPrefabPath = "Assets/03_Prefabs/UI/LoadListItem.prefab";

    [MenuItem("Flip Friends/Setup Phase 4 Map Selection UI")]
    public static void Build()
    {
        GameObject prefab = BuildPrefab();
        ConfigureMainScene(prefab);
        ConfigureGameRoomScene(prefab);
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
        GameObject instance = ReplaceSceneInstance(prefab, hostScreen.transform, new Vector2(360f, 0f));
        MapListUI presenter = instance.GetComponent<MapListUI>();
        SetComponentReference(presenter, "hostRoomScreen", hostScreen);

        SerializedObject serializedHost = new SerializedObject(hostScreen);
        SetReference(serializedHost, "selectedMapText", FindChild<TMP_Text>(instance, "SelectionText"));
        SetReference(serializedHost, "validationMessageText", FindChild<TMP_Text>(instance, "MessageText"));
        serializedHost.ApplyModifiedPropertiesWithoutUndo();
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

        Transform legacyPanel = parent.Find("Custom Map List Panel");
        if (legacyPanel != null)
            Object.DestroyImmediate(legacyPanel.gameObject);

        GameObject instance = ReplaceSceneInstance(prefab, parent, Vector2.zero);
        MapListUI presenter = instance.GetComponent<MapListUI>();
        SetComponentReference(presenter, "mapSelectionManager", manager);
        EditorSceneManager.SaveScene(scene);
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
