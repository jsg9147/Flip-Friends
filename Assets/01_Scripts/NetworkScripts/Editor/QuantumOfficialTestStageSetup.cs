using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

// 공식 스테이지 경로 검증용 작은 프리팹. 일부러 MapData 배치 범위(x−50~50) 밖에 두어 공식 경로가 그 규칙을 거치지 않음을 확인한다.
// 생성기는 원본 Stage 15 SavePoint·Box Switch ObjSummonSwitch의 스프라이트를 읽었으므로 Mirror 제거와 함께 지웠다.
// 커밋된 프리팹이 기준이다.
public static class QuantumOfficialTestStageSetup
{
    public const string PrefabPath = "Assets/03_Prefabs/OfficialStages/Official Test Stage.prefab";
    public const string StageId = "official-test";

    internal static TileBase FirstTile(string prefabPath, string path)
    {
        var map = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath).transform.Find(path).GetComponent<Tilemap>();
        foreach (var cell in map.cellBounds.allPositionsWithin) { var tile = map.GetTile(cell); if (tile != null) return tile; }
        throw new InvalidOperationException($"{prefabPath}/{path}: 타일이 없습니다.");
    }

    internal static void Paint(Tilemap map, TileBase tile, int x, int y) => map.SetTile(new Vector3Int(x, y, 0), tile);

    internal static GameObject Child(GameObject parent, string name)
    {
        var child = new GameObject(name); child.transform.SetParent(parent.transform, false); return child;
    }

    internal static GameObject Point(GameObject root, string name, float x, float y)
    {
        var point = Child(root, name); point.transform.position = new Vector3(x, y, 0); return point;
    }

    internal static void Set(UnityEngine.Object target, string field, object value)
    {
        var serialized = new SerializedObject(target); var property = serialized.FindProperty(field);
        switch (value)
        {
            case string text: property.stringValue = text; break;
            case int number: property.intValue = number; break;
            case float number: property.floatValue = number; break;
            case bool flag: property.boolValue = flag; break;
            case Vector2 vector: property.vector2Value = vector; break;
            case UnityEngine.Object reference: property.objectReferenceValue = reference; break;
            default: throw new ArgumentException(field);
        }
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
}
