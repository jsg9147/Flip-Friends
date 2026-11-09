using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Photon.Deterministic;
using Quantum;
using UnityEngine;

namespace FlipFriends.Maps
{
    public static class QuantumMotionConverter
    {
        public static void ValidatePlacement(QuantumMapConversion result, string stageId)
        {
            foreach (var c in result.Motions.Where(c => c.Kind == 0 || c.Kind == 4))
            {
                string at = $"{stageId}/{c.Id} 위치={c.Origin}";
                if (c.HalfSize.X <= 0 || c.HalfSize.Y <= 0 || FlipFriendsTerrain.Overlaps(result.TerrainContours, c.Origin, c.HalfSize))
                    throw new InvalidOperationException(at + ": FP 크기0 또는 고정 지형과 겹칩니다.");
                foreach (var button in result.StageSwitches)
                    if (!button.IsDoor && FlipFriendsMotionSystem.Overlap(c.Origin, c.HalfSize, button.Center, button.HalfSize))
                        throw new InvalidOperationException(at + $": 고체 스위치 {button.Id}와 겹칩니다.");
                if (c.Kind == 4)
                    foreach (var part in result.Motions)
                        if (part.Kind == 4 && part.Id != c.Id && part.ToggleGroup == c.ToggleGroup &&
                            part.InitialActive == c.InitialActive && FlipFriendsMotionSystem.Overlap(c.Origin, c.HalfSize, part.Origin, part.HalfSize))
                            throw new InvalidOperationException(at + $": 같은 위상 지형 {part.Id}와 겹칩니다.");
                if (c.Kind == 4 && !c.InitialActive) continue;
                foreach (var spawn in result.Spawns)
                    if (FlipFriendsMotionSystem.Overlap(c.Origin, c.HalfSize, spawn, new FPVector2(Q(.4f), Q(.5f)))) throw new InvalidOperationException(at + ": 플레이어 시작점과 겹칩니다.");
                foreach (var box in result.MovableBoxes)
                    if (FlipFriendsMotionSystem.Overlap(c.Origin, c.HalfSize, box.Center, box.Size / 2)) throw new InvalidOperationException(at + ": 상자 시작점과 겹칩니다.");
                foreach (var other in result.Motions)
                {
                    if (other.Id == c.Id || other.Kind != 0 && other.Kind != 4 || other.Kind == 4 && !other.InitialActive) continue;
                    if (FlipFriendsMotionSystem.Overlap(c.Origin, c.HalfSize, other.Origin, other.HalfSize)) throw new InvalidOperationException(at + ": 다른 고체 장치와 겹칩니다.");
                }
            }
            var solidButtons = result.StageSwitches.Where(c => !c.IsDoor).ToArray();
            foreach (var button in solidButtons)
            {
                bool Overlaps(FPVector2 center, FPVector2 half) =>
                    FlipFriendsMotionSystem.Overlap(button.Center, button.HalfSize, center, half);
                if (FlipFriendsTerrain.Overlaps(result.TerrainContours, button.Center, button.HalfSize) ||
                    result.Spawns.Any(p => Overlaps(p, new FPVector2(Q(.4f), Q(.5f)))) ||
                    result.MovableBoxes.Any(b => Overlaps(b.Center, b.Size / 2)) ||
                    result.MotionResets.Any(b => !b.IsTrigger && Overlaps(b.Center, b.HalfSize)) ||
                    solidButtons.Any(b => b.Id != button.Id && Overlaps(b.Center, b.HalfSize)))
                    throw new InvalidOperationException($"{stageId}/{button.Id} 위치={button.Center}: 고체 스위치가 지형/장치/시작점과 겹칩니다.");
            }
        }
        private static FP Q(float v) => QuantumMapConverter.Q(v);
        private static FPVector2 Q(Vector2 v) => new(Q(v.x), Q(v.y));
        private static bool Finite(Vector2 v) => float.IsFinite(v.x) && float.IsFinite(v.y);
        private static int Ticks(double seconds) => checked((int)Math.Ceiling(seconds * 60.0 - 0.00001));

        public static void Add(QuantumMapConversion result, List<string> lines, QuantumOfficialStageData data)
        {
            var sources = data.motions ?? Array.Empty<QuantumMotionSource>();
            if (sources.Length > 64) throw new InvalidOperationException("이동 장치는 최대64개입니다.");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var s in sources.OrderBy(s => s?.id, StringComparer.Ordinal))
            {
                string at = $"{data.stageId}/{s?.id} 위치={s?.origin}";
                void Check(bool valid, string why) { if (!valid) throw new InvalidOperationException(at + ": " + why); }
                Check(s != null && !string.IsNullOrWhiteSpace(s.id) && ids.Add(s.id), "이동 장치 ID 누락/중복");
                Check(s.kind >= 0 && s.kind <= 4 && Finite(s.origin) && Finite(s.size), "종류/위치/크기 오류");
                Check(new[] { s.speed, s.waitTime, s.returnDelay, s.easeAmount, s.initialWaitTime, s.rotationSpeed, s.randomRange }.All(float.IsFinite), "유한하지 않은 이동 설정");
                Check(s.speed >= 0 && s.speed <= 20 && s.waitTime >= 0 && s.waitTime <= 60 && s.returnDelay >= 0 && s.returnDelay <= 60 && s.initialWaitTime >= 0 && s.initialWaitTime <= 60 && s.easeAmount >= 0 && s.easeAmount <= 2, "속도0~20/대기0~60/이징0~2만 지원");
                Check(Math.Abs(s.rotationSpeed) + s.randomRange <= 3600 && s.randomRange >= 0, "회전/랜덤 범위는 합계3600도/초 이하");
                Check(s.waypoints != null && s.waypoints.Length >= 2 && s.waypoints.Length <= 16 && s.waypoints.All(Finite) && s.waypoints[0] == Vector2.zero, "경유점2~16개·첫 점0 필요");
                Check(s.kind != 1 || s.waypoints.Length == 2, "거리/초 이동 트랩은 두 경유점만 지원");
                Check(s.kind == 0 || s.kind == 4 || result.Traps.Any(t => t.Id == s.id), "이동/회전 트랩 판정 참조 누락");
                Check(s.kind == 0 || !s.detectPlayer && !s.disappearAfterComplete && !s.disableColliderWhenFalling, "트랩에는 발판 감지/소멸 설정 불가");
                Check(s.kind != 0 || s.size.x > 0 && s.size.y > 0 && s.rotationSpeed == 0 && !s.randomRotation, "고체 발판은 회전0·크기>0 필요");
                Check(s.speed == 0 || s.speed >= .005f, "0보다 크고0.005 미만인 속도는 원본 누적 틱 상한 초과");
                Check(s.kind != 4 || !string.IsNullOrWhiteSpace(s.toggleGroup) && s.toggleGroup.Length <= 64 && float.IsFinite(s.toggleInterval) && (s.toggleInterval == 0 || s.toggleInterval >= .1f && s.toggleInterval <= 60) && s.size.x > 0 && s.size.y > 0 && s.rotationSpeed == 0, "전환 그룹/간격(수동0 또는 자동0.1~60초)/크기 오류");
                Check(!s.randomRotation || s.kind == 2, "랜덤 회전은 RotatingTrap만 지원");
                Check(Finite(s.renderCenter) && Finite(s.renderSize) && s.renderSize.x > 0 && s.renderSize.y > 0, "표시 범위 오류");
                var points = s.waypoints.Select(Q).ToArray();
                Check(points.All(p => FPMath.Abs(p.X) <= 512 && FPMath.Abs(p.Y) <= 512), "경유점 범위512 초과");
                // 원본50Hz float 누적의 마지막 틱과 대기 -1틱을 작성 시 확정한다.
                int legacy = 0; float progress = 0;
                if (s.speed > 0) { while (progress < 1 && legacy < 10000) { progress = Mathf.Clamp01(progress + .02f * s.speed); legacy++; } }
                var ease = new FP[257];
                for (int i = 0; i < ease.Length; i++) ease[i] = Q(PlatformMotion.Ease(i / 256f, s.easeAmount));
                var c = new FlipFriendsMotionConfig { Id = s.id, Kind = s.kind, Origin = Q(s.origin), HalfSize = Q(s.size) / 2, Points = points, Ease = ease,
                    Speed = Q(s.speed), RotationSpeed = Q(s.rotationSpeed), RandomRange = Q(s.randomRange), SegmentTicks = Math.Max(1, Ticks(legacy * .02f)),
                    WaitTicks = Ticks(Math.Max(0, Math.Ceiling(s.waitTime / .02f - .00001) - 1) * .02f), ReturnTicks = Math.Max(2, Ticks(s.returnDelay)), InitialTicks = Ticks(s.initialWaitTime),
                    Cyclic = s.cyclic, Repeat = s.repeat, DetectPlayer = s.detectPlayer, Disappear = s.disappearAfterComplete, DisableOnComplete = s.disableColliderWhenFalling, RandomRotation = s.randomRotation,
                    ToggleGroup = s.toggleGroup, ToggleTicks = s.kind == 4 ? Ticks(s.toggleInterval) : 0, InitialActive = s.initialActive };
                result.Motions.Add(c); result.MotionVisuals.Add(s);
                if (s.kind == 4) lines.Add($"자동전환1={s.toggleGroup.Length}:{s.toggleGroup};틱={c.ToggleTicks};초기={c.InitialActive}");
                string Key(FPVector2 p) => p.X.RawValue.ToString(CultureInfo.InvariantCulture) + "," + p.Y.RawValue.ToString(CultureInfo.InvariantCulture);
                lines.Add(FormattableString.Invariant($"동작1={s.id};종류={c.Kind};기준={Key(c.Origin)};반크기={Key(c.HalfSize)};경로={string.Join("/", points.Select(Key))};속도={c.Speed.RawValue};시간={c.SegmentTicks},{c.WaitTicks},{c.ReturnTicks},{c.InitialTicks};왕복={c.Cyclic};반복={c.Repeat};감지={c.DetectPlayer};소멸={c.Disappear};비활성={c.DisableOnComplete};회전={c.RotationSpeed.RawValue},{c.RandomRange.RawValue},{c.RandomRotation};보간={string.Join(",", ease.Select(v => v.RawValue.ToString(CultureInfo.InvariantCulture)))};표시={Key(Q(s.renderCenter))}/{Key(Q(s.renderSize))}"));
            }
            foreach (var group in result.Motions.Where(c => c.Kind == 4).GroupBy(c => c.ToggleGroup))
                if (group.Select(c => c.ToggleTicks).Distinct().Count() != 1) throw new InvalidOperationException($"{data.stageId}: 전환 그룹 {group.Key} 간격이 다릅니다.");
            var resetIds = new HashSet<string>();
            var resets = data.motionResets ?? Array.Empty<QuantumMotionResetSource>();
            if (resets.Length > 64) throw new InvalidOperationException("발판 리셋 버튼 최대64개 초과");
            foreach (var s in resets.OrderBy(s => s?.id, StringComparer.Ordinal))
            {
                int index = s == null ? -1 : result.Motions.FindIndex(c => c.Id == s.target);
                if (s == null || string.IsNullOrEmpty(s.id) || !resetIds.Add(s.id) || index < 0 || !result.Motions[index].DetectPlayer || !Finite(s.center) || !Finite(s.size) || s.size.x <= 0 || s.size.y <= 0)
                    throw new InvalidOperationException($"{data.stageId}/{s?.id} 위치={s?.center}: 감지 발판 리셋 참조/크기/ID 오류");
                var c = new FlipFriendsMotionResetConfig { Id = s.id, Target = index, Center = Q(s.center), HalfSize = Q(s.size) / 2, IsTrigger = s.isTrigger };
                result.MotionResets.Add(c);
                lines.Add(FormattableString.Invariant($"발판리셋1={s.id};대상={s.target};위치={c.Center.X.RawValue},{c.Center.Y.RawValue};반크기={c.HalfSize.X.RawValue},{c.HalfSize.Y.RawValue};트리거={c.IsTrigger}"));
            }
            AddSwitches(result, lines, data, ids, resetIds);
        }

        private static void AddSwitches(QuantumMapConversion result, List<string> lines, QuantumOfficialStageData data,
            HashSet<string> motionIds, HashSet<string> resetIds)
        {
            var sources = data.switches ?? Array.Empty<QuantumStageSwitchSource>();
            if (sources.Length > 64) throw new InvalidOperationException($"{data.stageId}: 스위치 최대64개 초과");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var s in sources.OrderBy(s => s?.id, StringComparer.Ordinal))
            {
                string at = $"{data.stageId}/{s?.id} 위치={s?.center}";
                void Check(bool valid, string why) { if (!valid) throw new InvalidOperationException(at + ": " + why); }
                Check(s != null && !string.IsNullOrWhiteSpace(s.id) && ids.Add(s.id) && !motionIds.Contains(s.id) && !resetIds.Contains(s.id), "스위치 ID 누락/중복");
                int target = result.Motions.FindIndex(c => c.Kind == 4 && c.ToggleGroup == s.targetGroup);
                Check(target >= 0, $"전환 그룹 '{s.targetGroup}' 참조 누락");
                Check(Finite(s.center) && Finite(s.size) && s.size.x > 0 && s.size.y > 0, "스위치 위치/크기 오류");
                var half = Q(s.size) / 2;
                Check(half.X > 0 && half.Y > 0, "스위치 FP 크기0");
                Check(s.isDoor ? (s.detectPlayers || s.detectBoxes) && s.cooldown == 0 :
                    s.detectPlayers && !s.detectBoxes && float.IsFinite(s.cooldown) && s.cooldown >= 0 && s.cooldown <= 60,
                    "문은 플레이어/상자 감지 필요, OnOff는 플레이어 전용과 쿨다운0~60초 필요");
                var group = result.Motions[target];
                Check(!s.isDoor || group.ToggleTicks == 0 && group.InitialActive &&
                    result.Motions.Count(c => c.Kind == 4 && c.ToggleGroup == group.ToggleGroup) == 1, "문은 처음 닫힌 수동 단일 Box 필요");
                Check(!result.StageSwitches.Any(c => c.Target == target && (c.IsDoor || s.isDoor)), "문 그룹은 전용 스위치 하나만 지원");
                var c = new FlipFriendsStageSwitchConfig
                {
                    Id = s.id, Target = target, IsDoor = s.isDoor, DetectPlayers = s.detectPlayers,
                    DetectBoxes = s.detectBoxes, CooldownTicks = Math.Max(s.isDoor ? 0 : 1, Ticks(s.cooldown)),
                    Center = Q(s.center), HalfSize = half,
                };
                result.StageSwitches.Add(c);
                lines.Add(FormattableString.Invariant($"스위치1={c.Id.Length}:{c.Id};그룹={group.ToggleGroup.Length}:{group.ToggleGroup};문={c.IsDoor};감지={c.DetectPlayers},{c.DetectBoxes};시간={c.CooldownTicks};위치={c.Center.X.RawValue},{c.Center.Y.RawValue};반크기={half.X.RawValue},{half.Y.RawValue}"));
            }
            foreach (var motion in result.Motions.Where(c => c.Kind == 4 && c.ToggleTicks == 0))
                if (!result.StageSwitches.Any(s => result.Motions[s.Target].ToggleGroup == motion.ToggleGroup))
                    throw new InvalidOperationException($"{data.stageId}/{motion.Id} 위치={motion.Origin}: 수동 그룹에 스위치가 없습니다.");
        }
    }
}
