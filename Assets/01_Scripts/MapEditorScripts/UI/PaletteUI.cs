using TMPro;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PaletteUI : MonoBehaviour
{
    [SerializeField] private Transform buttonContainer;
    [SerializeField] private GameObject paletteButtonPrefab;

    private Button selectedButton;
    private readonly Dictionary<string, Button> buttonsById = new();
    private static readonly Color SelectedColor = new(0.68f, 0.94f, 0.31f, 1f);
    private static readonly Color NormalColor = Color.white;

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
        Image buttonImage = btnObj.GetComponent<Image>();
        if (buttonImage != null)
        {
            buttonImage.sprite = null;
            buttonImage.color = NormalColor;
            Outline outline = btnObj.GetComponent<Outline>();
            if (outline == null) outline = btnObj.AddComponent<Outline>();
            outline.effectColor = new Color(0.35f, 0.76f, 0.86f, 1f);
            outline.effectDistance = new Vector2(1f, -1f);
            outline.useGraphicAlpha = true;
        }

        var icon = btnObj.transform.Find("Icon");
        if (icon == null)
        {
            Debug.LogWarning($"팔레트 버튼에 Icon 자식이 없습니다: {entry.id}", btnObj);
        }
        else if (entry.paletteIcon != null || entry.thumbnail != null)
        {
            Image image = icon.GetComponent<Image>();
            image.sprite = entry.paletteIcon != null ? entry.paletteIcon : entry.thumbnail;
            if (entry.paletteIcon != null)
                image.preserveAspect = true;
        }
        else
        {
            Debug.LogWarning($"팔레트 썸네일이 누락되었습니다: {entry.id}", this);
        }

        string prefabID = entry.id;
        Button button = btnObj.GetComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.onClick.AddListener(() => SelectPrefab(prefabID));
        buttonsById[prefabID] = button;
    }

    private void SelectPrefab(string prefabID)
    {
        if (selectedButton != null && selectedButton.targetGraphic != null)
            selectedButton.targetGraphic.color = NormalColor;

        buttonsById.TryGetValue(prefabID, out selectedButton);
        if (selectedButton != null && selectedButton.targetGraphic != null)
            selectedButton.targetGraphic.color = SelectedColor;

        MapEditorManager.instance?.SelectPrefab(prefabID);
        ObjectPlacer.instance?.OnPaletteChanged(prefabID);
        if (MapEditorManager.instance?.palette != null && MapEditorManager.instance.palette.TryGetEntry(prefabID, out PaletteEntry entry))
            FindAnyObjectByType<MapEditorHUD>()?.SetSelectedObjectLabel(entry.displayName);
    }
}
