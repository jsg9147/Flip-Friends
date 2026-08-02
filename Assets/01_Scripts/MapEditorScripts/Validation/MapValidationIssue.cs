using System;

public enum MapValidationSeverity
{
    Info,
    Warning,
    Error
}

[Serializable]
public sealed class MapValidationIssue
{
    public MapValidationSeverity Severity { get; }
    public string Message { get; }
    public string PrefabID { get; }
    public int ObjectIndex { get; }

    public MapValidationIssue(
        MapValidationSeverity severity,
        string message,
        string prefabID = null,
        int objectIndex = -1)
    {
        Severity = severity;
        Message = message;
        PrefabID = prefabID;
        ObjectIndex = objectIndex;
    }
}
