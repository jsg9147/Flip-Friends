using System;
using UnityEngine;

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
