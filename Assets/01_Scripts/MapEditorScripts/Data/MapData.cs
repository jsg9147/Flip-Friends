using System;
using System.Collections.Generic;

[Serializable]
public class MapData
{
    public const string CurrentVersion = "2.0";

    public string mapName;
    public string authorName;
    public string version = CurrentVersion;
    public List<PlacedObjectData> objects = new List<PlacedObjectData>();

    public MapData(string mapName, string authorName)
    {
        this.mapName = mapName;
        this.authorName = authorName;
    }
}
