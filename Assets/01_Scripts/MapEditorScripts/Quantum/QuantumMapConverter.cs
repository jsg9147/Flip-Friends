using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Photon.Deterministic;
using UnityEngine;

namespace FlipFriends.Maps
{
    public sealed class QuantumMapConversion
    {
        public readonly List<QuantumMapBox> Boxes = new();
        public readonly List<QuantumMapBox> Ropes = new();
        public readonly List<FPVector2> Spawns = new();
        public string CanonicalContent;
        public string ContentHash => QuantumMapConverter.Hash(CanonicalContent);
    }

    [Serializable]
    public struct QuantumMapBox
    {
        public FPVector2 Center;
        public FPVector2 Size;
    }

    public static class QuantumMapConverter
    {
        public const int ConversionVersion = 2;
        public const int PaletteVersion = 2;
        public const string Ground = "terrain.ground.basic";
        public const string Spawn = "essential.player_spawn";
        public const string Finish = "essential.finish";
        public const string Rope = "terrain.rope";

        public static QuantumMapConversion Convert(MapData data, MapEditorPalette palette)
        {
            if (data?.objects?.Count > 2048)
                throw new InvalidOperationException("변환 개수 제한(2048)을 초과했습니다.");
            var report = new MapDataValidator(palette).Validate(data);
            if (report.HasErrors)
                throw new InvalidOperationException(string.Join("\n", report.Issues.Select(issue =>
                    issue.ObjectIndex >= 0 && issue.ObjectIndex < data.objects.Count
                        ? Describe(data.objects[issue.ObjectIndex], issue.ObjectIndex) + issue.Message : issue.Message)));
            if (data.version != MapData.CurrentVersion || data.objects.Count > 2048)
                throw new InvalidOperationException("맵 스키마 또는 변환 개수 제한(2048)을 확인하세요.");
            var result = new QuantumMapConversion();
            var ordered = new SortedDictionary<string, List<PlacedObjectData>>(StringComparer.Ordinal);
            for (int index = 0; index < data.objects.Count; index++)
            {
                var item = data.objects[index];
                try
                {
                    if (item.prefabID != Ground && item.prefabID != Spawn && item.prefabID != Finish && item.prefabID != Rope)
                        throw new InvalidOperationException("지원하지 않는 장치/팔레트입니다.");
                    ValidatePrefab(palette.GetPrefab(item.prefabID), item.prefabID);
                    if (item.position == null || item.scale == null) throw new InvalidOperationException("변환 값이 없습니다.");
                    var p = item.position.ToVector3();
                    var s = item.scale.ToVector3();
                    if (p.z != 0 || s.z != 1 || Math.Abs(s.x) < 0.01f || Math.Abs(s.y) < 0.01f ||
                        Math.Abs(s.x) > 128 || Math.Abs(s.y) > 128)
                        throw new InvalidOperationException("XY 평면·0이 아닌 크기(0.01~128)·Z 스케일 1만 지원합니다.");
                    if (!float.IsFinite(item.rotation) || item.rotation % 90 != 0 || Math.Abs(item.rotation) > 3600)
                        throw new InvalidOperationException("회전은 90도 단위(-3600~3600)만 지원합니다.");
                    // 수직 등반만 검증한 단계이므로 가로 로프를 지원하는 것으로 오해하지 않게 거부한다.
                    if (item.prefabID == Rope && item.rotation % 180 != 0)
                        throw new InvalidOperationException("로프는 수직 배치(180도 단위)만 지원합니다.");
                    string key = item.prefabID + ":" + string.Join(",", new[] { Q(p.x), Q(p.y), Q(s.x), Q(s.y), Q(item.rotation) }.Select(Raw));
                    if (!ordered.TryGetValue(key, out var group)) ordered[key] = group = new();
                    group.Add(item);
                }
                catch (Exception error)
                {
                    throw new InvalidOperationException(Describe(item, index) + error.Message, error);
                }
            }
            var lines = new List<string> { $"변환={ConversionVersion};팔레트={PaletteVersion};스키마={data.version};맵={data.mapId}" };
            foreach (var pair in ordered)
            foreach (var item in pair.Value)
            {
                try
                {
                    lines.Add(pair.Key);
                    var center = new FPVector2(Q(item.position.x), Q(item.position.y));
                    if (item.prefabID == Spawn) result.Spawns.Add(center);
                    if (item.prefabID != Ground && item.prefabID != Rope) continue;
                    var box = palette.GetPrefab(item.prefabID).GetComponent<BoxCollider2D>();
                    var scale = new FPVector2(Q(item.scale.x), Q(item.scale.y));
                    var offset = new FPVector2(Q(box.offset.x) * scale.X, Q(box.offset.y) * scale.Y);
                    var size = new FPVector2(Q(box.size.x) * FPMath.Abs(scale.X), Q(box.size.y) * FPMath.Abs(scale.Y));
                    int turn = ((int)(item.rotation / 90) % 4 + 4) % 4;
                    for (int i = 0; i < turn; i++) offset = new FPVector2(-offset.Y, offset.X);
                    if (turn % 2 == 1) size = new FPVector2(size.Y, size.X);
                    // SDK는 반너비를 저장하므로 전체 크기를 2 원시 단위 격자에 맞춘다.
                    size.X = FP.FromRaw((size.X.RawValue + 1) / 2 * 2);
                    size.Y = FP.FromRaw((size.Y.RawValue + 1) / 2 * 2);
                    if (size.X <= 0 || size.Y <= 0)
                        throw new InvalidOperationException($"{pair.Key}: 고정소수점 변환 후 콜라이더 크기가 0입니다.");
                    center += offset;
                    if (Q(center.X.AsFloat) != center.X || Q(center.Y.AsFloat) != center.Y ||
                        Q(size.X.AsFloat) != size.X || Q(size.Y.AsFloat) != size.Y)
                        throw new InvalidOperationException($"{pair.Key}: 위치·크기를 Unity 베이크에 정확하게 표현할 수 없습니다.");
                    var converted = new QuantumMapBox { Center = center, Size = size };
                    if (item.prefabID == Rope) result.Ropes.Add(converted);
                    else result.Boxes.Add(converted);
                    lines.Add($"{(item.prefabID == Rope ? "로프" : "박스")}={Raw(center.X)},{Raw(center.Y)},{Raw(size.X)},{Raw(size.Y)}");
                }
                catch (Exception error)
                {
                    throw new InvalidOperationException(Describe(item, data.objects.IndexOf(item)) + error.Message, error);
                }
            }
            if (result.Spawns.Count != 2 || result.Boxes.Count == 0)
                throw new InvalidOperationException("독립 시험은 시작점 정확히 2개와 정적 지형이 필요합니다.");
            foreach (var spawn in result.Spawns)
            foreach (var box in result.Boxes)
                if (FPMath.Abs(spawn.X - box.Center.X) < box.Size.X / 2 + Q(0.4f) &&
                    FPMath.Abs(spawn.Y - box.Center.Y) < box.Size.Y / 2 + Q(0.5f))
                    throw new InvalidOperationException("시작점의 플레이어 크기가 정적 지형과 겹칩니다.");
            result.CanonicalContent = string.Join("\n", lines);
            return result;
        }

        private static void ValidatePrefab(GameObject prefab, string id)
        {
            if (prefab == null || !prefab.activeSelf) throw new InvalidOperationException("활성 프리팹이 없습니다.");
            var allowed = new HashSet<Type> { typeof(Transform), typeof(SpriteRenderer), typeof(Mirror.NetworkIdentity) };
            if (id == Rope) allowed.Add(typeof(Mirror.NetworkTransformReliable));
            if (id == Spawn) { allowed.Add(typeof(Mirror.NetworkStartPosition)); allowed.Add(typeof(PlayerSpawnPoint)); }
            else allowed.Add(typeof(BoxCollider2D));
            foreach (var component in prefab.GetComponentsInChildren<Component>(true))
                if (component == null || !allowed.Contains(component.GetType()))
                    throw new InvalidOperationException($"미지원 컴포넌트: {component?.name}/{component?.GetType().Name}");
            var colliders = prefab.GetComponentsInChildren<Collider2D>(true);
            if (id == Spawn && colliders.Length != 0) throw new InvalidOperationException("시작점의 콜라이더는 지원하지 않습니다.");
            if (id == Spawn) return;
            if (colliders.Length != 1 || colliders[0] is not BoxCollider2D box || box.transform != prefab.transform ||
                !box.enabled || box.isTrigger != (id == Finish || id == Rope) || box.edgeRadius != 0 ||
                box.compositeOperation != Collider2D.CompositeOperation.None || box.size.x <= 0 || box.size.y <= 0)
                throw new InvalidOperationException("루트의 단일 활성 박스만 지원합니다. 지형 트리거·둥근 모서리·합성은 거부합니다.");
            if (id == Rope && (!prefab.CompareTag("Rope") || prefab.transform.localRotation != Quaternion.identity ||
                prefab.transform.childCount != 0 || box.usedByEffector || prefab.GetComponent<Rigidbody2D>() != null))
                throw new InvalidOperationException("로프는 Rope 태그의 자식 없는 정적 루트 박스 트리거만 지원합니다.");
        }

        // 작성 단계의 float를 16비트 소수로 반올림한 뒤 프레임에는 정수 원시 값만 넘긴다.
        public static FP Q(float value)
        {
            if (!float.IsFinite(value) || Math.Abs(value) > 4096) throw new InvalidOperationException("유한 범위(-4096~4096)를 벗어났습니다.");
            return FP.FromRaw(checked((long)Math.Round((double)value * 65536, MidpointRounding.AwayFromZero)));
        }

        private static string Raw(FP value) => value.RawValue.ToString(CultureInfo.InvariantCulture);
        private static string Describe(PlacedObjectData item, int index) =>
            $"오브젝트 #{index + 1} {item?.prefabID} 위치=({item?.position?.x}, {item?.position?.y}): ";
        public static string Hash(string text)
        {
            using var sha = SHA256.Create();
            return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(text))).Replace("-", "").ToLowerInvariant();
        }
    }
}
