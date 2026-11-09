using System;
using Photon.Deterministic;
using Quantum;

// 재생 비교를 끝낸 일회용 러너의 최종 확정 프레임에서 대포·탄 경계를 직접 검사한다.
internal static class QuantumCannonDirectChecks
{
    private delegate void Edit<T>(ref T value);

    public static int Run(Frame frame)
    {
        int checks = 0;
        void Check(bool valid, string why)
        {
            if (!valid) throw new InvalidOperationException("대포 직접 검사: " + why);
            checks++;
        }
        var configs = frame.RuntimeConfig.Cannons;
        int Index(string name) => Array.FindIndex(configs, c => c.Id.EndsWith("/" + name, StringComparison.Ordinal));
        int left = Index("왼쪽 대포"), wall = Index("벽 관통 대포"), down = Index("하강 대포"), fast = Index("빠른 대포");
        Check(left >= 0 && wall >= 0 && down >= 0 && fast >= 0, "시험 대포 4종");
        frame.FlipFriendsCompletedFrame = 0;
        var system = new FlipFriendsCannonSystem();
        var players = frame.Filter<FlipFriendsPlayer>();
        players.Next(out var first, out _);
        players.Next(out var second, out _);
        var boxes = frame.Filter<FlipFriendsBox>();
        boxes.Next(out var box, out _);
        var parking = new FPVector2(-8, FP._0_50);
        int now = frame.Number;

        FlipFriendsPlayer P(EntityRef e) => frame.Get<FlipFriendsPlayer>(e);
        Transform2D T(EntityRef e) => frame.Get<Transform2D>(e);
        void SetPos(EntityRef e, FPVector2 position) { var t = T(e); t.Position = position; frame.Set(e, t); }
        void Prepare(EntityRef e, FPVector2 position)
        {
            var old = P(e);
            frame.Set(e, new FlipFriendsPlayer { Player = old.Player, HomePosition = old.HomePosition, RespawnPosition = old.RespawnPosition, Facing = 1, IsGrounded = true, PreviousPosition = position, DamageCount = old.DamageCount });
            SetPos(e, position);
        }
        void Clear()
        {
            var q = frame.Filter<FlipFriendsProjectile>();
            while (q.Next(out var e, out _)) { frame.Destroy(e); }
            frame.FlipFriendsProjectileCount = 0;
        }
        EntityRef Shot(int cannon, FPVector2 position, int spawn, int shot = 1)
        {
            var e = frame.Create();
            frame.Add(e, new FlipFriendsProjectile { Cannon = cannon, Shot = shot, SpawnFrame = spawn, Origin = configs[cannon].Muzzle, Position = position });
            frame.FlipFriendsProjectileCount++;
            return e;
        }
        FlipFriendsProjectile S(EntityRef e) => frame.Get<FlipFriendsProjectile>(e);
        bool Hit(EntityRef e, out EntityRef shot, out FPVector2 at)
        {
            var p = P(e);
            return FlipFriendsCannonSystem.TryFindHit(frame, T(e).Position + FlipFriendsInteractionSystem.Offset(p), FlipFriendsInteractionSystem.HalfSize(p), out shot, out at);
        }
        // 완료 뒤 멈춘 예약이 직접 검사 중 몰려 발사되지 않게 모든 대포의 다음 발사를 먼 미래로 둔다.
        void Freeze()
        {
            var q = frame.Filter<FlipFriendsCannon>();
            while (q.Next(out var e, out var c)) { c.ShotCount = 1_000_000_000; frame.Set(e, c); }
        }
        Freeze();
        Clear();
        Prepare(second, parking - new FPVector2(3, 0));
        SetPos(box, parking - new FPVector2(5, FP.FromString("0.05")));

        // 생성 지점이 플레이어와 겹쳐도 생성 틱에는 판정하지 않고, 다음 틱부터 판정한다.
        Prepare(first, parking);
        var spawned = Shot(left, parking, now);
        Check(!Hit(first, out _, out _), "생성 틱 판정 제외");
        Clear(); var older = Shot(left, parking, now - 1);
        Check(Hit(first, out var hit, out _) && hit == older, "다음 틱부터 판정");

        // 같은 틱 여러 탄: (발사 틱, 대포 ID 순) 가장 이른 탄 하나. 엔티티 생성 순서와 무관하다.
        Clear();
        Shot(fast, parking + new FPVector2(FP._0_10, 0), now - 1);
        var expectedShot = Shot(wall, parking - new FPVector2(FP._0_10, 0), now - 3);
        Shot(down, parking, now - 3);
        Shot(left, parking, now - 2);
        Check(Hit(first, out hit, out _) && hit == expectedShot && wall < down, "다중 접촉 첫 탄 선택");

        // 넉백은 탄→플레이어 단위 벡터×(20,20)·무적60. 탄은 맞혀도 남고 무적 중 재피격 없음.
        Clear();
        var center = parking + new FPVector2(-FP._0_50, -FP._0_25);
        var target = Shot(left, center, now - 1);
        Check(FlipFriendsHazardSystem.TryDamage(frame, first), "탄 피격");
        var away = (parking - center).Normalized;
        Check(P(first).Velocity == new FPVector2(away.X * 20, away.Y * 20) && P(first).InvincibleTicks == 60 && P(first).UncontrollableTicks == 60 && P(first).Facing == 1, "넉백 방향·크기·무적·조작 제한");
        Check(frame.Exists(target) && S(target).HitCount == 1 && !FlipFriendsHazardSystem.TryDamage(frame, first), "탄 유지·무적 중 재피격 없음");

        // 몸체 정적 트랩이 탄보다 먼저다(기존 ID 순 첫 트랩 규칙 뒤에 탄).
        Clear();
        var trap = Array.Find(frame.RuntimeConfig.Traps, t => t.Id == configs[left].Id);
        var bodyPoint = trap.Origin + new FPVector2(FP._0_50, FP._0_10);
        Prepare(first, bodyPoint);
        Shot(left, bodyPoint + new FPVector2(FP._0_25, 0), now - 1);
        Check(FlipFriendsHazardSystem.TryDamage(frame, first) && P(first).Velocity == FlipFriendsHazardSystem.Direction(trap, bodyPoint) * 20, "몸체 트랩 우선");

        // 들린 플레이어는 맞지 않는다. 운반자가 맞으면 넉백 쪽으로 내려놓는다.
        Clear();
        Prepare(first, parking);
        Prepare(second, parking + new FPVector2(FP.FromString("0.8"), 0));
        Check(FlipFriendsInteractionSystem.TryPickup(frame, first) && P(first).Held == second, "운반 준비");
        SetPos(second, T(first).Position + FlipFriendsInteractionSystem.HeldOffset);
        Shot(left, T(second).Position, now - 1);
        Check(!FlipFriendsHazardSystem.TryDamage(frame, second), "들린 플레이어 제외");
        Shot(left, parking + new FPVector2(FP._0_50, 0), now - 2);
        Check(FlipFriendsHazardSystem.TryDamage(frame, first) && !P(first).Held.IsValid && !P(second).Carrier.IsValid && P(first).Facing == -1, "운반자 피격 내려놓기");

        // 상자는 대상이 아니며 탄 이동이 상자 상태를 바꾸지 않는다.
        Clear();
        Prepare(first, parking); Prepare(second, parking - new FPVector2(3, 0));
        var boxBefore = frame.Get<FlipFriendsBox>(box); var boxAt = T(box).Position;
        Shot(left, boxAt, now - 1);
        system.Update(frame);
        var boxAfter = frame.Get<FlipFriendsBox>(box);
        Check(T(box).Position == boxAt && boxAfter.Velocity == boxBefore.Velocity && boxAfter.Carrier == boxBefore.Carrier, "상자 비대상");

        // 지형 안에서 생성·이동해도 멈추지 않는다(원본 Kinematic 트리거).
        Clear();
        var inWall = new FPVector2(QuantumCannonSetup.WallX + FP._0_50, FP._0_50);
        var wallShot = Shot(wall, inWall, now - 1);
        var floorShot = Shot(down, new FPVector2(10, -FP._1), now - 1);
        system.Update(frame);
        Check(S(wallShot).Position == inWall + configs[wall].Velocity * frame.DeltaTime && S(floorShot).Position.Y < -FP._1, "지형 통과 이동");

        // 거리 검사는 생성 뒤 6의 배수 나이에만 한다. 넘은 탄도 검사 틱 전에는 남는다.
        Clear();
        var far = configs[left].Muzzle + new FPVector2(25, 0);
        var waiting = Shot(left, far, now - 1 - 125);
        var due = Shot(left, far, now - 1 - 126, 2);
        system.Update(frame);
        Check(frame.Exists(waiting) && !frame.Exists(due) && frame.FlipFriendsProjectileCount == 1, "6틱 거리 검사·초과 제거");

        // 동시 탄 상한: 넘치면 발사를 건너뛰되 예약은 진행한다(발사 시각이 밀리지 않는다).
        Clear();
        var cannons = frame.Filter<FlipFriendsCannon>();
        EntityRef fastCannon = default;
        while (cannons.Next(out var e, out var c)) if (c.ContentIndex == fast) fastCannon = e;
        var ready = frame.Get<FlipFriendsCannon>(fastCannon);
        ready.ShotCount = 0; frame.Set(fastCannon, ready);
        frame.FlipFriendsProjectileCount = FlipFriendsCannonSystem.MaxProjectiles;
        int dropped = frame.FlipFriendsDroppedShots;
        system.Update(frame);
        Check(frame.FlipFriendsDroppedShots == dropped + 1 && frame.Get<FlipFriendsCannon>(fastCannon).ShotCount == 1, "상한 초과 발사 건너뜀");
        frame.FlipFriendsProjectileCount = 0; frame.FlipFriendsDroppedShots = dropped;

        // 같은 틱에는 대포마다 최대 한 발이다(밀린 발사를 몰아 쏘지 않는다).
        ready = frame.Get<FlipFriendsCannon>(fastCannon); ready.ShotCount = 0; frame.Set(fastCannon, ready);
        system.Update(frame);
        int fastShots = 0; var all = frame.Filter<FlipFriendsProjectile>();
        while (all.Next(out _, out var s)) if (s.Cannon == fast) fastShots++;
        Check(fastShots == 1 && frame.Get<FlipFriendsCannon>(fastCannon).ShotCount == 1, "틱당 최대 한 발");

        // 전원 도착 뒤에는 이동·발사·소멸하지 않는다.
        Freeze();
        Clear();
        var still = Shot(left, parking, now - 1);
        frame.FlipFriendsCompletedFrame = now;
        system.Update(frame);
        Check(S(still).Position == parking, "도착 뒤 정지");
        frame.FlipFriendsCompletedFrame = 0;

        // 터널링: 원본 최대 탄속30(틱당0.4999)이 크기0 대상도 건너뛰지 않는지 16개 위상으로 확인한다.
        Clear();
        Prepare(first, parking);
        var step = configs[fast].Velocity * frame.DeltaTime;
        var sweep = Shot(fast, parking, now - 1);
        for (int phase = 0; phase < 16; phase++)
        {
            bool found = false;
            var start = parking + new FPVector2(3 + FP._0_50 * phase / 16, FP._0_50);
            for (int k = 0; k < 20 && !found; k++)
            {
                var s = S(sweep); s.Position = start + step * k; frame.Set(sweep, s);
                // 크기가 거의 0인 대상을 같은 높이에 두어 탄 지름만으로 겹침을 본다.
                found = FlipFriendsCannonSystem.TryFindHit(frame, parking + new FPVector2(0, FP._0_50), new FPVector2(FP.FromRaw(1), FP.FromRaw(1)), out _, out _);
            }
            Check(found, "터널링 없음 위상" + phase);
        }
        Clear();
        frame.FlipFriendsCompletedFrame = now;
        return checks;
    }
}
