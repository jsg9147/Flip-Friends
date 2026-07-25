using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PaletteUI : MonoBehaviour
{
    [SerializeField] private Transform buttonContainer;
    [SerializeField] private GameObject paletteButtonPrefab;

    private Button selectedButton;

    private void Start()
    {
        BuildPalette();
    }

    private void BuildPalette()
    {
        if (buttonContainer == null || paletteButtonPrefab == null)
        {
            Debug.LogError($"PaletteUI의 Inspector 참조가 누락되었습니다. buttonContainer: {buttonContainer != null}, paletteButtonPrefab: {paletteButtonPrefab != null}", this);
            return;
        }

        MapEditorPalette palette = MapEditorManager.instance?.palette;
        if (palette == null)
        {
            Debug.LogError("MapEditorManager에 팔레트가 연결되어 있지 않습니다.", this);
            return;
        }

        if (palette.entries.Count == 0)
        {
            Debug.LogWarning("팔레트 항목이 비어 있습니다.", palette);
            return;
        }

        for (int i = 0; i < palette.entries.Count; i++)
        {
            CreatePaletteButton(palette.entries[i]);
        }

        if (palette.entries.Count > 0)
            SelectPrefab(palette.entries[0].id);
    }

    private void CreatePaletteButton(PaletteEntry entry)
    {
        GameObject btnObj = Instantiate(paletteButtonPrefab, buttonContainer);
        btnObj.GetComponentInChildren<TMP_Text>().text = entry.displayName;

        var icon = btnObj.transform.Find("Icon");
        if (icon == null)
        {
            Debug.LogWarning($"팔레트 버튼에 Icon 자식이 없습니다: {entry.id}", btnObj);
        }
        else if (entry.thumbnail != null)
        {
            icon.GetComponent<Image>().sprite = entry.thumbnail;
        }
        else
        {
            Debug.LogWarning($"팔레트 썸네일이 누락되었습니다: {entry.id}", this);
        }

        string prefabID = entry.id;
        btnObj.GetComponent<Button>().onClick.AddListener(() => SelectPrefab(prefabID));
    }

    private void SelectPrefab(string prefabID)
    {
        MapEditorManager.instance?.SelectPrefab(prefabID);
        ObjectPlacer.instance?.OnPaletteChanged(prefabID);
    }
}
