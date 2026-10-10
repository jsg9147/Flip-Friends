using System.Collections.Generic;
using System.Text.RegularExpressions;

// 기본 맵 식별자 규칙. 목록 순서가 바뀌어도 선택·네트워크·씬 로드가 같은 맵을 가리키도록
// 정수 인덱스 대신 소문자 kebab-case 문자열을 쓴다.
public static class BuiltInMapId
{
    public const int MaximumLength = 64;

    private static readonly Regex Pattern = new("^[a-z0-9]+(-[a-z0-9]+)*$");

    public static bool IsValid(string mapId) =>
        !string.IsNullOrEmpty(mapId) &&
        mapId.Length <= MaximumLength &&
        Pattern.IsMatch(mapId);

    public static bool TryFindProblem(IEnumerable<string> mapIds, out string problem)
    {
        problem = null;
        if (mapIds == null) return false;

        var seen = new HashSet<string>();
        int index = 0;
        foreach (string mapId in mapIds)
        {
            if (!IsValid(mapId))
            {
                problem = $"{index}번 항목의 기본 맵 ID가 형식에 맞지 않습니다: '{mapId}'";
                return true;
            }
            if (!seen.Add(mapId))
            {
                problem = $"기본 맵 ID가 중복되었습니다: '{mapId}'";
                return true;
            }

            index++;
        }

        return false;
    }
}
