using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "OfficialMapCatalog", menuName = "Flip Friends/Official Map Catalog")]
public sealed class OfficialMapCatalog : ScriptableObject
{
    private static readonly IReadOnlyList<OfficialMapEntry> EmptyEntries =
        Array.Empty<OfficialMapEntry>();

    [SerializeField] private List<OfficialMapEntry> entries = new();

    public IReadOnlyList<OfficialMapEntry> Entries => entries ?? EmptyEntries;

    public bool TryGet(string mapId, out OfficialMapEntry entry)
    {
        entry = null;
        if (entries == null) return false;
        if (!OfficialMapId.TryNormalize(mapId, out string normalizedMapId) ||
            normalizedMapId != mapId)
            return false;

        entry = entries.Find(candidate => candidate != null && candidate.MapId == mapId);
        return entry != null;
    }

    public IReadOnlyList<string> GetValidationErrors()
    {
        if (entries == null)
            return OfficialMapCatalogRules.Validate(null);

        var descriptors = new List<OfficialMapDescriptor>(entries.Count);
        foreach (OfficialMapEntry entry in entries)
            descriptors.Add(entry?.ToDescriptor());

        var errors = new List<string>(OfficialMapCatalogRules.Validate(descriptors));
        ValidateUnityReferences(errors);
        return errors;
    }

    private void OnValidate()
    {
        foreach (string error in GetValidationErrors())
            Debug.LogError(error, this);
    }

    private void ValidateUnityReferences(List<string> errors)
    {
        for (int index = 0; index < entries.Count; index++)
        {
            OfficialMapEntry entry = entries[index];
            if (entry == null) continue;

            if (entry.StagePrefab == null)
                errors.Add($"공식맵 프리팹이 없습니다: {entry.MapId}");
        }
    }
}

[Serializable]
public sealed class OfficialMapEntry
{
    [SerializeField] private string mapId;
    [SerializeField] private string displayName;
    [SerializeField] private string authorName = "Flip Friends";
    [SerializeField] private string version = "1.0";
    [SerializeField, Range(1, 4)] private int minimumPlayersToClear = 1;
    [SerializeField, Min(1)] private int completionRevision = 1;
    [SerializeField] private GameObject stagePrefab;
    [SerializeField] private Sprite thumbnail;

    public string MapId => mapId;
    public string DisplayName => displayName;
    public string AuthorName => authorName;
    public string Version => version;
    public int MinimumPlayersToClear => minimumPlayersToClear;
    public int CompletionRevision => completionRevision;
    public GameObject StagePrefab => stagePrefab;
    public Sprite Thumbnail => thumbnail;

    public OfficialMapDescriptor ToDescriptor() =>
        new(
            mapId,
            displayName,
            authorName,
            version,
            minimumPlayersToClear,
            completionRevision);
}
