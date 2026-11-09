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
        public const int SwitchFormatVersion = 2;

        public static int FormatFor(QuantumOfficialStageData data) =>
            data?.switches?.Length > 0 ? SwitchFormatVersion : FormatVersion;
        private const int MaxGoals = 16;
        private const int MaxRopes = 64;
        private const int MaxBoxes = 64;
        private const int MaxTraps = 128;
        private const int MaxLaunchers = 128;
        private const int MaxTrapVertices = 8;
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
            var lines = new List<string> { $"공식={FormatFor(data)};스테이지={data.stageId}" };
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
            AddTraps(result, lines, data.traps, data.stageId, data.motions);
            AddLaunchers(result, lines, data.launchers, data.stageId);
            QuantumMotionConverter.Add(result, lines, data);
            var boundary = new FallBoundary(data.renderBounds);
            result.MinX = QuantumMapConverter.Q(boundary.MinX); result.MaxX = QuantumMapConverter.Q(boundary.MaxX); result.MinY = QuantumMapConverter.Q(boundary.MinY);
            lines.Add($"복귀={Raw(result.MinX)},{Raw(result.MaxX)},{Raw(result.MinY)};여백10;위쪽제외");
            QuantumMapConverter.ValidateBoxesAgainstSpawns(result);
            QuantumMapConverter.ValidateRespawnPlacement(result);
            result.CanonicalContent = string.Join("\n", lines);
            // 장치 목적지를 지형과 대조하려면 장치를 먼저 변환해야 한다(MapData 경로와 같은 순서).
            if (devices != null) devices.Apply(result);
            terrain.Apply(result);
            QuantumMotionConverter.ValidatePlacement(result, data.stageId);
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

        // 트랩은 ID(프리팹 경로) 순으로 정렬한다. 같은 틱에 여러 개에 닿으면 이 순서의 첫 트랩만 적용된다.
        private static void AddTraps(QuantumMapConversion result, List<string> lines, QuantumTrapSource[] traps, string stageId, QuantumMotionSource[] motions)
        {
            if ((traps?.Length ?? 0) > MaxTraps) throw new InvalidOperationException($"공식 스테이지 {stageId}: 트랩 {MaxTraps}개 상한을 초과했습니다.");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var source in (traps ?? Array.Empty<QuantumTrapSource>()).OrderBy(s => s?.id, StringComparer.Ordinal))
            {
                string at = $"공식 스테이지 {stageId}: 트랩 {source?.id} 위치={source?.origin}";
                if (source == null || string.IsNullOrWhiteSpace(source.id) || !ids.Add(source.id)) throw new InvalidOperationException(at + ": 안정적인 트랩 ID가 없거나 중복입니다.");
                if (!Finite(source.origin) || !Finite(source.direction)) throw new InvalidOperationException(at + ": 기준점/넉백 방향이 유한하지 않습니다.");
                bool isCircle = source.radius > 0;
                if (!float.IsFinite(source.radius) || source.radius < 0 || !Finite(source.center) ||
                    source.vertices == null || (isCircle ? source.vertices.Length != 0 : source.vertices.Length < 3 || source.vertices.Length > MaxTrapVertices || !source.vertices.All(Finite)))
                    throw new InvalidOperationException(at + $": 판정 꼭짓점은 유한한 3~{MaxTrapVertices}개여야 합니다.");
                if (source.renderSize.x <= 0 || source.renderSize.y <= 0) throw new InvalidOperationException(at + ": 표시할 렌더러 범위가 없습니다.");
                var vertices = isCircle ? Array.Empty<FPVector2>() : ConvexVertices(source.vertices, at);
                var config = new Quantum.FlipFriendsTrapConfig { Id = source.id, Origin = Q(source.origin), Direction = Q(source.direction), Vertices = vertices, Center = Q(source.center), Radius = QuantumMapConverter.Q(source.radius) };
                if (isCircle && (config.Radius <= 0 || !(motions ?? Array.Empty<QuantumMotionSource>()).Any(m => m != null && m.id == source.id && m.kind >= 1 && m.kind <= 3))) throw new InvalidOperationException(at + ": 이동 원형 트랩 참조/FP 반지름 오류");
                result.Traps.Add(config);
                result.TrapVisuals.Add(source);
                if (isCircle) lines.Add($"이동원형트랩1={source.id};원={Key(config.Center)}/{Raw(config.Radius)}");
                lines.Add($"트랩={source.id.Length}:{source.id};기준={Key(config.Origin)};방향={Key(config.Direction)};꼭짓점={string.Join("/", vertices.Select(Key))};표시={Key(Q(source.renderCenter))}/{Key(Q(source.renderSize))}");
            }
        }

        private static FPVector2[] ConvexVertices(UnityEngine.Vector2[] source, string at)
        {
            var vertices = source.Select(Q).ToArray();
            long area = 0;
            for (int i = 0; i < vertices.Length; i++) area += Cross(vertices[i], vertices[(i + 1) % vertices.Length], default);
            if (area == 0) throw new InvalidOperationException(at + ": 판정 넓이가 0입니다.");
            if (area < 0) Array.Reverse(vertices);
            // FP 양자화 뒤에도 모든 모서리가 같은 쪽으로 꺾여야 분리축 검사가 원본 형상과 같다. 일직선 꼭짓점도 거부한다.
            for (int i = 0; i < vertices.Length; i++)
                if (Cross(vertices[(i + 1) % vertices.Length], vertices[(i + 2) % vertices.Length], vertices[i]) <= 0)
                    throw new InvalidOperationException(at + $": 꼭짓점 {i + 1}에서 오목하거나 일직선입니다. 볼록 다각형만 지원합니다.");
            return vertices;
        }

        private static void AddLaunchers(QuantumMapConversion result, List<string> lines, QuantumLauncherSource[] sources, string stageId)
        {
            if ((sources?.Length ?? 0) > MaxLaunchers) throw new InvalidOperationException($"공식 스테이지 {stageId}: 스프링/바운스 {MaxLaunchers}개 상한을 초과했습니다.");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var source in (sources ?? Array.Empty<QuantumLauncherSource>()).OrderBy(s => s?.id, StringComparer.Ordinal))
            {
                string at = $"공식 스테이지 {stageId}: 스프링/바운스 {source?.id} 위치={source?.origin}";
                if (source == null || string.IsNullOrWhiteSpace(source.id) || !ids.Add(source.id)) throw new InvalidOperationException(at + ": ID가 없거나 중복입니다.");
                if (source.kind < 0 || source.kind > 1 || !Finite(source.origin) || !Finite(source.center) || !float.IsFinite(source.radius) || source.radius < 0)
                    throw new InvalidOperationException(at + ": 종류/기준점/원형 판정이 유효하지 않습니다.");
                if (!Finite(source.renderCenter) || !Finite(source.renderSize) || source.renderSize.x <= 0 || source.renderSize.y <= 0)
                    throw new InvalidOperationException(at + ": 표시할 렌더러 범위가 없습니다.");
                FP radius = QuantumMapConverter.Q(source.radius);
                FPVector2[] vertices;
                if (source.radius > 0)
                {
                    if (radius <= 0 || (source.vertices?.Length ?? 0) != 0) throw new InvalidOperationException(at + ": 원 반지름이 FP에서 0이거나 다각형과 중복입니다.");
                    vertices = Array.Empty<FPVector2>();
                }
                else
                {
                    if (source.vertices == null || source.vertices.Length < 3 || source.vertices.Length > MaxTrapVertices || !source.vertices.All(Finite))
                        throw new InvalidOperationException(at + ": 판정 꼭짓점은 유한한 3~8개여야 합니다.");
                    vertices = ConvexVertices(source.vertices, at);
                }
                var config = new Quantum.FlipFriendsLauncherConfig { Id = source.id, Kind = source.kind, Origin = Q(source.origin), Center = Q(source.center), Radius = radius, Vertices = vertices };
                result.Launchers.Add(config); result.LauncherVisuals.Add(source);
                lines.Add($"발사={source.id.Length}:{source.id};종류={source.kind};기준={Key(config.Origin)};원={Key(config.Center)}/{Raw(radius)};꼭짓점={string.Join("/", vertices.Select(Key))};표시={Key(Q(source.renderCenter))}/{Key(Q(source.renderSize))}");
            }
        }

        // 원점 기준 외적을 raw 정수로 계산한다. 좌표 상한(작성 범위)에서 long을 넘지 않는다.
        private static long Cross(FPVector2 a, FPVector2 b, FPVector2 origin)
        {
            long ax = a.X.RawValue - origin.X.RawValue, ay = a.Y.RawValue - origin.Y.RawValue;
            long bx = b.X.RawValue - origin.X.RawValue, by = b.Y.RawValue - origin.Y.RawValue;
            return checked(ax * by - ay * bx);
        }

        private static bool Finite(UnityEngine.Vector2 v) => float.IsFinite(v.x) && float.IsFinite(v.y);
        private static FPVector2 Q(UnityEngine.Vector2 v) => new(QuantumMapConverter.Q(v.x), QuantumMapConverter.Q(v.y));
        private static string Raw(FP value) => value.RawValue.ToString(CultureInfo.InvariantCulture);
        private static string Key(FPVector2 v) => Raw(v.X) + "," + Raw(v.Y);
    }
}
