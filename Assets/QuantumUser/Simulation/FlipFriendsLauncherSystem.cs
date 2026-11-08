namespace Quantum
{
    using Photon.Deterministic;

    // OnTriggerEnter2D처럼 이동 뒤 진입에서만 속도를 덮어쓴다. 접촉 이력을 프레임에 두어 롤백에도 진입 횟수가 같다.
    public unsafe class FlipFriendsLauncherSystem : SystemMainThread
    {
        public const int BounceForce = 20;
        public const int SpringForce = 50;

        public override void Update(Frame frame)
        {
            var configs = frame.RuntimeConfig.Launchers;
            if (configs == null || configs.Length == 0 || frame.Global->FlipFriendsCompletedFrame > 0) return;
            if (!frame.Global->FlipFriendsLaunchersInitialized)
            {
                frame.Global->FlipFriendsLaunchersInitialized = true;
                for (int i = 0; i < configs.Length; i++) frame.Add(frame.Create(), new FlipFriendsLauncher { ContentIndex = i });
            }
            // 변환기의 ID 순서대로 모두 적용한다. 원본과 같이 같은 틱 여러 진입은 마지막 속도 덮어쓰기가 남는다.
            for (int index = 0; index < configs.Length; index++)
            {
                var devices = frame.Filter<FlipFriendsLauncher>();
                while (devices.NextUnsafe(out _, out var device))
                {
                    if (device->ContentIndex != index) continue;
                    int contacts = 0;
                    var players = frame.Filter<Transform2D, FlipFriendsPlayer>();
                    while (players.NextUnsafe(out _, out var transform, out var player))
                    {
                        if (player->IsFinished || FlipFriendsRespawnSystem.IsBlocked(frame, *player)) continue;
                        if (!Overlaps(configs[index], transform->Position + FlipFriendsInteractionSystem.Offset(*player), FlipFriendsInteractionSystem.HalfSize(*player))) continue;
                        int bit = 1 << (int)player->Player;
                        contacts |= bit;
                        if ((device->PlayerContacts & bit) == 0) Apply(frame, transform->Position, player, configs[index]);
                    }
                    device->PlayerContacts = contacts;
                }
            }
        }

        public static void Apply(Frame frame, FPVector2 position, ref FlipFriendsPlayer player, FlipFriendsLauncherConfig config)
        {
            fixed (FlipFriendsPlayer* pointer = &player) Apply(frame, position, pointer, config);
        }

        public static void Apply(Frame frame, FPVector2 position, FlipFriendsPlayer* player, FlipFriendsLauncherConfig config)
        {
            var away = position - config.Origin;
            // 원본은 운반/로프/무적·조작 제한 여부와 관계없이 velocity만 바꾼다. 관계·스무딩·타이머는 유지한다.
            player->Velocity = away == default ? default : away.Normalized * (config.Kind == 0 ? BounceForce : SpringForce);
            player->LaunchCount++;
            player->LaunchFrame = frame.Number;
            player->LaunchKind = config.Kind;
        }

        public static bool Overlaps(FlipFriendsLauncherConfig config, FPVector2 center, FPVector2 half)
        {
            if (config.Radius <= 0) return FlipFriendsHazardSystem.Overlaps(config.Vertices, center, half);
            var delta = center - config.Center;
            var x = FPMath.Max(FPMath.Abs(delta.X) - half.X, 0);
            var y = FPMath.Max(FPMath.Abs(delta.Y) - half.Y, 0);
            return x * x + y * y < config.Radius * config.Radius;
        }
    }
}
