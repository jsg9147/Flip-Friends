using System;
using UnityEngine;

// 테스트 플레이로 씬을 떠났다 돌아와도 저장하지 않은 편집 상태를 잃지 않도록 맡아 둔다.
public static class MapEditorDraftStore
{
    private static string draftJson;
    private static string draftFileName;

    public static bool HasDraft => draftJson != null;

    public static void Store(MapData data, string fileName)
    {
        if (data == null) throw new ArgumentNullException(nameof(data));

        // 복사본으로 보관해야 테스트 플레이 쪽 처리나 씬 정리가 보관 데이터를 바꾸지 못한다.
        draftJson = JsonUtility.ToJson(data);
        draftFileName = fileName;
    }

    public static bool TryTake(out MapData data, out string fileName)
    {
        data = HasDraft ? JsonUtility.FromJson<MapData>(draftJson) : null;
        fileName = data != null ? draftFileName : null;
        Clear();
        return data != null;
    }

    public static void Clear()
    {
        draftJson = null;
        draftFileName = null;
    }
}
