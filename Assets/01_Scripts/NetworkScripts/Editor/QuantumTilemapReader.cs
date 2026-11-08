using System;
using System.Collections.Generic;
using System.Linq;
using FlipFriends.Maps;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;
using LayerMask = UnityEngine.LayerMask;

// 원본 사용례와 공식 스테이지가 같은 Tilemap 판독·거부 규칙을 쓰도록 한 곳에 둔다.
public static class QuantumTilemapReader
{
    public static QuantumTerrainSource[] ReadTerrain(TilemapCollider2D tile, string id)
    {
        var composite = tile.GetComponent<CompositeCollider2D>();
        ValidatePlane(tile.transform, id);
        if (!tile.enabled || !tile.gameObject.activeInHierarchy || !tile.CompareTag("Ground") || tile.gameObject.layer != LayerMask.NameToLayer("Ground") ||
            tile.compositeOperation != Collider2D.CompositeOperation.Merge || composite == null || !composite.enabled || composite.isTrigger ||
            composite.geometryType != CompositeCollider2D.GeometryType.Outlines || composite.edgeRadius != 0 || tile.usedByEffector || composite.usedByEffector ||
            tile.GetComponentsInChildren<Collider2D>(true).Length != 2 || tile.attachedRigidbody == null || tile.attachedRigidbody.bodyType != RigidbodyType2D.Kinematic ||
            tile.GetComponentsInChildren<MonoBehaviour>().Length != 0)
            throw new InvalidOperationException($"지형 {id} 위치={tile.transform.position}: 정적 Ground Tilemap+Kinematic Composite Outline만 지원합니다.");
        tile.ProcessTilemapChanges(); composite.GenerateGeometry();
        var group = new PhysicsShapeGroup2D(); composite.GetShapes(group);
        var paths = new SortedDictionary<string, Vector2[]>(StringComparer.Ordinal);
        for (int i = 0; i < group.shapeCount; i++)
        {
            var shape = group.GetShape(i); var local = new List<Vector2>(); group.GetShapeVertices(i, local);
            if (shape.shapeType != PhysicsShapeType2D.Edges || shape.radius != 0 || local.Count < 4 || !local[0].Equals(local[^1]))
                throw new InvalidOperationException($"지형 {id} 조각{i}: 닫힌 반경0 윤곽이 아닙니다.");
            var points = local.Take(local.Count - 1).Select(v => (Vector2)group.localToWorldMatrix.MultiplyPoint3x4(v)).ToArray();
            var candidates = new List<Vector2[]>();
            for (int start = 0; start < points.Length; start++)
            foreach (int direction in new[] { -1, 1 })
                candidates.Add(Enumerable.Range(0, points.Length).Select(k => points[(start + direction * k + points.Length) % points.Length]).ToArray());
            var canonical = candidates.OrderBy(Key, StringComparer.Ordinal).First();
            if (!paths.TryAdd(Key(canonical), canonical)) throw new InvalidOperationException($"지형 {id}: 중복 윤곽입니다.");
        }
        return paths.Select(p => new QuantumTerrainSource { id = id + "/" + QuantumMapConverter.Hash(p.Key), vertices = p.Value }).ToArray();
    }

    public static string TerrainSignature(TilemapCollider2D tile, string id) =>
        id + ";Merge;Outlines;Kinematic;Ground;반경0;" + Signature(tile.GetComponent<Tilemap>());

    public static QuantumDeviceSource ReadReset(RespawnHandler reset, string id)
    {
        if (!reset.CompareTag("Reset") || reset.resetPoint == null)
            throw new InvalidOperationException($"Reset {id} 위치={reset.transform.position}: 원본 정적 Tilemap 트리거/목적지를 확인하세요.");
        int player = 1 << LayerMask.NameToLayer("Player"), box = 1 << LayerMask.NameToLayer("Pickable");
        var source = ReadTilemapReset(reset.GetComponent<TilemapCollider2D>(), reset.resetPoint, id, (reset.targetLayers.value & player) != 0, true, reset.onlyBoxReset,
            FormattableString.Invariant($"{reset.targetLayers.value};Reset;Trigger;None;목적지참조="), () =>
            {
                if ((reset.targetLayers.value & ~(player | box)) != 0) throw new InvalidOperationException($"Reset {id}: 미지원 마스크입니다.");
            });
        return source;
    }

    public static QuantumDeviceSource ReadTilemapReset(TilemapCollider2D tile, Transform destination, string id, bool detectPlayers, bool detectBoxes, bool onlyBoxReset,
        string signaturePrefix, Action validateTargets = null)
    {
        var root = tile != null ? tile.transform : destination;
        ValidatePlane(root, id);
        if (tile == null || !tile.enabled || !tile.isTrigger || tile.compositeOperation != Collider2D.CompositeOperation.None || tile.usedByEffector ||
            destination == null || tile.GetComponentsInChildren<Collider2D>(true).Length != 1 || !tile.gameObject.activeInHierarchy ||
            tile.attachedRigidbody != null && tile.attachedRigidbody.bodyType != RigidbodyType2D.Kinematic)
            throw new InvalidOperationException($"Reset {id} 위치={root.position}: 원본 정적 Tilemap 트리거/목적지를 확인하세요.");
        validateTargets?.Invoke();
        tile.ProcessTilemapChanges(); var group = new PhysicsShapeGroup2D(); tile.GetShapes(group);
        if (group.shapeCount == 0 || group.shapeCount > 256) throw new InvalidOperationException($"Reset {id}: 조각 수 제한입니다.");
        var centers = new List<Vector2>(); var sizes = new List<Vector2>();
        for (int i = 0; i < group.shapeCount; i++)
        {
            var shape = group.GetShape(i); var v = new List<Vector2>(); group.GetShapeVertices(i, v);
            var points = v.Select(p => (Vector2)group.localToWorldMatrix.MultiplyPoint3x4(p)).ToArray();
            var min = new Vector2(points.Min(p => p.x), points.Min(p => p.y)); var max = new Vector2(points.Max(p => p.x), points.Max(p => p.y));
            if (shape.shapeType != PhysicsShapeType2D.Polygon || shape.radius != 0 || points.Length != 4 || points.Distinct().Count() != 4 ||
                points.Any(p => (p.x != min.x && p.x != max.x) || (p.y != min.y && p.y != max.y)))
                throw new InvalidOperationException($"Reset {id} 조각{i} 위치={min}: 축 정렬 사각 Polygon만 지원합니다.");
            centers.Add((min + max) / 2); sizes.Add(max - min);
        }
        var renderer = tile.GetComponent<TilemapRenderer>();
        if (renderer == null || !renderer.enabled) throw new InvalidOperationException($"Reset {id}: 렌더러 누락입니다.");
        return new QuantumDeviceSource { id = id, kind = 1, center = centers[0], size = sizes[0], contactCenters = centers.ToArray(), contactSizes = sizes.ToArray(),
            destination = destination.position, detectPlayers = detectPlayers, detectBoxes = detectBoxes,
            onlyBoxReset = onlyBoxReset, renderCenter = renderer.bounds.center, renderSize = renderer.bounds.size,
            authoringSignature = signaturePrefix + AnimationUtility.CalculateTransformPath(destination, tile.transform.root) + ";" + Signature(tile.GetComponent<Tilemap>()) };
    }

    public static string Key(Vector2[] points) => string.Join(";", points.Select(p => FormattableString.Invariant($"{QuantumMapConverter.Q(p.x).RawValue},{QuantumMapConverter.Q(p.y).RawValue}")));

    public static string Signature(Tilemap tilemap)
    {
        var values = new List<string>();
        foreach (var cell in tilemap.cellBounds.allPositionsWithin)
        {
            var tile = tilemap.GetTile(cell); if (tile == null) continue;
            values.Add(FormattableString.Invariant($"{cell.x},{cell.y},{cell.z}:") + AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(tile)) + ":" + tilemap.GetColliderType(cell));
        }
        var matrix = tilemap.transform.localToWorldMatrix;
        var tileCollider = tilemap.GetComponent<TilemapCollider2D>(); var composite = tilemap.GetComponent<CompositeCollider2D>();
        string settings = FormattableString.Invariant($"오프셋={tileCollider.offset.x:R},{tileCollider.offset.y:R};합성={tileCollider.compositeOperation};돌출={tileCollider.extrusionFactor:R};");
        if (composite != null) settings += FormattableString.Invariant($"윤곽={composite.geometryType};꼭짓점거리={composite.vertexDistance:R};오프셋거리={composite.offsetDistance:R};반경={composite.edgeRadius:R};");
        return QuantumMapConverter.Hash(settings + string.Join(",", Enumerable.Range(0, 16).Select(i => QuantumMapConverter.Q(matrix[i]).RawValue.ToString(System.Globalization.CultureInfo.InvariantCulture))) +
            ";" + string.Join(";", values.OrderBy(v => v, StringComparer.Ordinal)));
    }

    public static void ValidatePlane(Transform transform, string id)
    {
        if (transform.position.z != 0 || transform.eulerAngles.x != 0 || transform.eulerAngles.y != 0 || transform.lossyScale.z != 1)
            throw new InvalidOperationException($"{id} 위치={transform.position}: XY 평면만 지원합니다.");
    }
}
