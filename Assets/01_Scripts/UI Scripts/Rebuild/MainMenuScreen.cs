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

    protected override void Awake()
    {
        base.Awake();
        CreateMapEditorButton();
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
        RegisterLegacyNavigation(mapEditorButton);
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

    private void RegisterLegacyNavigation(Button button)
    {
        ButtonSelectController controller = GetComponent<ButtonSelectController>();
        if (controller == null)
        {
            Debug.LogWarning("Main 메뉴의 ButtonSelectController를 찾지 못해 맵 에디터 버튼의 레거시 키보드 탐색을 등록하지 못했습니다.", this);
            return;
        }

        if (!controller.tagetButtonList.Contains(button))
        {
            controller.tagetButtonList.Insert(controller.tagetButtonList.Count - 1, button);
        }
    }
}
