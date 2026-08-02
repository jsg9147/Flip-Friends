using System;
using System.Collections.Generic;

[Serializable]
public class MapData
{
    public const string CurrentVersion = "2.1";
    public const string PreviousVersion = "2.0";

    public string mapId;
    public string mapName;
    public string authorName;
    public string version = CurrentVersion;
    public List<PlacedObjectData> objects = new List<PlacedObjectData>();

    public MapData(string mapName, string authorName)
    {
        mapId = CreateMapId();
        this.mapName = mapName;
        this.authorName = authorName;
    }

    public static string CreateMapId() => Guid.NewGuid().ToString("N");

    public static bool TryNormalizeMapId(string value, out string normalizedMapId)
    {
        normalizedMapId = null;
        if (!Guid.TryParseExact(value, "N", out Guid mapId)) return false;

        normalizedMapId = mapId.ToString("N");
        return true;
    }
}
