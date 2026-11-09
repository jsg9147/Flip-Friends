namespace Quantum
{
    using Photon.Deterministic;

    // 원본 CannonShooter/CannonProjectile을 틱 상태로 옮긴다. 이동을 마친 플레이어 뒤, 피격 판정 앞에서 돈다.
    // 틱 안 순서: (1) 이전 틱 위치로 거리 초과 탄 제거 (2) 남은 탄 이동 (3) 대포 ID 순 발사.
    // 원본도 물리 단계(이동·트리거) 뒤 Update에서 거리 검사·발사를 하므로, 제거 틱 직전 위치는 이미 피격 판정을 거쳤다.
    public unsafe class FlipFriendsCannonSystem : SystemMainThread
    {
        // 원본 RangeCheckInterval 0.1초.
        public const int RangeCheckTicks = 6;
        // 변환기가 대포별 최대 동시 탄 수 합이 이 값을 넘는 콘텐츠를 거부한다. 실행 중 초과는 발사를 건너뛰고 기록만 한다.
        public const int MaxProjectiles = 128;

        public override void Update(Frame frame)
        {
            var configs = frame.RuntimeConfig.Cannons;
            if (configs == null || configs.Length == 0) return;
            if (!frame.Global->FlipFriendsCannonsInitialized)
            {
                frame.Global->FlipFriendsCannonsInitialized = true;
                for (int i = 0; i < configs.Length; i++) frame.Add(frame.Create(), new FlipFriendsCannon { ContentIndex = i, LastShotFrame = -1 });
            }
            int started = frame.Global->FlipFriendsMotionStartedFrame;
            // 두 플레이어 등록 전과 전원 도착 뒤에는 발사·이동하지 않는다. 남은 탄은 러너 종료 때 프레임과 함께 사라진다.
            if (started == 0 || frame.Global->FlipFriendsCompletedFrame > 0) return;

            // 순회 중 제거를 피하려고 제거 대상을 먼저 모은다. 동시 탄 수는 MaxProjectiles를 넘지 않는다.
            var expired = stackalloc EntityRef[MaxProjectiles];
            int expiredCount = 0;
            var projectiles = frame.Filter<FlipFriendsProjectile>();
            while (projectiles.NextUnsafe(out var entity, out var shot))
            {
                int age = frame.Number - 1 - shot->SpawnFrame;
                if (age > 0 && age % RangeCheckTicks == 0 && expiredCount < MaxProjectiles &&
                    (shot->Position - shot->Origin).SqrMagnitude > configs[shot->Cannon].MaxDistanceSquared)
                {
                    expired[expiredCount++] = entity;
                    continue;
                }
                shot->Position += configs[shot->Cannon].Velocity * frame.DeltaTime;
            }
            for (int i = 0; i < expiredCount; i++)
            {
                frame.Destroy(expired[i]);
                frame.Global->FlipFriendsProjectileCount--;
                frame.Global->FlipFriendsProjectileExpired++;
            }

            long elapsedRaw = (long)(frame.Number - started) << 16;
            for (int index = 0; index < configs.Length; index++)
            {
                var cannons = frame.Filter<FlipFriendsCannon>();
                while (cannons.NextUnsafe(out _, out var cannon))
                {
                    if (cannon->ContentIndex != index) continue;
                    var c = configs[index];
                    // 원본 Update의 time >= nextShootTime. 간격이 1틱 이상이라 한 틱에 최대 한 번 발사한다.
                    if (elapsedRaw < c.DelayTicks.RawValue + cannon->ShotCount * c.IntervalTicks.RawValue) break;
                    cannon->ShotCount++;
                    cannon->LastShotFrame = frame.Number;
                    if (frame.Global->FlipFriendsProjectileCount >= MaxProjectiles) { frame.Global->FlipFriendsDroppedShots++; break; }
                    frame.Add(frame.Create(), new FlipFriendsProjectile { Cannon = index, Shot = cannon->ShotCount, SpawnFrame = frame.Number, Origin = c.Muzzle, Position = c.Muzzle });
                    frame.Global->FlipFriendsProjectileCount++;
                    frame.Global->FlipFriendsProjectileSpawned++;
                    break;
                }
            }
        }

        // 피격 시스템이 정적·이동 트랩에 닿지 않은 플레이어에게만 호출한다. 생성 틱의 탄은 원본처럼 첫 이동 뒤부터 판정한다.
        // 여러 탄에 닿으면 (발사 틱, 대포 ID 순) 가장 이른 탄 하나를 쓴다. 엔티티 색인 재사용 순서와 무관하게 하기 위함이다.
        public static bool TryFindHit(Frame frame, FPVector2 center, FPVector2 half, out EntityRef hit, out FPVector2 origin)
        {
            hit = default; origin = default;
            var configs = frame.RuntimeConfig.Cannons;
            if (configs == null || configs.Length == 0) return false;
            int bestFrame = int.MaxValue, bestCannon = int.MaxValue;
            var projectiles = frame.Filter<FlipFriendsProjectile>();
            while (projectiles.Next(out var entity, out var shot))
            {
                if (shot.SpawnFrame >= frame.Number) continue;
                if (shot.SpawnFrame > bestFrame || shot.SpawnFrame == bestFrame && shot.Cannon >= bestCannon) continue;
                var d = shot.Position - center;
                var x = FPMath.Max(0, FPMath.Abs(d.X) - half.X); var y = FPMath.Max(0, FPMath.Abs(d.Y) - half.Y);
                var r = configs[shot.Cannon].Radius;
                if (x * x + y * y >= r * r) continue;
                bestFrame = shot.SpawnFrame; bestCannon = shot.Cannon; hit = entity; origin = shot.Position;
            }
            return hit.IsValid;
        }
    }
}
