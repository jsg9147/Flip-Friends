using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// GameRoom 씬에서 방장이 로컬 저장 맵을 선택하는 패널
public class MapListUI : MonoBehaviour
{
    [SerializeField] private GameObject panel;
    [SerializeField] private Transform listContainer;
    [SerializeField] private GameObject listItemPrefab;
    [SerializeField] private MapSelectionManager mapSelectionManager;

    private void Start()
    {
        panel.SetActive(false);
    }

    public void TogglePanel()
    {
        bool isOpen = !panel.activeSelf;
        panel.SetActive(isOpen);
        if (isOpen) PopulateList();
    }

    private void PopulateList()
    {
        foreach (Transform child in listContainer)
            Destroy(child.gameObject);

        List<string> mapNames = MapDataRepository.GetAllMapNames();
        foreach (string mapName in mapNames)
            CreateListItem(mapName);
    }

    private void CreateListItem(string mapName)
    {
        GameObject item = Instantiate(listItemPrefab, listContainer);
        item.GetComponentInChildren<TMP_Text>().text = mapName;
        item.GetComponent<Button>().onClick.AddListener(() => OnMapSelected(mapName));
    }

    private void OnMapSelected(string mapName)
    {
        MapData mapData = MapDataRepository.Load(mapName);
        if (mapData == null) return;

        string json = MapDataRepository.ToJson(mapData);
        mapSelectionManager.CustomMapLoad(json);
        panel.SetActive(false);
    }
}
