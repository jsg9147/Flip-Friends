using System;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

[CreateAssetMenu(fileName = "MapEditorPalette", menuName = "Flip Friends/Map Editor Palette")]
public class MapEditorPalette : ScriptableObject
{
    public List<PaletteEntry> entries = new List<PaletteEntry>();
    public MapValidationSettings validationSettings = new();

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
        ValidateSettings();
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

    private void ValidateSettings()
    {
        validationSettings ??= new MapValidationSettings();
        validationSettings.minimumPlayerSpawnCount = Mathf.Max(
            1,
            validationSettings.minimumPlayerSpawnCount);
        validationSettings.overlapPositionTolerance = Mathf.Max(
            0f,
            validationSettings.overlapPositionTolerance);
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
    public MapObjectValidationRule validationRule = new();
}

[Serializable]
public class MapValidationSettings
{
    public int minimumPlayerSpawnCount = 1;
    public Vector2 minimumMapPosition = new(-50f, -30f);
    public Vector2 maximumMapPosition = new(50f, 30f);
    public float overlapPositionTolerance = 0.01f;
}

[Serializable]
public class MapObjectValidationRule
{
    public int minimumCount;
    public int maximumCount;
    public bool disallowPositionOverlap = true;
    public bool overlapIsCritical;
}
