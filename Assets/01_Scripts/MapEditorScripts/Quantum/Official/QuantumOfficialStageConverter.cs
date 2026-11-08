using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Photon.Deterministic;

namespace FlipFriends.Maps
{
    // 공식 스테이지는 MapData 배치 범위 규칙을 거치지 않고, 같은 FP 변환·초기 배치·장치·지형 검증만 공유한다.
    public static class QuantumOfficialStageConverter
    {
        public const int FormatVersion = 1;
        private const int MaxGoals = 16;
        private const int MaxRopes = 64;
        private const int MaxBoxes = 64;
        private static readonly Regex StageIdPattern = new("^[a-z0-9][a-z0-9-]{0,47}$");

        public static bool IsValidStageId(string id) => id != null && StageIdPattern.IsMatch(id);

        public static QuantumMapConversion Convert(QuantumOfficialStageData data, QuantumMapTerrain terrain, QuantumMapDevices devices)
        {
            if (data == null || !IsValidStageId(data.stageId))
                throw new InvalidOperationException("공식 스테이지 ID는 소문자·숫자·하이픈(1~48자)이어야 합니다.");
            if (terrain == null) throw new InvalidOperationException($"공식 스테이지 {data.stageId}: Ground Tilemap 지형이 필요합니다.");
            if (data.spawns == null || data.spawns.Length != 2)
                throw new InvalidOperationException($"공식 스테이지 {data.stageId}: 현재 2인 전제라 시작점이 정확히 2개여야 합니다.");
            if (data.goals == null || data.goals.Length == 0 || data.goals.Length > MaxGoals)
                throw new InvalidOperationException($"공식 스테이지 {data.stageId}: 도착점은 1~{MaxGoals}개여야 합니다.");
            if ((data.ropes?.Length ?? 0) > MaxRopes || (data.boxes?.Length ?? 0) > MaxBoxes)
                throw new InvalidOperationException($"공식 스테이지 {data.stageId}: 로프 {MaxRopes}개·상자 {MaxBoxes}개 상한을 초과했습니다.");
            if (data.renderBounds.size.x <= 0 || data.renderBounds.size.y <= 0)
                throw new InvalidOperationException($"공식 스테이지 {data.stageId}: 추락 경계를 계산할 렌더러 범위가 없습니다.");

            var result = new QuantumMapConversion { HasRespawn = true };
            var lines = new List<string> { $"공식={FormatVersion};스테이지={data.stageId}" };
            foreach (var spawn in data.spawns.Select(Q).OrderBy(p => p.X.RawValue).ThenBy(p => p.Y.RawValue))
            {
                result.Spawns.Add(spawn);
                lines.Add("시작점=" + Key(spawn));
            }
            foreach (var goal in Areas(data.goals, "도착점", data.stageId)) { result.Goals.Add(goal); lines.Add("도착=" + Key(goal.Center) + "/" + Key(goal.Size)); }
            foreach (var rope in Areas(data.ropes, "로프", data.stageId)) { result.Ropes.Add(rope); lines.Add("로프=" + Key(rope.Center) + "/" + Key(rope.Size)); }
            var boxSize = QuantumMapConverter.Q(QuantumStageBox.Size);
            foreach (var center in (data.boxes ?? Array.Empty<UnityEngine.Vector2>()).Select(Q).OrderBy(p => p.X.RawValue).ThenBy(p => p.Y.RawValue))
            {
                var box = new QuantumMapBox { Center = center, Size = new FPVector2(boxSize, boxSize) };
                result.MovableBoxes.Add(box);
                lines.Add($"상자={Key(box.Center)}/{Key(box.Size)};중력9.8;감속1.5;낙하12");
            }
            var boundary = new FallBoundary(data.renderBounds);
            result.MinX = QuantumMapConverter.Q(boundary.MinX); result.MaxX = QuantumMapConverter.Q(boundary.MaxX); result.MinY = QuantumMapConverter.Q(boundary.MinY);
            lines.Add($"복귀={Raw(result.MinX)},{Raw(result.MaxX)},{Raw(result.MinY)};여백10;위쪽제외");
            QuantumMapConverter.ValidateBoxesAgainstSpawns(result);
            QuantumMapConverter.ValidateRespawnPlacement(result);
            result.CanonicalContent = string.Join("\n", lines);
            // 장치 목적지를 지형과 대조하려면 장치를 먼저 변환해야 한다(MapData 경로와 같은 순서).
            if (devices != null) devices.Apply(result);
            terrain.Apply(result);
            return result;
        }

        private static IEnumerable<QuantumMapBox> Areas(QuantumStageArea[] areas, string label, string stageId)
        {
            var converted = (areas ?? Array.Empty<QuantumStageArea>()).Select(a => new QuantumMapBox { Center = Q(a.center), Size = Q(a.size) })
                .OrderBy(b => Key(b.Center) + "/" + Key(b.Size), StringComparer.Ordinal).ToArray();
            if (converted.Any(b => b.Size.X <= 0 || b.Size.Y <= 0) || converted.Select(b => Key(b.Center) + "/" + Key(b.Size)).Distinct().Count() != converted.Length)
                throw new InvalidOperationException($"공식 스테이지 {stageId}: {label} 크기가 0 이하이거나 중복입니다.");
            return converted;
        }

        private static FPVector2 Q(UnityEngine.Vector2 v) => new(QuantumMapConverter.Q(v.x), QuantumMapConverter.Q(v.y));
        private static string Raw(FP value) => value.RawValue.ToString(CultureInfo.InvariantCulture);
        private static string Key(FPVector2 v) => Raw(v.X) + "," + Raw(v.Y);
    }
}
