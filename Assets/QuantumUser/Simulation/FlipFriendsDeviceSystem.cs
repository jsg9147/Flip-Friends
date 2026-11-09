namespace Quantum
{
    using Photon.Deterministic;

    public unsafe class FlipFriendsDeviceSystem : SystemMainThread
    {
        public override void Update(Frame frame)
        {
            var configs = frame.RuntimeConfig.Devices;
            if (configs == null || frame.Global->FlipFriendsCompletedFrame > 0) return;
            if (!frame.Global->FlipFriendsDevicesInitialized)
            {
                frame.Global->FlipFriendsDevicesInitialized = true;
                for (int i = 0; i < configs.Length; i++) frame.Add(frame.Create(), new FlipFriendsDevice { ContentIndex = i });
            }
            // 체크포인트를 먼저 확정하고 구간 이동, 소환 순으로 처리한다.
            for (int kind = 0; kind < 3; kind++)
            for (int index = 0; index < configs.Length; index++)
            {
                var config = configs[index];
                if (config.Kind != kind) continue;
                var devices = frame.Filter<FlipFriendsDevice>();
                while (devices.NextUnsafe(out _, out var device))
                {
                    if (device->ContentIndex != index) continue;
                    Contacts(frame, config, out int players, out ulong boxes);
                    int entered = players & ~device->PlayerContacts;
                    ulong boxEntered = boxes & ~device->BoxContacts;
                    device->PlayerContacts = players; device->BoxContacts = boxes;
                    for (int player = 0; player < frame.PlayerCount; player++)
                    {
                        if ((entered & (1 << player)) == 0) continue;
                        var query = frame.Filter<FlipFriendsPlayer>();
                        while (query.NextUnsafe(out var entity, out var state))
                        {
                            if ((int)state->Player != player || FlipFriendsRespawnSystem.IsBlocked(frame, *state)) continue;
                            if (kind == 0) TouchCheckpoint(frame, entity, config);
                            else if (kind == 1) FlipFriendsRespawnSystem.QueueSection(frame, entity, config.Destination, config.OnlyBoxReset);
                            else TrySummon(frame, config, device);
                        }
                    }
                    if (kind == 1)
                    {
                        var query = frame.Filter<FlipFriendsBox>();
                        while (query.NextUnsafe(out var entity, out var box))
                            if ((boxEntered & (1UL << box->ContentIndex)) != 0 && !box->RespawnPending && box->RespawnFrame != frame.Number)
                            {
                                // 들린 상자는 Mirror에서 콜라이더가 꺼지므로 직접 접촉 대상이 아니다.
                                if (box->Carrier.IsValid) continue;
                                FlipFriendsRespawnSystem.ClearBox(frame, entity, config.Destination, false);
                            }
                    }
                }
            }
            // 순간이동 목적지의 접촉을 이미 소비한 것으로 기록해 유지 접촉 재실행을 막는다.
            var all = frame.Filter<FlipFriendsDevice>();
            while (all.NextUnsafe(out _, out var device))
                Contacts(frame, configs[device->ContentIndex], out device->PlayerContacts, out device->BoxContacts);
        }

        public static void TouchCheckpoint(Frame frame, EntityRef entity, FlipFriendsDeviceConfig config)
        {
            var player = frame.Unsafe.GetPointer<FlipFriendsPlayer>(entity);
            if (player->IsFinished || FlipFriendsRespawnSystem.IsBlocked(frame, *player)) return;
            if (!player->HasCheckpoint || config.CheckpointId > player->CheckpointId)
            {
                player->HasCheckpoint = true; player->CheckpointId = config.CheckpointId;
                player->CheckpointPosition = config.Destination;
            }
        }

        public static bool TrySummon(Frame frame, FlipFriendsDeviceConfig config, ref FlipFriendsDevice device)
        {
            fixed (FlipFriendsDevice* pointer = &device) return TrySummon(frame, config, pointer);
        }

        public static bool TrySummon(Frame frame, FlipFriendsDeviceConfig config, FlipFriendsDevice* device)
        {
            if (frame.Global->FlipFriendsCompletedFrame > 0 || frame.Number < device->CooldownUntil || device->LastSummonFrame == frame.Number) return false;
            var boxes = frame.Filter<FlipFriendsBox>();
            while (boxes.NextUnsafe(out var entity, out var box))
            {
                if (box->ContentIndex != config.TargetBoxIndex) continue;
                if (box->RespawnPending || box->RespawnFrame == frame.Number ||
                    box->Carrier.IsValid && frame.Get<FlipFriendsPlayer>(box->Carrier).SectionPending ||
                    !CanPlace(frame, config.Destination, box->HalfSize, entity, default))
                { device->FailureCount++; return false; }
                if (box->Carrier.IsValid) FlipFriendsRespawnSystem.Detach(frame, box->Carrier);
                FlipFriendsRespawnSystem.ClearBox(frame, entity, config.Destination, false);
                box->RespawnPending = false;
                device->LastSummonFrame = frame.Number;
                device->CooldownUntil = frame.Number + config.CooldownTicks;
                device->SuccessCount++;
                return true;
            }
            return false;
        }

        public static bool CanPlace(Frame frame, FPVector2 center, FPVector2 half, EntityRef ignored, EntityRef held)
        {
            if (FlipFriendsMotionSystem.Overlaps(frame, center, half)) return false;
            if (FlipFriendsTerrain.Overlaps(frame.RuntimeConfig.TerrainContours, center, half)) return false;
            foreach (var box in frame.Map.StaticColliders2D)
                if (Overlaps(center, half, box.Position, box.BoxExtents)) return false;
            var boxes = frame.Filter<Transform2D, FlipFriendsBox>();
            while (boxes.Next(out var entity, out var transform, out var box))
                if (entity != ignored && entity != held && !box.RespawnPending && Overlaps(center, half, transform.Position, box.HalfSize)) return false;
            var players = frame.Filter<Transform2D, FlipFriendsPlayer>();
            while (players.Next(out var entity, out var transform, out var player))
                if (entity != ignored && entity != held && !player.RespawnPending && !player.IsFinished &&
                    Overlaps(center, half, transform.Position + FlipFriendsInteractionSystem.Offset(player), FlipFriendsInteractionSystem.HalfSize(player))) return false;
            return true;
        }

        private static void Contacts(Frame frame, FlipFriendsDeviceConfig config, out int players, out ulong boxes)
        {
            players = 0; boxes = 0;
            if (config.DetectPlayers)
            {
                var query = frame.Filter<Transform2D, FlipFriendsPlayer>();
                while (query.Next(out _, out var transform, out var player))
                    if (!player.IsFinished && !player.RespawnPending && Touches(config,
                        transform.Position + FlipFriendsInteractionSystem.Offset(player), FlipFriendsInteractionSystem.HalfSize(player))) players |= 1 << (int)player.Player;
            }
            if (config.DetectBoxes)
            {
                var query = frame.Filter<Transform2D, FlipFriendsBox>();
                while (query.Next(out _, out var transform, out var box))
                    if (!box.Carrier.IsValid && !box.RespawnPending && Touches(config, transform.Position, box.HalfSize)) boxes |= 1UL << box.ContentIndex;
            }
        }

        public static bool Touches(FlipFriendsDeviceConfig config, FPVector2 center, FPVector2 half)
        {
            if (config.ContactCenters == null || config.ContactCenters.Length == 0) return Overlaps(config.Center, config.Size / 2, center, half);
            for (int i = 0; i < config.ContactCenters.Length; i++)
                if (Overlaps(config.ContactCenters[i], config.ContactSizes[i] / 2, center, half)) return true;
            return false;
        }

        private static bool Overlaps(FPVector2 a, FPVector2 ah, FPVector2 b, FPVector2 bh) =>
            FPMath.Abs(a.X - b.X) < ah.X + bh.X && FPMath.Abs(a.Y - b.Y) < ah.Y + bh.Y;
    }
}
