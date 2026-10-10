using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "BuiltInMapCatalog", menuName = "Flip Friends/Built-In Map Catalog")]
public class BuiltInMapCatalog : ScriptableObject
{
    [Serializable]
    public class Entry
    {
        [SerializeField] private string mapId;
        [SerializeField] private string displayName;
        [SerializeField] private GameObject stagePrefab;

        public string MapId => mapId;
        public string DisplayName => displayName;
        public GameObject StagePrefab => stagePrefab;
    }

    // 순서는 GameRoom의 맵 버튼 순서와만 연결된다. 네트워크와 씬 로드는 MapId만 쓴다.
    [SerializeField] private List<Entry> entries = new();

    public int Count => entries.Count;

    public Entry GetAt(int index) =>
        index >= 0 && index < entries.Count ? entries[index] : null;

    public bool TryGet(string mapId, out Entry entry)
    {
        entry = null;
        if (string.IsNullOrEmpty(mapId)) return false;

        foreach (Entry candidate in entries)
        {
            if (candidate != null && candidate.MapId == mapId)
            {
                entry = candidate;
                return entry.StagePrefab != null;
            }
        }

        return false;
    }

    private void OnValidate()
    {
        var mapIds = new List<string>(entries.Count);
        foreach (Entry entry in entries)
            mapIds.Add(entry?.MapId);

        if (BuiltInMapId.TryFindProblem(mapIds, out string problem))
            Debug.LogWarning($"{name}: {problem}", this);
    }
}
