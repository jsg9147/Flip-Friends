using System;
using UnityEngine;

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
    public Sprite paletteIcon;
    public MapObjectValidationRule validationRule = new();
}
