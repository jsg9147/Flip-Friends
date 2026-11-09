using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Photon.Deterministic;

namespace FlipFriends.Maps
{
    // 원본 CannonShooter 값을 60Hz FP로 바꾼다. 원본 값은 초 단위이고 50Hz 물리와 무관하게 실시간으로 예약되므로
    // 지연·간격은 초×60 FP 틱, 탄속은 초당 속도로 그대로 둔다(틱당 변위는 시스템이 DeltaTime을 곱한다).
    public static class QuantumCannonConverter
    {
        public const int TicksPerSecond = 60;
        private const int MaxCannons = 128;
        // 원본 MuzzleOffset.
        private static readonly FP MuzzleOffset = 1;
        private const float MaxSeconds = 60f;

        public static void Add(QuantumMapConversion result, List<string> lines, QuantumOfficialStageData data)
        {
            var sources = data.cannons ?? Array.Empty<QuantumCannonSource>();
            string stageId = data.stageId;
            if (sources.Length > MaxCannons) throw new InvalidOperationException($"공식 스테이지 {stageId}: 대포 {MaxCannons}개 상한을 초과했습니다.");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            int alive = 0;
            foreach (var source in sources.OrderBy(s => s?.id, StringComparer.Ordinal))
            {
                string at = $"공식 스테이지 {stageId}: 대포 {source?.id} 위치={source?.origin}";
                if (source == null || string.IsNullOrWhiteSpace(source.id) || !ids.Add(source.id)) throw new InvalidOperationException(at + ": 대포 ID가 없거나 중복입니다.");
                // 원본 Cannon.prefab은 몸체 자체가 Trap 트리거다. 같은 ID의 정적 트랩이 몸체 판정이며 이동 장치 위 대포는 원본에 없어 지원하지 않는다.
                if (!result.Traps.Any(t => t.Id == source.id) || (data.motions ?? Array.Empty<QuantumMotionSource>()).Any(m => m != null && m.id == source.id))
                    throw new InvalidOperationException(at + ": 같은 오브젝트의 정적 QuantumStageTrap 몸체 판정이 필요합니다(이동 대포 미지원).");
                if (!Finite(source.origin.x) || !Finite(source.origin.y) || !Finite(source.direction.x) || !Finite(source.direction.y))
                    throw new InvalidOperationException(at + ": 위치/방향이 유한하지 않습니다.");
                if (!Finite(source.delay) || source.delay < 0 || source.delay > MaxSeconds || !Finite(source.interval) || source.interval * TicksPerSecond < 1 || source.interval > MaxSeconds)
                    throw new InvalidOperationException(at + $": 첫 발사 지연은 0~{MaxSeconds}초, 간격은 1틱(1/60초)~{MaxSeconds}초여야 합니다.");
                if (!Finite(source.speed) || source.speed <= 0 || !Finite(source.distance) || source.distance <= 0 || source.distance > 100 || !Finite(source.radius) || source.radius <= 0)
                    throw new InvalidOperationException(at + ": 탄속·최대 거리(0~100)·반지름은 0보다 커야 합니다.");
                var direction = Q(source.direction.x, source.direction.y);
                if (direction == default) throw new InvalidOperationException(at + ": 발사 방향이 0입니다.");
                FP speed = QuantumMapConverter.Q(source.speed), radius = QuantumMapConverter.Q(source.radius), distance = QuantumMapConverter.Q(source.distance);
                if (radius <= 0 || speed <= 0) throw new InvalidOperationException(at + ": FP 변환 뒤 탄속/반지름이 0입니다.");
                // 틱당 변위가 지름 이하이면 크기0 대상도 건너뛰지 않는다(원본 최대 30/초·반지름0.25 = 0.5 = 지름).
                if (speed > radius * 2 * TicksPerSecond) throw new InvalidOperationException(at + $": 탄속 {source.speed}은 틱당 변위가 탄 지름을 넘어 관통(터널링)됩니다. 최대 {source.radius * 2 * TicksPerSecond}입니다.");
                var normal = direction.Normalized;
                var config = new Quantum.FlipFriendsCannonConfig
                {
                    Id = source.id,
                    Muzzle = Q(source.origin.x, source.origin.y) + normal * MuzzleOffset,
                    Velocity = normal * speed,
                    Radius = radius,
                    MaxDistanceSquared = distance * distance,
                    DelayTicks = QuantumMapConverter.Q(source.delay * TicksPerSecond),
                    IntervalTicks = QuantumMapConverter.Q(source.interval * TicksPerSecond),
                };
                if (config.IntervalTicks < 1) throw new InvalidOperationException(at + ": FP 변환 뒤 간격이 1틱보다 짧습니다.");
                config.MaxAlive = MaxAlive(config);
                alive += config.MaxAlive;
                if (alive > Quantum.FlipFriendsCannonSystem.MaxProjectiles)
                    throw new InvalidOperationException(at + $": 대포 전체 최대 동시 탄 수가 {Quantum.FlipFriendsCannonSystem.MaxProjectiles}개를 넘습니다(누적 {alive}). 간격을 늘리거나 거리를 줄이세요.");
                result.Cannons.Add(config);
                result.CannonVisuals.Add(source);
                lines.Add($"대포1={source.id.Length}:{source.id};포구={Key(config.Muzzle)};속도={Key(config.Velocity)};반지름={Raw(radius)};거리제곱={Raw(config.MaxDistanceSquared)};지연={Raw(config.DelayTicks)};간격={Raw(config.IntervalTicks)};통과;6틱거리검사");
            }
        }

        // 제거는 발사 뒤 6의 배수 나이에서 거리가 최대를 넘은 다음 틱이다. 이동 누적 반올림을 고려해 검사 한 번만큼 여유를 둔다.
        public static int MaxAlive(Quantum.FlipFriendsCannonConfig c)
        {
            var step = c.Velocity * (FP._1 / TicksPerSecond);
            int age = Quantum.FlipFriendsCannonSystem.RangeCheckTicks;
            while ((step * age).SqrMagnitude <= c.MaxDistanceSquared)
            {
                age += Quantum.FlipFriendsCannonSystem.RangeCheckTicks;
                if (age > MaxSeconds * TicksPerSecond) throw new InvalidOperationException($"대포 {c.Id}: 탄 수명이 {MaxSeconds}초를 넘습니다. 탄속을 늘리거나 거리를 줄이세요.");
            }
            long lifeRaw = (long)(age + 1 + Quantum.FlipFriendsCannonSystem.RangeCheckTicks) << 16;
            return (int)((lifeRaw + c.IntervalTicks.RawValue - 1) / c.IntervalTicks.RawValue) + 1;
        }

        private static bool Finite(float value) => float.IsFinite(value);
        private static FPVector2 Q(float x, float y) => new(QuantumMapConverter.Q(x), QuantumMapConverter.Q(y));
        private static string Raw(FP value) => value.RawValue.ToString(CultureInfo.InvariantCulture);
        private static string Key(FPVector2 v) => Raw(v.X) + "," + Raw(v.Y);
    }
}
