using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class MainMenuScreen : UIScreen
{
    private const string MapEditorSceneName = "MapEditor";
    private const string MapEditorButtonName = "Map Editor Button";

    [SerializeField] private GameObject titleLogo;
    [SerializeField] private GameObject titleText;
    private GameObject transitionBackdrop;

    protected override void Awake()
    {
        base.Awake();
        CreateMapEditorButton();
        CreateTransitionBackdrop();
    }

    protected override void OnShow()
    {
        if (titleLogo != null) titleLogo.SetActive(true);
        if (titleText != null) titleText.SetActive(true);
        if (transitionBackdrop != null) transitionBackdrop.SetActive(false);
    }

    protected override void OnHide()
    {
        if (titleLogo != null) titleLogo.SetActive(false);
        if (titleText != null) titleText.SetActive(false);
        if (transitionBackdrop != null) transitionBackdrop.SetActive(true);
    }

    private void CreateTransitionBackdrop()
    {
        if (transform.parent == null) return;
        transitionBackdrop = new GameObject("Pixel Menu Backdrop", typeof(RectTransform), typeof(Image));
        transitionBackdrop.transform.SetParent(transform.parent, false);
        transitionBackdrop.transform.SetAsFirstSibling();
        RectTransform backgroundRect = (RectTransform)transitionBackdrop.transform;
        backgroundRect.anchorMin = Vector2.zero;
        backgroundRect.anchorMax = Vector2.one;
        backgroundRect.offsetMin = Vector2.zero;
        backgroundRect.offsetMax = Vector2.zero;
        Image backgroundImage = transitionBackdrop.GetComponent<Image>();
        backgroundImage.color = new Color32(5, 38, 52, 255);
        backgroundImage.raycastTarget = false;

        AddPixel(0.12f, 0.78f, 14, 14, new Color32(232, 242, 209, 255));
        AddPixel(0.28f, 0.88f, 10, 10, new Color32(19, 113, 125, 255));
        AddPixel(0.48f, 0.74f, 12, 12, new Color32(232, 242, 209, 255));
        AddPixel(0.71f, 0.91f, 14, 14, new Color32(19, 113, 125, 255));
        AddPixel(0.87f, 0.68f, 10, 10, new Color32(232, 242, 209, 255));
        AddPlatform(0f, 0.14f, 0.2f);
        AddPlatform(1f, 0.14f, 0.2f);
        transitionBackdrop.SetActive(false);
    }

    private void AddPixel(float x, float y, float width, float height, Color color)
    {
        GameObject pixel = new GameObject("Pixel Star", typeof(RectTransform), typeof(Image));
        pixel.transform.SetParent(transitionBackdrop.transform, false);
        RectTransform rect = (RectTransform)pixel.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(x, y);
        rect.sizeDelta = new Vector2(width, height);
        Image image = pixel.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
    }

    private void AddPlatform(float x, float y, float width)
    {
        GameObject platform = new GameObject("Pixel Platform", typeof(RectTransform));
        platform.transform.SetParent(transitionBackdrop.transform, false);
        RectTransform rect = (RectTransform)platform.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(x, y);
        rect.pivot = new Vector2(x, 0.5f);
        rect.sizeDelta = new Vector2(Screen.width * width, 50);
        Image block = platform.gameObject.AddComponent<Image>();
        block.color = new Color32(8, 85, 101, 255);
        block.raycastTarget = false;
        GameObject top = new GameObject("Lime Platform Edge", typeof(RectTransform), typeof(Image));
        top.transform.SetParent(platform.transform, false);
        RectTransform topRect = (RectTransform)top.transform;
        topRect.anchorMin = new Vector2(0, 1);
        topRect.anchorMax = new Vector2(1, 1);
        topRect.pivot = new Vector2(0.5f, 1);
        topRect.sizeDelta = new Vector2(0, 12);
        Image topImage = top.GetComponent<Image>();
        topImage.color = new Color32(183, 239, 53, 255);
        topImage.raycastTarget = false;
    }

    public void OpenMapEditor()
    {
        if (NetworkServer.active || NetworkClient.active)
        {
            Debug.LogWarning("네트워크 세션이 활성화되어 있어 맵 에디터 진입을 차단했습니다. 세션을 종료한 뒤 다시 시도하세요.", this);
            return;
        }

        if (!Application.CanStreamedLevelBeLoaded(MapEditorSceneName))
        {
            Debug.LogError($"맵 에디터 씬을 불러올 수 없습니다. Build Settings에서 '{MapEditorSceneName}' 씬이 활성화되어 있는지 확인하세요.", this);
            return;
        }

        SceneManager.LoadScene(MapEditorSceneName);
    }

    private void CreateMapEditorButton()
    {
        Button templateButton = GetComponentInChildren<Button>(true);
        if (templateButton == null || templateButton.transform.parent == null)
        {
            Debug.LogError("맵 에디터 버튼의 템플릿으로 사용할 Main 메뉴 버튼을 찾지 못했습니다.", this);
            return;
        }

        Transform buttonContainer = templateButton.transform.parent;
        Transform existingButton = buttonContainer.Find(MapEditorButtonName);
        Button mapEditorButton = existingButton != null
            ? existingButton.GetComponent<Button>()
            : Instantiate(templateButton, buttonContainer);

        if (mapEditorButton == null)
        {
            Debug.LogError("맵 에디터 진입 버튼에 Button 컴포넌트가 없습니다.", this);
            return;
        }

        mapEditorButton.name = MapEditorButtonName;
        RemoveNavigationComponents(mapEditorButton.gameObject);
        mapEditorButton.onClick.RemoveAllListeners();
        mapEditorButton.onClick.AddListener(OpenMapEditor);
        SetButtonLabel(mapEditorButton);
        RegisterNavigation(mapEditorButton);
    }

    private void RemoveNavigationComponents(GameObject buttonObject)
    {
        ScreenNavigationButton[] navigationComponents = buttonObject.GetComponents<ScreenNavigationButton>();
        foreach (ScreenNavigationButton navigationComponent in navigationComponents)
        {
            Destroy(navigationComponent);
        }
    }

    private void SetButtonLabel(Button button)
    {
        TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
        if (label == null)
        {
            Debug.LogError("맵 에디터 진입 버튼의 TMP 텍스트를 찾지 못했습니다.", button);
            return;
        }

        label.text = "MAP EDITOR";
    }

    private void RegisterNavigation(Button button)
    {
        ButtonSelectController controller = GetComponent<ButtonSelectController>();
        if (controller == null)
        {
            Debug.LogWarning("Main 메뉴의 ButtonSelectController를 찾지 못해 맵 에디터 버튼의 탐색 순서를 등록하지 못했습니다.", this);
            return;
        }

        controller.tagetButtonList.Remove(button);
        int insertionIndex = Mathf.Max(0, controller.tagetButtonList.Count - 1);
        if (controller.tagetButtonList.Count > 0)
        {
            Selectable exitButton = controller.tagetButtonList[insertionIndex];
            // 화면 배치도 선택 목록과 같이 종료 버튼 바로 앞에 둔다.
            int siblingIndex = exitButton.transform.GetSiblingIndex();
            if (button.transform.GetSiblingIndex() < siblingIndex)
                siblingIndex--;
            button.transform.SetSiblingIndex(siblingIndex);
        }
        controller.InsertSelectable(insertionIndex, button);
    }
}
