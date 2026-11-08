using System;
using System.Collections.Generic;
using System.Linq;
using Photon.Deterministic;
using Quantum;
using UnityEngine;

namespace FlipFriends.Maps
{
    [Serializable]
    public sealed class QuantumTerrainSource
    {
        public string id;
        public Vector2[] vertices;
    }

    public sealed class QuantumMapTerrain : ScriptableObject
    {
        [SerializeField] private QuantumTerrainSource[] contours;
        [SerializeField] private Vector2 renderCenter, renderSize;
        [SerializeField] private string authoringSignature;
        public QuantumTerrainSource[] Sources => contours;
        public void Configure(QuantumTerrainSource[] value, Bounds bounds, string signature)
        { contours = value; renderCenter = bounds.center; renderSize = bounds.size; authoringSignature = signature; }

        public void Apply(QuantumMapConversion conversion)
        {
            if (contours == null || contours.Length == 0 || contours.Length > 64 || contours.Any(c => c?.vertices == null) ||
                contours.Sum(c => c.vertices.Length) > 512 || string.IsNullOrWhiteSpace(authoringSignature))
                throw new InvalidOperationException("지형 윤곽/작성 서명 누락 또는 상한(64윤곽/512변) 초과입니다.");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var converted = new List<FlipFriendsTerrainContour>(); var lines = new List<string>();
            foreach (var source in contours.OrderBy(c => c.id, StringComparer.Ordinal))
            {
                if (string.IsNullOrWhiteSpace(source.id) || !ids.Add(source.id) || source.vertices.Length < 3)
                    throw new InvalidOperationException($"지형 {source.id}: 누락/중복 ID 또는 짧은 윤곽입니다.");
                var vertices = source.vertices.Select(Q).ToArray();
                if (vertices.Distinct().Count() != vertices.Length) throw new InvalidOperationException($"지형 {source.id}: 중복 꼭짓점입니다.");
                for (int i = 0; i < vertices.Length; i++)
                {
                    var d = vertices[(i + 1) % vertices.Length] - vertices[i];
                    // Composite가 생성한 미세 기울기는 그대로 보존하고 본격적인 경사는 별도 이동 이식까지 거부한다.
                    if (FPMath.Min(FPMath.Abs(d.X), FPMath.Abs(d.Y)) > FP.FromRaw(8))
                        throw new InvalidOperationException($"지형 {source.id} 위치={vertices[i]}: 경사 이동 미지원입니다.");
                }
                FP area = 0;
                for (int i = 0; i < vertices.Length; i++) area += Cross(vertices[i], vertices[(i + 1) % vertices.Length]);
                if (area == 0) throw new InvalidOperationException($"지형 {source.id}: 면적이 없는 윤곽입니다.");
                converted.Add(new FlipFriendsTerrainContour { Id = source.id, Vertices = vertices });
                lines.Add(source.id + "=" + string.Join(";", vertices.Select(Key)));
            }
            var rc = Q(renderCenter); var rs = Q(renderSize);
            if (rs.X <= 0 || rs.Y <= 0 || converted.SelectMany(c => c.Vertices).Any(v =>
                FPMath.Abs(v.X - rc.X) > rs.X / 2 + FP.FromRaw(8) || FPMath.Abs(v.Y - rc.Y) > rs.Y / 2 + FP.FromRaw(8)))
                throw new InvalidOperationException("지형 판정이 렌더러 범위를 벗어났습니다.");
            var value = converted.ToArray();
            for (int a = 0; a < value.Length; a++)
            for (int b = a; b < value.Length; b++)
            for (int i = 0; i < value[a].Vertices.Length; i++)
            for (int j = 0; j < value[b].Vertices.Length; j++)
            {
                var va = value[a].Vertices; var vb = value[b].Vertices;
                if (a == b && (i == j || (i + 1) % va.Length == j || (j + 1) % va.Length == i)) continue;
                if (Intersects(va[i], va[(i + 1) % va.Length], vb[j], vb[(j + 1) % vb.Length]))
                    throw new InvalidOperationException($"지형 {value[a].Id}/{value[b].Id} 위치={va[i]}: 교차/접촉 윤곽은 지원하지 않습니다.");
            }
            foreach (var spawn in conversion.Spawns)
                if (FlipFriendsTerrain.Overlaps(value, spawn, new FPVector2(QuantumMapConverter.Q(0.4f), QuantumMapConverter.Q(0.5f))))
                    throw new InvalidOperationException("지형과 플레이어 시작점이 겹칩니다.");
            foreach (var box in conversion.MovableBoxes)
                if (FlipFriendsTerrain.Overlaps(value, box.Center, box.Size / 2)) throw new InvalidOperationException("지형과 상자 초기 위치가 겹칩니다.");
            foreach (var device in conversion.Devices)
            {
                var half = new FPVector2(QuantumMapConverter.Q(0.4f), QuantumMapConverter.Q(0.5f));
                if (device.Kind == 2) half = conversion.MovableBoxes[device.TargetBoxIndex].Size / 2;
                else if (device.Kind == 1 && device.DetectBoxes)
                    foreach (var box in conversion.MovableBoxes) { half.X = FPMath.Max(half.X, box.Size.X / 2); half.Y = FPMath.Max(half.Y, box.Size.Y / 2); }
                if (FlipFriendsTerrain.Overlaps(value, device.Destination, half))
                    throw new InvalidOperationException($"지형과 장치 {device.Id} 목적지가 겹칩니다.");
            }
            conversion.MinX = FPMath.Min(conversion.MinX, rc.X - rs.X / 2 - 10);
            conversion.MaxX = FPMath.Max(conversion.MaxX, rc.X + rs.X / 2 + 10);
            conversion.MinY = FPMath.Min(conversion.MinY, rc.Y - rs.Y / 2 - 10);
            conversion.TerrainContours = value; conversion.HasTerrain = true;
            conversion.CanonicalContent = conversion.CanonicalContent.Replace("변환=5;팔레트=5;", "변환=6;팔레트=6;") + "\n정적윤곽=" + authoringSignature + "\n" + string.Join("\n", lines) +
                FormattableString.Invariant($"\n지형표시={Key(rc)}/{Key(rs)};경계={conversion.MinX.RawValue},{conversion.MaxX.RawValue},{conversion.MinY.RawValue}");
        }
        private static FPVector2 Q(Vector2 v) => new(QuantumMapConverter.Q(v.x), QuantumMapConverter.Q(v.y));
        private static FP Cross(FPVector2 a, FPVector2 b) => a.X * b.Y - a.Y * b.X;
        private static bool Intersects(FPVector2 a, FPVector2 b, FPVector2 c, FPVector2 d)
        {
            if (FPMath.Max(a.X, b.X) < FPMath.Min(c.X, d.X) || FPMath.Max(c.X, d.X) < FPMath.Min(a.X, b.X) ||
                FPMath.Max(a.Y, b.Y) < FPMath.Min(c.Y, d.Y) || FPMath.Max(c.Y, d.Y) < FPMath.Min(a.Y, b.Y)) return false;
            var ab = b - a; var cd = d - c;
            var s0 = FPMath.Sign(Cross(ab, c - a)); var s1 = FPMath.Sign(Cross(ab, d - a));
            var s2 = FPMath.Sign(Cross(cd, a - c)); var s3 = FPMath.Sign(Cross(cd, b - c));
            return s0 * s1 <= 0 && s2 * s3 <= 0;
        }
        private static string Key(FPVector2 v) => FormattableString.Invariant($"{v.X.RawValue},{v.Y.RawValue}");
    }
}
