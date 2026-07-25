using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MapEditorHUD : MonoBehaviour
{
    [Header("맵 이름")]
    [SerializeField] private TMP_InputField mapNameInput;

    [Header("맵 불러오기 팝업")]
    [SerializeField] private GameObject loadPopup;
    [SerializeField] private Transform loadListContainer;
    [SerializeField] private GameObject loadListItemPrefab;

    private void Start()
    {
        if (!ValidateReferences()) return;

        mapNameInput.onEndEdit.AddListener(OnMapNameChanged);
        mapNameInput.text = MapEditorManager.instance?.CurrentMapData?.mapName ?? "새 맵";
        loadPopup.SetActive(false);
    }

    private void OnMapNameChanged(string newName)
    {
        MapEditorManager.instance?.SetMapName(newName);
    }

    public void OnSaveButtonClicked()
    {
        MapEditorManager.instance?.SetMapName(mapNameInput.text);
        MapEditorManager.instance?.SaveMap();
    }

    public void OnNewMapButtonClicked()
    {
        string name = string.IsNullOrEmpty(mapNameInput.text) ? "새 맵" : mapNameInput.text;
        MapEditorManager.instance?.NewMap(name);
        ObjectPlacer.instance?.ClearDisplayObjects();
        mapNameInput.text = name;
    }

    public void OnLoadButtonClicked()
    {
        bool isOpen = !loadPopup.activeSelf;
        loadPopup.SetActive(isOpen);
        if (isOpen) PopulateLoadList();
    }

    public void OnReturnButtonClicked()
    {
        if (MapEditorManager.instance == null)
        {
            Debug.LogError("Main으로 돌아갈 수 없습니다. MapEditorManager 참조가 없습니다.", this);
            return;
        }

        MapEditorManager.instance.ReturnToMain();
    }

    private void PopulateLoadList()
    {
        foreach (Transform child in loadListContainer)
            Destroy(child.gameObject);

        List<string> mapNames = MapDataRepository.GetAllMapNames();
        foreach (string mapName in mapNames)
        {
            CreateLoadListItem(mapName);
        }
    }

    private void CreateLoadListItem(string mapName)
    {
        GameObject item = Instantiate(loadListItemPrefab, loadListContainer);
        TMP_Text label = item.GetComponentInChildren<TMP_Text>();
        Button button = item.GetComponent<Button>();
        if (label == null || button == null)
        {
            Debug.LogError($"맵 목록 버튼 프리팹에 필요한 TMP_Text 또는 Button이 없습니다: {loadListItemPrefab.name}", loadListItemPrefab);
            Destroy(item);
            return;
        }

        label.text = mapName;
        button.onClick.AddListener(() =>
        {
            MapEditorManager.instance?.LoadMap(mapName);
            mapNameInput.text = mapName;
            loadPopup.SetActive(false);
        });
    }

    private bool ValidateReferences()
    {
        bool isValid = true;
        isValid &= ValidateReference(mapNameInput, nameof(mapNameInput));
        isValid &= ValidateReference(loadPopup, nameof(loadPopup));
        isValid &= ValidateReference(loadListContainer, nameof(loadListContainer));
        isValid &= ValidateReference(loadListItemPrefab, nameof(loadListItemPrefab));
        return isValid;
    }

    private bool ValidateReference(Object reference, string fieldName)
    {
        if (reference != null) return true;

        Debug.LogError($"MapEditorHUD의 Inspector 참조가 누락되었습니다: {fieldName}", this);
        return false;
    }
}
