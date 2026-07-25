using System;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

[CreateAssetMenu(fileName = "MapEditorPalette", menuName = "Flip Friends/Map Editor Palette")]
public class MapEditorPalette : ScriptableObject
{
    public List<PaletteEntry> entries = new List<PaletteEntry>();

    public bool TryGetEntry(string prefabID, out PaletteEntry entry)
    {
        entry = entries.Find(candidate => candidate.id == prefabID);
        if (entry != null)
            return true;

        Debug.LogError($"등록되지 않은 팔레트 ID입니다: {prefabID}");
        return false;
    }

    public GameObject GetPrefab(string prefabID)
    {
        if (!TryGetEntry(prefabID, out PaletteEntry entry))
            return null;

        if (entry.prefab == null)
        {
            Debug.LogError($"프리팹이 연결되지 않은 팔레트 ID입니다: {prefabID}");
            return null;
        }

        return entry.prefab;
    }

    private void OnValidate()
    {
        var registeredIDs = new HashSet<string>();
        foreach (PaletteEntry entry in entries)
        {
            if (entry == null)
            {
                Debug.LogError("null 팔레트 항목이 있습니다.", this);
                continue;
            }

            if (string.IsNullOrWhiteSpace(entry.id))
            {
                Debug.LogWarning($"ID가 비어 있는 팔레트 항목이 있습니다: {entry.displayName}", this);
                continue;
            }

            if (!registeredIDs.Add(entry.id))
                Debug.LogError($"중복된 팔레트 ID가 있습니다: {entry.id}", this);

            if (entry.prefab == null)
            {
                Debug.LogError($"프리팹이 연결되지 않은 팔레트 항목입니다: {entry.id}", this);
                continue;
            }

            if (entry.thumbnail == null)
                Debug.LogWarning($"썸네일이 연결되지 않은 팔레트 항목입니다: {entry.id}", this);

            if (entry.prefab.GetComponent<NetworkIdentity>() == null)
                Debug.LogError($"NetworkIdentity가 없는 팔레트 프리팹입니다: {entry.id}", entry.prefab);
        }
    }
}

public enum MapObjectCategory
{
    Essential,
    Terrain,
    Obstacle,
    Interactive,
    Decoration
}

[Serializable]
public class PaletteEntry
{
    public string id;
    public string displayName;
    public MapObjectCategory category;
    public GameObject prefab;
    public Sprite thumbnail;
}
