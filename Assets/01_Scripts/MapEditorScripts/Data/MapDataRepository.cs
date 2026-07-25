using System.Collections.Generic;
using System.IO;
using UnityEngine;

public static class MapDataRepository
{
    private static string SaveDirectory => Path.Combine(Application.persistentDataPath, "Maps");

    public static void Save(MapData data)
    {
        EnsureDirectoryExists();
        string json = JsonUtility.ToJson(data, prettyPrint: true);
        string path = GetFilePath(data.mapName);
        File.WriteAllText(path, json);
    }

    public static MapData Load(string mapName)
    {
        string path = GetFilePath(mapName);
        if (!File.Exists(path))
        {
            Debug.LogWarning($"맵 파일을 찾을 수 없습니다: {path}");
            return null;
        }

        string json = File.ReadAllText(path);
        return JsonUtility.FromJson<MapData>(json);
    }

    public static List<string> GetAllMapNames()
    {
        EnsureDirectoryExists();
        var names = new List<string>();
        foreach (string file in Directory.GetFiles(SaveDirectory, "*.json"))
        {
            names.Add(Path.GetFileNameWithoutExtension(file));
        }
        return names;
    }

    public static void Delete(string mapName)
    {
        string path = GetFilePath(mapName);
        if (File.Exists(path))
            File.Delete(path);
    }

    public static string ToJson(MapData data) => JsonUtility.ToJson(data);

    public static MapData FromJson(string json) => JsonUtility.FromJson<MapData>(json);

    private static string GetFilePath(string mapName) =>
        Path.Combine(SaveDirectory, $"{mapName}.json");

    private static void EnsureDirectoryExists()
    {
        if (!Directory.Exists(SaveDirectory))
            Directory.CreateDirectory(SaveDirectory);
    }
}
