using TMPro;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PaletteUI : MonoBehaviour
{
    [SerializeField] private Transform buttonContainer;
    [SerializeField] private GameObject paletteButtonPrefab;
    [Header("도트 프레임")]
    [SerializeField] private Sprite normalFrame;
    [SerializeField] private Sprite selectedFrame;

    private Button selectedButton;
    private readonly Dictionary<string, Button> buttonsById = new();
    private readonly List<Button> orderedButtons = new();
    private bool isBuilt;
    private static readonly Color LabelInk = new Color32(7, 53, 56, 255);

    // HUD가 Start에서 키보드·패드 탐색을 이을 때 팔레트 버튼이 필요하다. Start 순서와 무관하게 먼저 만든다.
    public IReadOnlyList<Button> Buttons
    {
        get
        {
            BuildPalette();
            return orderedButtons;
        }
    }

    private void Start()
    {
        BuildPalette();
    }

    private void BuildPalette()
    {
        if (isBuilt) return;
        isBuilt = true;

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
        TMP_Text label = btnObj.GetComponentInChildren<TMP_Text>();
        label.text = entry.displayName;
        // 밝은 도트 프레임 위라 짙은 글자를 쓴다. 흰 글자는 흰 바탕에서 보이지 않았다.
        label.color = LabelInk;
        Image buttonImage = btnObj.GetComponent<Image>();
        if (buttonImage != null)
        {
            buttonImage.sprite = normalFrame;
            buttonImage.type = Image.Type.Sliced;
            buttonImage.color = Color.white;
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
        orderedButtons.Add(button);
    }

    private void SelectPrefab(string prefabID)
    {
        if (selectedButton != null) selectedButton.image.sprite = normalFrame;

        buttonsById.TryGetValue(prefabID, out selectedButton);
        if (selectedButton != null) selectedButton.image.sprite = selectedFrame;

        MapEditorManager.instance?.SelectPrefab(prefabID);
        ObjectPlacer.instance?.OnPaletteChanged(prefabID);
        if (MapEditorManager.instance?.palette != null && MapEditorManager.instance.palette.TryGetEntry(prefabID, out PaletteEntry entry))
            FindAnyObjectByType<MapEditorHUD>()?.SetSelectedObjectLabel(entry.displayName);
    }
}
