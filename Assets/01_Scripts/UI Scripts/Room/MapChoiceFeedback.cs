using UnityEngine;

public class MapChoiceFeedback : MonoBehaviour
{
    [SerializeField] private MapSelectionManager selectionManager;
    [SerializeField] private LobbyMapKind mapKind;
    [SerializeField] private string mapId;
    [SerializeField] private GameObject selectedMarker;

    private void OnEnable()
    {
        if (selectionManager != null)
            selectionManager.SelectionChanged += RefreshSelection;
        RefreshSelection();
    }

    private void OnDisable()
    {
        if (selectionManager != null)
            selectionManager.SelectionChanged -= RefreshSelection;
    }

    public void Bind(MapSelectionManager manager, LobbyMapKind kind, string id)
    {
        if (isActiveAndEnabled && selectionManager != null)
            selectionManager.SelectionChanged -= RefreshSelection;
        selectionManager = manager;
        mapKind = kind;
        mapId = id;
        if (isActiveAndEnabled && selectionManager != null)
            selectionManager.SelectionChanged += RefreshSelection;
        RefreshSelection();
    }

    private void RefreshSelection()
    {
        bool isSelected = selectionManager != null &&
            !string.IsNullOrEmpty(mapId) &&
            selectionManager.SelectedMapKind == mapKind &&
            selectionManager.SelectedMapId == mapId;
        selectedMarker.SetActive(isSelected);
    }
}
