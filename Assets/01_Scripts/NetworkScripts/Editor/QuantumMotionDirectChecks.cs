using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Photon.Deterministic;
using Quantum;

public static class QuantumMotionDirectChecks
{
    public static int Run(Frame f)
    {
        var motions = f.RuntimeConfig.Motions; var resets = f.RuntimeConfig.MotionResets;
        var contours = f.RuntimeConfig.TerrainContours; var traps = f.RuntimeConfig.Traps;
        int completed = f.FlipFriendsCompletedFrame; var made = new List<EntityRef>(); var lines = new List<string>();
        var savedMotions = new Dictionary<EntityRef, FlipFriendsMotion>(); var savedResets = new Dictionary<EntityRef, FlipFriendsMotionReset>();
        var mq = f.Filter<FlipFriendsMotion>(); while (mq.Next(out var e, out var state)) savedMotions[e] = state;
        var rq = f.Filter<FlipFriendsMotionReset>(); while (rq.Next(out var e, out var state)) savedResets[e] = state;
        EntityRef New() { var e = f.Create(); made.Add(e); return e; }
        void Require(bool valid, string label) { if (!valid) throw new InvalidOperationException("발판 직접 검사: " + label); lines.Add(label); }
        var c = new FlipFriendsMotionConfig { Id = "direct", Origin = new FPVector2(100, 5), HalfSize = new FPVector2(2, FP._0_25),
            Points = new[] { default(FPVector2), new FPVector2(1, 0) }, Ease = Enumerable.Range(0, 257).Select(i => (FP)i / 256).ToArray(),
            Speed = 1, SegmentTicks = 60, ReturnTicks = 30, Cyclic = true };
        int index = motions.Length; var m = New(); var p0 = New(); var p1 = New(); var box = New();
        var origin0 = c.Origin + new FPVector2(-1, FP.FromString("0.75"));
        var origin1 = c.Origin + new FPVector2(1, FP.FromString("0.75"));
        void Start()
        {
            foreach (var e in made) if (f.Exists(e) && f.Has<FlipFriendsMotionReset>(e)) f.Destroy(e);
            f.RuntimeConfig.Motions = motions.Append(c).ToArray(); f.RuntimeConfig.MotionResets = resets; f.RuntimeConfig.TerrainContours = contours;
            f.Set(m, new FlipFriendsMotion { ContentIndex = index, Position = c.Origin, Collidable = true, Phase = 2 });
            f.Set(p0, new Transform2D { Position = origin0 }); f.Set(p1, new Transform2D { Position = origin1 });
            f.Set(p0, new FlipFriendsPlayer { Player = 0, IsGrounded = true, PreviousPosition = origin0 });
            f.Set(p1, new FlipFriendsPlayer { Player = 1, IsGrounded = true, PreviousPosition = origin1 });
            f.Set(box, new Transform2D { Position = new FPVector2(120, 5) }); f.Set(box, new FlipFriendsBox { HalfSize = new FPVector2(FP._0_50, FP._0_50) });
        }
        var system = new FlipFriendsMotionSystem();
        void Tick() => system.Update(f);
        void Again() { var s = f.Get<FlipFriendsMotion>(m); s.LastUpdateFrame = 0; f.Set(m, s); Tick(); }
        FlipFriendsTerrainContour Wall(FPVector2 min, FPVector2 max) => new() { Id = "직접벽", Vertices = new[] { min, new FPVector2(max.X, min.Y), max, new FPVector2(min.X, max.Y) } };
        try
        {
            f.FlipFriendsCompletedFrame = 0; Start(); Tick();
            var d = f.Get<FlipFriendsMotion>(m).Delta;
            Require(d.X > 0 && f.Get<Transform2D>(p0).Position == origin0 + d && f.Get<Transform2D>(p1).Position == origin1 + d, "다중승객 동일 이동량");
            var s = f.Get<FlipFriendsMotion>(m); Tick(); Require(f.Get<FlipFriendsMotion>(m).ElapsedTicks == s.ElapsedTicks, "동일틱 중복 이동 차단");
            Start(); var p = f.Get<FlipFriendsPlayer>(p0); p.Velocity.Y = 1; f.Set(p0, p); Tick();
            Require(f.Get<Transform2D>(p0).Position == origin0 && f.Get<Transform2D>(p1).Position.X > origin1.X, "점프 승객 분리");
            Start(); p = f.Get<FlipFriendsPlayer>(p0); p.Held = box; f.Set(p0, p);
            var b = f.Get<FlipFriendsBox>(box); b.Carrier = p0; f.Set(box, b); f.Set(box, new Transform2D { Position = origin0 + FlipFriendsInteractionSystem.HeldOffset }); Tick();
            Require(f.Get<Transform2D>(box).Position == f.Get<Transform2D>(p0).Position + FlipFriendsInteractionSystem.HeldOffset && f.Get<FlipFriendsBox>(box).Carrier == p0, "상자 운반 관계/위치 유지");
            Start(); p = f.Get<FlipFriendsPlayer>(p0); p.Held = p1; f.Set(p0, p); var carried = f.Get<FlipFriendsPlayer>(p1); carried.Carrier = p0; f.Set(p1, carried);
            f.Set(p1, new Transform2D { Position = origin0 + FlipFriendsInteractionSystem.HeldOffset }); Tick();
            Require(f.Get<Transform2D>(p1).Position == f.Get<Transform2D>(p0).Position + FlipFriendsInteractionSystem.HeldOffset && f.Get<FlipFriendsPlayer>(p1).Carrier == p0, "플레이어 운반 관계/위치 유지");
            Start(); f.Set(box, new Transform2D { Position = c.Origin + new FPVector2(0, FP.FromString("0.75")) }); Tick();
            Require(f.Get<Transform2D>(box).Position.X == c.Origin.X, "느슨한 상자 별도 탑승 운반 없음");
            Start(); var wallX = origin1.X + FP.FromString("0.405");
            f.RuntimeConfig.TerrainContours = contours.Append(Wall(new FPVector2(wallX, 5), new FPVector2(wallX + 1, 7))).ToArray(); Tick();
            Require(f.Get<FlipFriendsMotion>(m).ElapsedTicks == 0 && f.Get<Transform2D>(p0).Position == origin0 && f.Get<Transform2D>(p1).Position == origin1, "벽 막힘 전체승객/위상 원자 대기");
            c.Points[1] = new FPVector2(0, 1); Start();
            p = f.Get<FlipFriendsPlayer>(p0); p.Held = box; f.Set(p0, p); b = f.Get<FlipFriendsBox>(box); b.Carrier = p0; f.Set(box, b); f.Set(box, new Transform2D { Position = origin0 + FlipFriendsInteractionSystem.HeldOffset });
            var top = origin0.Y + FlipFriendsInteractionSystem.HeldOffset.Y + FP._0_50 + FP.FromString("0.005");
            f.RuntimeConfig.TerrainContours = contours.Append(Wall(new FPVector2(98, top), new FPVector2(100, top + 1))).ToArray(); Tick();
            Require(f.Get<FlipFriendsMotion>(m).ElapsedTicks == 0 && f.Get<Transform2D>(box).Position == origin0 + FlipFriendsInteractionSystem.HeldOffset, "천장 운반물 막힘 비관통");
            c.Points[1] = new FPVector2(1, 0); Start();
            f.Set(box, new Transform2D { Position = c.Origin + new FPVector2(FP.FromString("2.505"), 0) }); Tick();
            Require(f.Get<FlipFriendsMotion>(m).ElapsedTicks == 0, "측면 느슨한 상자 끼임 차단");
            Start(); c.DetectPlayer = true; c.InitialTicks = 18; c.Cyclic = false; f.RuntimeConfig.Motions[index] = c;
            s = f.Get<FlipFriendsMotion>(m); s.Phase = 0; s.ReadyFrame = f.Number + 1; f.Set(m, s); Tick();
            Require(f.Get<FlipFriendsMotion>(m).Phase == 0, "재감지12틱 준비 전 미출발");
            s.ReadyFrame = f.Number; s.LastUpdateFrame = 0; f.Set(m, s); Tick();
            Require(f.Get<FlipFriendsMotion>(m).ElapsedTicks == -18 && f.Get<FlipFriendsMotion>(m).Phase == 1, "감지 출발 대기18틱");
            s = f.Get<FlipFriendsMotion>(m); s.ElapsedTicks = c.SegmentTicks - 1; s.Phase = 2; s.LastUpdateFrame = 0; f.Set(m, s); Again();
            Require(f.Get<FlipFriendsMotion>(m).Phase == 3, "비왕복 완료 위상");
            s = f.Get<FlipFriendsMotion>(m); s.ElapsedTicks = c.SegmentTicks + c.ReturnTicks - 1; s.Position = c.Origin + c.Points[1]; f.Set(m, s);
            var before = f.Get<Transform2D>(p0).Position; Again();
            Require(f.Get<FlipFriendsMotion>(m).Revision == 1 && f.Get<Transform2D>(p0).Position == before, "자동 복귀 세대/승객 순간이동 없음");
            Start(); f.RuntimeConfig.Motions[index] = c; s = f.Get<FlipFriendsMotion>(m); s.Position += new FPVector2(3, 0); f.Set(m, s);
            f.Set(p0, new Transform2D { Position = new FPVector2(110, 5) }); f.Set(p1, new Transform2D { Position = new FPVector2(112, 5) });
            f.RuntimeConfig.MotionResets = resets.Concat(new[] { new FlipFriendsMotionResetConfig { Id = "r0", Target = index, Center = new FPVector2(110, 5), HalfSize = new FPVector2(1, 1), IsTrigger = true },
                new FlipFriendsMotionResetConfig { Id = "r1", Target = index, Center = new FPVector2(112, 5), HalfSize = new FPVector2(1, 1), IsTrigger = true } }).ToArray();
            for (int i = 0; i < 2; i++) { var e = New(); f.Add(e, new FlipFriendsMotionReset { ContentIndex = resets.Length + i }); }
            Tick(); Require(f.Get<FlipFriendsMotion>(m).Revision == 1 && f.Get<FlipFriendsMotion>(m).Position == c.Origin, "동일틱 두 버튼 리셋 세대1회");
            Again(); Require(f.Get<FlipFriendsMotion>(m).Revision == 1, "지속접촉 재리셋 없음");
            s = f.Get<FlipFriendsMotion>(m); s.Position += new FPVector2(3, 0); s.ResetPending = true; f.Set(m, s); f.Set(p0, new Transform2D { Position = c.Origin }); Again();
            Require(f.Get<FlipFriendsMotion>(m).ResetPending && f.Get<FlipFriendsMotion>(m).Revision == 1, "리셋 목적지 점유 대기");
            f.Set(p0, new Transform2D { Position = new FPVector2(115, 5) }); Again(); Require(!f.Get<FlipFriendsMotion>(m).ResetPending && f.Get<FlipFriendsMotion>(m).Revision == 2, "점유 해제 뒤 리셋 완료");
            c.Kind = 1; f.RuntimeConfig.Motions[index] = c; s = f.Get<FlipFriendsMotion>(m); s.Position = c.Origin + new FPVector2(4, 0); s.Angle = FP.Pi / 2; s.Collidable = false; f.Set(m, s);
            var trap = new FlipFriendsTrapConfig { Id = c.Id, Origin = c.Origin, Vertices = new[] { c.Origin + new FPVector2(-2, -FP._0_25), c.Origin + new FPVector2(2, -FP._0_25), c.Origin + new FPVector2(2, FP._0_25), c.Origin + new FPVector2(-2, FP._0_25) } };
            var transformed = FlipFriendsMotionSystem.TrapAt(f, trap);
            Require(transformed.Origin == s.Position && FlipFriendsHazardSystem.Overlaps(transformed.Vertices, s.Position + new FPVector2(0, 1), new FPVector2(FP._0_25, FP._0_25)) && !FlipFriendsHazardSystem.Overlaps(transformed.Vertices, s.Position + new FPVector2(1, 0), new FPVector2(FP._0_25, FP._0_25)), "이동 기준점/FP 회전 판정");
            f.RuntimeConfig.Traps = new[] { trap }; f.Set(p0, new FlipFriendsPlayer { Player = 0 }); f.Set(p0, new Transform2D { Position = s.Position + new FPVector2(0, 1) });
            Require(FlipFriendsHazardSystem.TryDamage(f, p0) && f.Get<FlipFriendsPlayer>(p0).Velocity == new FPVector2(0, 20), "동적 트랩 기존 피격/넉백 결합");
            trap.Center = c.Origin + new FPVector2(1, 0); trap.Radius = FP._0_50; trap.Vertices = Array.Empty<FPVector2>();
            transformed = FlipFriendsMotionSystem.TrapAt(f, trap);
            Require(FPMath.Abs(transformed.Center.X - s.Position.X) < FP.FromRaw(8) && FPMath.Abs(transformed.Center.Y - s.Position.Y - 1) < FP.FromRaw(8) &&
                FlipFriendsHazardSystem.Overlaps(transformed, transformed.Center, new FPVector2(FP._0_25, FP._0_25)) && !FlipFriendsHazardSystem.Overlaps(transformed, transformed.Center + new FPVector2(1, 1), new FPVector2(FP._0_25, FP._0_25)), "원형 이동 트랩 오프셋/회전/모서리 판정");
            c.Kind = 3; c.Cyclic = false; c.Repeat = true;
            var start = FlipFriendsMotionSystem.Sample(c, 61, out _, out _, out int cycle);
            Require(cycle == 1 && start.X > c.Origin.X && start.X < c.Origin.X + FP._0_50, "비왕복 반복 경로 전환");
            c.Kind = 4; c.ToggleGroup = "direct-toggle"; c.ToggleTicks = 1; c.InitialActive = ((f.Number - f.FlipFriendsMotionStartedFrame) & 1) == 0; Start();
            s = f.Get<FlipFriendsMotion>(m); s.Collidable = false; f.Set(m, s); f.Set(p0, new Transform2D { Position = c.Origin });
            Tick(); Require(!f.Get<FlipFriendsMotion>(m).Collidable, "전환 지형 활성화 점유 대기");
            f.Set(p0, new Transform2D { Position = new FPVector2(115, 8) }); Again();
            Require(f.Get<FlipFriendsMotion>(m).Collidable && f.Get<FlipFriendsMotion>(m).Revision == 1, "점유 해제 뒤 전환 활성화/세대");
        }
        finally
        {
            foreach (var e in made) if (f.Exists(e)) f.Destroy(e);
            foreach (var pair in savedMotions) f.Set(pair.Key, pair.Value);
            foreach (var pair in savedResets) f.Set(pair.Key, pair.Value);
            f.RuntimeConfig.Motions = motions; f.RuntimeConfig.MotionResets = resets; f.RuntimeConfig.TerrainContours = contours; f.RuntimeConfig.Traps = traps; f.FlipFriendsCompletedFrame = completed;
            File.WriteAllText("Logs/QuantumMotionDirect.txt", string.Join("\n", lines));
        }
        return lines.Count;
    }
}
