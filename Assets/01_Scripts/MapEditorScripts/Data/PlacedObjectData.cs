using System;
using UnityEngine;

[Serializable]
public class PlacedObjectData
{
    public string prefabID;
    public SerializableVector3 position;
    public float rotation;
    public SerializableVector3 scale;

    public PlacedObjectData(string prefabID, Vector3 position, float rotation, Vector3 scale)
    {
        this.prefabID = prefabID;
        this.position = new SerializableVector3(position);
        this.rotation = rotation;
        this.scale = new SerializableVector3(scale);
    }
}

[Serializable]
public class SerializableVector3
{
    public float x, y, z;

    public SerializableVector3(Vector3 v)
    {
        x = v.x;
        y = v.y;
        z = v.z;
    }

    public Vector3 ToVector3() => new Vector3(x, y, z);
}
