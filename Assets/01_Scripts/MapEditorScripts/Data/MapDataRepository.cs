using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public static class MapDataRepository
{
    private static readonly HashSet<string> WindowsReservedNames =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "CON", "PRN", "AUX", "NUL",
            "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
            "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
        };

    private static string SaveDirectory => Path.Combine(Application.persistentDataPath, "Maps");
    public static event Action<string> OperationFailed;

    public static bool Save(MapData data)
    {
        if (data == null)
        {
            ReportFailure("저장할 맵 데이터가 없습니다.");
            return false;
        }

        if (!TryNormalizeMapName(data.mapName, out string mapName, out string error))
        {
            ReportFailure($"맵을 저장할 수 없습니다: {error}");
            return false;
        }

        try
        {
            data.mapName = mapName;
            EnsureDirectoryExists();
            if (!TryGetFilePath(mapName, out string path, out error))
            {
                ReportFailure($"맵을 저장할 수 없습니다: {error}");
                return false;
            }

            string json = JsonUtility.ToJson(data, prettyPrint: true);
            WriteSafely(path, json);
            return true;
        }
        catch (Exception exception)
        {
            ReportException($"맵 저장 중 오류가 발생했습니다: {mapName}", exception);
            return false;
        }
    }

    public static MapData Load(string mapName)
    {
        if (!TryGetFilePath(mapName, out string path, out string error))
        {
            ReportFailure($"맵을 불러올 수 없습니다: {error}");
            return null;
        }

        try
        {
            if (!File.Exists(path))
            {
                ReportFailure($"맵 파일을 찾을 수 없습니다: {path}");
                return null;
            }

            string json = File.ReadAllText(path);
            return ParseJson(json, $"맵 파일 '{mapName}'");
        }
        catch (Exception exception)
        {
            ReportException($"맵 불러오기 중 오류가 발생했습니다: {mapName}", exception);
            return null;
        }
    }

    public static List<string> GetAllMapNames()
    {
        var names = new List<string>();
        try
        {
            EnsureDirectoryExists();
            foreach (string file in Directory.GetFiles(SaveDirectory, "*.json"))
                names.Add(Path.GetFileNameWithoutExtension(file));
        }
        catch (Exception exception)
        {
            ReportException("저장된 맵 목록을 읽는 중 오류가 발생했습니다.", exception);
        }

        names.Sort(CompareMapNames);
        return names;
    }

    public static bool Delete(string mapName)
    {
        if (!TryGetFilePath(mapName, out string path, out string error))
        {
            ReportFailure($"맵을 삭제할 수 없습니다: {error}");
            return false;
        }

        try
        {
            if (!File.Exists(path))
            {
                ReportFailure($"삭제할 맵 파일을 찾을 수 없습니다: {path}");
                return false;
            }

            File.Delete(path);
            return true;
        }
        catch (Exception exception)
        {
            ReportException($"맵 삭제 중 오류가 발생했습니다: {mapName}", exception);
            return false;
        }
    }

    public static bool TryExists(string mapName, out bool exists)
    {
        exists = false;
        if (!TryGetFilePath(mapName, out string path, out string error))
        {
            ReportFailure($"맵 파일을 확인할 수 없습니다: {error}");
            return false;
        }

        try
        {
            exists = File.Exists(path);
            return true;
        }
        catch (Exception exception)
        {
            ReportException($"맵 파일 존재 여부 확인 중 오류가 발생했습니다: {mapName}", exception);
            return false;
        }
    }

    public static string ToJson(MapData data)
    {
        if (data == null)
        {
            ReportFailure("JSON으로 변환할 맵 데이터가 없습니다.");
            return null;
        }

        try
        {
            return JsonUtility.ToJson(data);
        }
        catch (Exception exception)
        {
            ReportException("맵 데이터를 JSON으로 변환하는 중 오류가 발생했습니다.", exception);
            return null;
        }
    }

    public static MapData FromJson(string json) => ParseJson(json, "수신된 맵 데이터");

    public static bool TryNormalizeMapName(
        string mapName,
        out string normalizedName,
        out string error)
    {
        normalizedName = mapName?.Trim();
        if (string.IsNullOrEmpty(normalizedName))
        {
            error = "맵 이름을 입력하세요.";
            return false;
        }

        if (normalizedName.Contains("..", StringComparison.Ordinal))
        {
            error = "맵 이름에는 '..'를 사용할 수 없습니다.";
            return false;
        }

        if (normalizedName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
            normalizedName.IndexOf(Path.DirectorySeparatorChar) >= 0 ||
            normalizedName.IndexOf(Path.AltDirectorySeparatorChar) >= 0)
        {
            error = "맵 이름에 파일명으로 사용할 수 없는 문자가 있습니다.";
            return false;
        }

        if (normalizedName.EndsWith(".", StringComparison.Ordinal))
        {
            error = "맵 이름은 마침표로 끝날 수 없습니다.";
            return false;
        }

        string baseName = Path.GetFileNameWithoutExtension(normalizedName);
        if (WindowsReservedNames.Contains(baseName))
        {
            error = $"'{normalizedName}'은(는) 파일명으로 사용할 수 없는 이름입니다.";
            return false;
        }

        error = null;
        return true;
    }

    private static bool TryGetFilePath(
        string mapName,
        out string filePath,
        out string error)
    {
        filePath = null;
        if (!TryNormalizeMapName(mapName, out string normalizedName, out error))
            return false;

        string directoryPath = Path.GetFullPath(SaveDirectory);
        string candidatePath = Path.GetFullPath(
            Path.Combine(directoryPath, $"{normalizedName}.json"));
        string directoryPrefix = directoryPath.TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;

        if (!candidatePath.StartsWith(directoryPrefix, StringComparison.OrdinalIgnoreCase))
        {
            error = "저장 폴더 밖의 경로에는 접근할 수 없습니다.";
            return false;
        }

        filePath = candidatePath;
        return true;
    }

    private static void EnsureDirectoryExists()
    {
        if (!Directory.Exists(SaveDirectory))
            Directory.CreateDirectory(SaveDirectory);
    }

    private static void WriteSafely(string destinationPath, string contents)
    {
        string temporaryPath = $"{destinationPath}.tmp";
        try
        {
            DeleteStaleTemporaryFile(temporaryPath);
            File.WriteAllText(temporaryPath, contents);

            if (File.Exists(destinationPath))
                File.Replace(temporaryPath, destinationPath, null);
            else
                File.Move(temporaryPath, destinationPath);
        }
        finally
        {
            DeleteStaleTemporaryFile(temporaryPath);
        }
    }

    private static void DeleteStaleTemporaryFile(string temporaryPath)
    {
        if (!File.Exists(temporaryPath)) return;

        try
        {
            File.Delete(temporaryPath);
        }
        catch (Exception exception)
        {
            Debug.LogWarning(
                $"맵 저장 임시 파일을 정리하지 못했습니다: {temporaryPath}\n{exception}");
        }
    }

    private static int CompareMapNames(string first, string second)
    {
        int ignoreCaseResult = StringComparer.OrdinalIgnoreCase.Compare(first, second);
        return ignoreCaseResult != 0
            ? ignoreCaseResult
            : StringComparer.Ordinal.Compare(first, second);
    }

    private static MapData ParseJson(string json, string context)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            ReportFailure($"{context}의 JSON이 비어 있습니다.");
            return null;
        }

        try
        {
            MapData data = JsonUtility.FromJson<MapData>(json);
            if (data == null)
            {
                ReportFailure($"{context}의 JSON을 맵 데이터로 변환할 수 없습니다.");
                return null;
            }

            if (!TryMigrate(data, out MapData migratedData, out string error))
            {
                ReportFailure($"{context}을(를) 불러올 수 없습니다: {error}");
                return null;
            }

            return migratedData;
        }
        catch (Exception exception)
        {
            ReportException($"{context}의 JSON 형식이 올바르지 않습니다.", exception);
            return null;
        }
    }

    private static void ReportFailure(string message)
    {
        Debug.LogWarning(message);
        OperationFailed?.Invoke(message);
    }

    private static bool TryMigrate(
        MapData source,
        out MapData migratedData,
        out string error)
    {
        migratedData = null;
        if (string.IsNullOrWhiteSpace(source.version))
        {
            error = "맵 데이터 버전이 없어 안전하게 변환할 수 없습니다.";
            return false;
        }

        switch (source.version)
        {
            case MapData.CurrentVersion:
                migratedData = source;
                error = null;
                return true;
            default:
                error = $"지원하지 않는 맵 데이터 버전입니다: {source.version} " +
                        $"(현재 버전: {MapData.CurrentVersion})";
                return false;
        }
    }

    private static void ReportException(string message, Exception exception)
    {
        Debug.LogError(message);
        Debug.LogException(exception);
        OperationFailed?.Invoke(message);
    }
}
