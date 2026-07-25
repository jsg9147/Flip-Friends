using System;
using System.Collections.Generic;

[Serializable]
public class MapData
{
    public string mapName;
    public string authorName;
    public string version = "2.0";
    public List<PlacedObjectData> objects = new List<PlacedObjectData>();

    public MapData(string mapName, string authorName)
    {
        this.mapName = mapName;
        this.authorName = authorName;
    }
}
