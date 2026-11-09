using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FlipFriends.Maps;
using FlipFriends.Network;
using Photon.Deterministic;
using Quantum;
using UnityEditor;
using UnityEngine;

public static class QuantumDeviceChecks
{
    public static string Status { get; private set; } = "미실행";
    private const int Target = 2400;
    public static RuntimeConfig Config()
    {
        var config = new RuntimeConfig();
        QuantumGameContent.Prepare(AssetDatabase.LoadAssetAtPath<QuantumCustomMapAsset>(QuantumDeviceSetup.ManifestPath), config);
        return config;
    }

    public static Quantum.Input InputFor(Frame frame, int number, int player)
    {
        if (frame == null || number < 120) return default;
        var query = frame.Filter<Transform2D, FlipFriendsPlayer>();
        FlipFriendsPlayer state = default; FPVector2 position = default;
        while (query.Next(out _, out var t, out var p)) if ((int)p.Player == player) { state = p; position = t.Position; }
        Quantum.Input Aim(FP x) => new Quantum.Input { Horizontal = FPMath.Clamp((x - position.X) * 4 - state.Velocity.X * 2, -FP._1, FP._1) };
        if (player == 1)
        {
            if (number < 400) return default;
            var input = Aim(number < 1050 ? -1 : number < 1700 ? FP.FromString("2.5") : 8);
            input.Jump = number >= 400 && number < 435;
            return input;
        }
        if (number < 650)
        {
            if (state.Held.IsValid) return Aim(FP.FromString("3.1"));
            var boxes = frame.Filter<Transform2D, FlipFriendsBox>(); boxes.Next(out _, out var bt, out _);
            FP gap = bt.Position.X - position.X;
            var input = Aim(bt.Position.X - FP.FromString("0.9"));
            if (gap >= FP.FromString("0.85") && gap < FP.FromString("0.98")) input.Horizontal = FP.FromString("0.001");
            input.Interact = number % 30 == 0;
            return input;
        }
        if (number < 1050)
        {
            if (state.SectionCount > 0) return new Quantum.Input { Reset = number > 900, Jump = true, Interact = true, Vertical = 1 };
            var input = Aim(4); input.Jump = number % 90 < 45; return input;
        }
        if (number < 1550) return Aim(-9);
        if (number < 1800) return Aim(-7);
        if (number < 2150) return Aim(-9);
        if (number < 2200) return new Quantum.Input { Reset = true };
        return default;
    }

    public sealed class Evidence
    {
        public bool HasCheckpoint, HasSectionCarry, HasSectionWaiting, HasSummon, HasFailure, HasCheckpointReset;
        public readonly List<string> Trace = new();
        public string Summary => $"CP={HasCheckpoint}, 구간운반={HasSectionCarry}, 대기={HasSectionWaiting}, 소환={HasSummon}, 거부={HasFailure}, CP리셋={HasCheckpointReset}";
        public void Observe(Frame frame)
        {
            var players = frame.Filter<Transform2D, FlipFriendsPlayer>();
            while (players.Next(out var entity, out var t, out var p))
            {
                HasCheckpoint |= p.HasCheckpoint;
                HasSectionCarry |= p.SectionCount > 0 && p.Held.IsValid;
                HasSectionWaiting |= p.SectionPending && p.Held.IsValid;
                HasCheckpointReset |= p.HasCheckpoint && p.RespawnCount > 0;
                if (p.Held.IsValid)
                {
                    if (frame.TryGet<FlipFriendsBox>(p.Held, out var box)) Require(box.Carrier == entity && box.RespawnPending == p.RespawnPending, "운반/대기 양방향 일치");
                    if (frame.TryGet<FlipFriendsPlayer>(p.Held, out var held)) Require(held.Carrier == entity && held.RespawnPending == p.RespawnPending, "플레이어 운반/대기 일치");
                }
                if (frame.Number % 30 == 0) Trace.Add($"{frame.Number} P{p.Player} pos={t.Position} v={p.Velocity} CP={p.HasCheckpoint}/{p.CheckpointId} section={p.SectionCount}/{p.SectionPending} held={p.Held} reset={p.RespawnCount} input={frame.FlipFriendsInput(p.Player).Horizontal} jump={frame.FlipFriendsInput(p.Player).Jump.IsDown}");
            }
            var devices = frame.Filter<FlipFriendsDevice>();
            while (devices.Next(out _, out var d))
            {
                HasSummon |= d.SuccessCount > 0; HasFailure |= d.FailureCount > 0;
                if (frame.Number % 30 == 0 && frame.RuntimeConfig.Devices[d.ContentIndex].Kind == 2) Trace.Add($"{frame.Number} summon success={d.SuccessCount} fail={d.FailureCount} cooldown={d.CooldownUntil} contacts={d.PlayerContacts}");
            }
        }
        public void AssertComplete() => Require(HasCheckpoint && HasSectionCarry && HasSectionWaiting && HasSummon && HasFailure && HasCheckpointReset, Summary);
    }
    public static async Task Run()
    {
        CheckContent();
        await CheckBoundaries();
        var session = new QuantumGameSession();
        session.InputSource = (tick, player) => InputFor(session.Runner?.Game.Frames.Verified, tick, player);
        QuantumRunner replay = null;
        var expected = new Dictionary<int, ulong>(); var actual = new Dictionary<int, ulong>(); var evidence = new Evidence();
        try
        {
            using (QuantumCallback.SubscribeManual<CallbackSimulateFinished>(callback =>
            {
                if (!callback.Frame.IsVerified || callback.Frame.Number > Target) return;
                if (callback.Game == session.Runner?.Game)
                {
                    evidence.Observe(callback.Frame);
                    if (callback.Frame.Number % 120 == 0) expected[callback.Frame.Number] = callback.Frame.CalculateChecksum();
                }
                if (callback.Game == replay?.Game && callback.Frame.Number % 120 == 0) actual[callback.Frame.Number] = callback.Frame.CalculateChecksum();
            }))
            {
                await session.StartLocalAsync(Config());
                session.Runner.IsSessionUpdateDisabled = true;
                for (int i = 0; i < Target + 240 && !expected.ContainsKey(Target); i++) session.Runner.Service(1f / 60);
                File.WriteAllText("Logs/QuantumDeviceTrace.txt", string.Join("\n", evidence.Trace));
                evidence.AssertComplete();
                var record = session.Runner.Game.GetRecordedReplay(includeChecksums: true);
                var serializer = new QuantumUnityJsonSerializer();
                replay = QuantumRunner.StartGame(new SessionRunner.Arguments
                {
                    RunnerId = "FF_DEVICES_REPLAY", RuntimeConfig = serializer.ConfigFromByteArray<RuntimeConfig>(record.RuntimeConfigData.Decode(), compressed: true),
                    SessionConfig = record.DeterministicConfig, ReplayProvider = record.CreateInputProvider(), InitialTick = record.InitialTick,
                    FrameData = record.InitialFrameData, GameMode = DeterministicGameMode.Replay, PlayerCount = 2
                });
                replay.IsSessionUpdateDisabled = true;
                for (int i = 0; i < Target + 240 && !actual.ContainsKey(Target); i++) replay.Service(1f / 60);
                Require(expected.Count == 20 && actual.Count == 20 && expected.All(p => actual[p.Key] == p.Value), "장치 입력 새 러너 재생20개");
                Status = "통과: 장치 경계/관계 검사·새 입력 재생20개 " + evidence.Summary;
                File.WriteAllText("Logs/QuantumDeviceChecks.txt", Status + "\n" + string.Join("\n", expected.Select(p => p.Key + ": " + p.Value)));
                File.WriteAllText("Logs/QuantumDeviceReplay.json", UnityEngine.JsonUtility.ToJson(record));
            }
        }
        finally { if (replay != null) await replay.ShutdownAsync(); await session.StopAsync(); }
    }

    public static async Task<string> RunCloud()
    {
        var sessions = new[] { new QuantumGameSession(), new QuantumGameSession() };
        var checks = new[] { new Dictionary<int, ulong>(), new Dictionary<int, ulong>() };
        var evidence = new[] { new Evidence(), new Evidence() };
        var clients = new Photon.Realtime.RealtimeClient[2];
        bool isFinishing = false;
        try
        {
            foreach (var session in sessions)
                session.InputSource = (tick, slot) =>
                {
                    var locals = session.Runner?.Game.GetLocalPlayers();
                    if (locals == null || locals.Count <= slot) return default;
                    return isFinishing ? FinishInput(session, slot) : InputFor(session.Runner.Game.Frames.Verified, tick, (int)locals[slot]);
                };
            using (QuantumCallback.SubscribeManual<CallbackSimulateFinished>(callback =>
            {
                if (!callback.Frame.IsVerified) return;
                for (int i = 0; i < 2; i++)
                    if (callback.Game == sessions[i].Runner?.Game && callback.Frame.Number <= 2400)
                    {
                        evidence[i].Observe(callback.Frame);
                        if (callback.Frame.Number % 120 == 0) checks[i][callback.Frame.Number] = callback.Frame.CalculateChecksum();
                    }
            }))
            {
                string room = "ff-devices-" + Guid.NewGuid().ToString("N");
                var first = sessions[0].StartDevelopmentAsync(Config(), room, false);
                await Wait(() => sessions[0].Client?.InRoom == true || first.IsFaulted, 15000);
                if (first.IsFaulted) await first;
                var second = sessions[1].StartDevelopmentAsync(Config(), room, true);
                await Task.WhenAll(first, second);
                clients[0] = sessions[0].Client; clients[1] = sessions[1].Client;
                await Wait(() => checks.All(check => check.ContainsKey(2400)), 65000);
                File.WriteAllText("Logs/QuantumDeviceCloudTrace.txt", string.Join("\n", evidence[0].Trace));
                foreach (var item in evidence) item.AssertComplete();
                var samples = checks[0].Where(pair => pair.Key >= 240).ToArray();
                Require(samples.Length == 19 && samples.All(p => checks[1].TryGetValue(p.Key, out var value) && value == p.Value), "개발 Photon 장치 확정 체크섬19개");
                isFinishing = true;
                await Wait(() => sessions.All(session => session.Runner.Game.Frames.Verified.FlipFriendsCompletedFrame > 0), 25000);
                int completed = sessions[0].Runner.Game.Frames.Verified.FlipFriendsCompletedFrame;
                Require(completed == sessions[1].Runner.Game.Frames.Verified.FlipFriendsCompletedFrame, "장치 Photon 전원 완료 프레임 일치");
                var result = $"통과: 새 장치 Photon 두 연결, 완료={completed}, 지역={clients[0].CurrentRegion}, RTT={clients[0].RealtimePeer.Stats.RoundtripTime}/{clients[1].RealtimePeer.Stats.RoundtripTime}ms, 같은 PC, 지연/손실 주입 없음\n" +
                    evidence[0].Summary + "\n" + evidence[1].Summary + "\n" + string.Join("\n", samples.Select(p => p.Key + ": " + p.Value));
                File.WriteAllText("Logs/QuantumDeviceCloud.txt", result);
                return result;
            }
        }
        finally
        {
            foreach (var session in sessions) await session.StopAsync();
            Require(sessions.All(session => session.Runner == null && session.Client == null && !session.HasInputSubscription) && clients.All(client => client == null || !client.IsConnected), "장치 Photon 종료 정리");
        }
    }

    public static Quantum.Input FinishInput(QuantumGameSession session, int slot)
    {
        var frame = session.Runner.Game.Frames.Verified;
        var locals = session.Runner.Game.GetLocalPlayers();
        if (frame == null || locals.Count <= slot) return default;
        var query = frame.Filter<Transform2D, FlipFriendsPlayer>();
        while (query.Next(out _, out var transform, out var player))
        {
            if (player.Player != locals[slot] || player.IsFinished) continue;
            var distance = frame.RuntimeConfig.GoalCenters[0].X - transform.Position.X;
            var input = new Quantum.Input { Horizontal = FPMath.Clamp(distance * 2 - player.Velocity.X, -FP._1, FP._1), Vertical = FPMath.Abs(distance) < FP.FromString("0.3") ? 1 : 0 };
            if (!player.Held.IsValid)
            {
                var boxes = frame.Filter<Transform2D, FlipFriendsBox>();
                while (boxes.Next(out _, out var bt, out var box))
                {
                    var gap = bt.Position.X - transform.Position.X;
                    if (box.Carrier.IsValid || gap <= 0 || gap > 2) continue;
                    input.Horizontal = FPMath.Clamp((gap - FP.FromString("0.9")) * 4 - player.Velocity.X * 2, -FP._1, FP._1);
                    if (gap >= FP.FromString("0.85") && gap < FP.FromString("0.98")) input.Horizontal = FP.FromString("0.001");
                    input.Interact = frame.Number % 30 < 5;
                }
            }
            return input;
        }
        return default;
    }

    private static void CheckContent()
    {
        var palette = AssetDatabase.LoadAssetAtPath<MapEditorPalette>("Assets/07_ScriptableObject/MapEditorPalette.asset");
        var text = AssetDatabase.LoadAssetAtPath<TextAsset>(QuantumDeviceSetup.SourcePath).text;
        var original = AssetDatabase.LoadAssetAtPath<QuantumMapDevices>(QuantumDeviceSetup.DevicesPath);
        QuantumMapConversion Convert(QuantumMapDevices devices, bool reverse = false)
        {
            var data = JsonUtility.FromJson<MapData>(text); if (reverse) data.objects.Reverse();
            var result = QuantumMapConverter.Convert(data, palette, true); devices.Apply(result); return result;
        }
        var valid = Convert(original);
        Require(Config().CustomMapAgreement.Contains(":제품12:") && QuantumGameContent.NetworkVersion == "ff-product-12" && valid.Version == 5, "제품12/기존5:5 콘텐츠 합의");
        Require(valid.ContentHash == Convert(original, true).ContentHash, "상자 정렬/재변환 안정 참조");
        var expectedAgreement = Config().CustomMapAgreement;
        var previousCulture = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            var differentCulture = (System.Globalization.CultureInfo)System.Globalization.CultureInfo.InvariantCulture.Clone();
            differentCulture.NumberFormat.NegativeSign = "~";
            System.Globalization.CultureInfo.CurrentCulture = differentCulture;
            Require(valid.ContentHash == Convert(original).ContentHash && Config().CustomMapAgreement == expectedAgreement, "숫자 문화권과 무관한 콘텐츠/제품 합의");
        }
        finally { System.Globalization.CultureInfo.CurrentCulture = previousCulture; }
        var clone = ScriptableObject.CreateInstance<QuantumMapDevices>();
        QuantumDeviceSource[] Copy() => original.Sources.Select(s => JsonUtility.FromJson<QuantumDeviceSource>(JsonUtility.ToJson(s))).ToArray();
        void Reject(Action<QuantumDeviceSource[]> change)
        {
            var sources = Copy(); change(sources); clone.Configure(sources); bool rejected = false;
            try { Convert(clone); } catch (InvalidOperationException error) { rejected = error.Message.Contains("장치"); }
            Require(rejected, "잘못된 장치 전체 거부");
        }
        try
        {
            var reversed = Copy(); Array.Reverse(reversed); clone.Configure(reversed);
            Require(valid.ContentHash == Convert(clone).ContentHash, "장치 ID 정렬 안정성");
            foreach (var field in new[] { "center", "destination", "cooldown", "checkpoint", "bounds", "reference" })
            {
                var sources = Copy();
                if (field == "center") sources[0].center.x += 1;
                if (field == "destination") sources[0].destination.x += 1;
                if (field == "cooldown") sources[4].cooldownSeconds += 1;
                if (field == "checkpoint") sources[0].checkpointId += 1;
                if (field == "bounds") sources[0].renderCenter.x -= 20;
                if (field == "reference") sources[4].id += "-changed";
                clone.Configure(sources); Require(valid.ContentHash != Convert(clone).ContentHash, "해시 반영: " + field);
            }
            Reject(s => s[4].targetBoxHome.x = 99); Reject(s => s[1].id = s[0].id);
            Reject(s => s[0].destination.y = -0.5f); Reject(s => s[0].destination.x = 4000);
            Reject(s => s[0].kind = 3); Reject(s => s[4].cooldownSeconds = float.NaN);
            Reject(s => s[0].size.x = 0); Reject(s => s[0].checkpointId = -1);
            var config = Config(); Require(config.Devices.Single(d => d.Kind == 2).CooldownTicks == 300, "실제 프리팹5초 보존");
            var root = new GameObject("미지원 장치 형태 검사");
            try
            {
                root.AddComponent<SavePoint>(); root.AddComponent<CircleCollider2D>().isTrigger = true;
                bool isRejected = false;
                try { QuantumDeviceSetup.Read(root, "bad-shape"); } catch (InvalidOperationException error) { isRejected = error.Message.Contains("위치="); }
                Require(isRejected, "원형/미지원 형태 위치 진단 전체 거부");
                UnityEngine.Object.DestroyImmediate(root.GetComponent<CircleCollider2D>());
                root.AddComponent<BoxCollider2D>().isTrigger = true;
                root.AddComponent<SpriteRenderer>().sprite = original.Sources[0].defaultSprite;
                UnityEngine.Object.DestroyImmediate(root.GetComponent<SavePoint>());
                root.AddComponent<RespawnHandler>(); isRejected = false;
                try { QuantumDeviceSetup.Read(root, "bad-link"); } catch (InvalidOperationException error) { isRejected = error.Message.Contains("resetPoint"); }
                Require(isRejected, "누락된 목적지 연결 전체 거부");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
        finally { UnityEngine.Object.DestroyImmediate(clone); }
    }

    private static async Task CheckBoundaries()
    {
        var session = new QuantumGameSession { InputSource = (_, _) => default };
        try
        {
            await session.StartLocalAsync(Config()); session.Runner.IsSessionUpdateDisabled = true;
            for (int i = 0; i < 80; i++) session.Runner.Service(1f / 60);
            CheckFrame(session.Runner.Game.Frames.Verified);
        }
        finally { await session.StopAsync(); }
    }

    private static void CheckFrame(Frame frame)
    {
        var players = frame.Filter<FlipFriendsPlayer>(); players.Next(out var a, out var ap); players.Next(out var b, out var bp);
        var boxes = frame.Filter<FlipFriendsBox>(); boxes.Next(out var boxEntity, out var originalBox);
        var config = frame.RuntimeConfig; var summon = config.Devices.Single(d => d.Kind == 2);
        var checkpoint = config.Devices.First(d => d.Kind == 0);
        void Place(EntityRef entity, FPVector2 position) => frame.Set(entity, new Transform2D { Position = position });
        void Reset()
        {
            frame.Set(a, new FlipFriendsPlayer { Player = ap.Player, HomePosition = config.CustomMapSpawns[(int)ap.Player], Facing = 1 });
            frame.Set(b, new FlipFriendsPlayer { Player = bp.Player, HomePosition = config.CustomMapSpawns[(int)bp.Player], Facing = 1 });
            frame.Set(boxEntity, new FlipFriendsBox { ContentIndex = originalBox.ContentIndex, HomePosition = originalBox.HomePosition, HalfSize = originalBox.HalfSize });
            Place(a, new FPVector2(0, 1)); Place(b, new FPVector2(-7, 1)); Place(boxEntity, new FPVector2(-3, 1));
            var devices = frame.Filter<FlipFriendsDevice>();
            while (devices.Next(out var entity, out var d)) frame.Set(entity, new FlipFriendsDevice { ContentIndex = d.ContentIndex });
        }
        void Carry(EntityRef target)
        {
            var state = frame.Get<FlipFriendsPlayer>(a); state.Held = target; frame.Set(a, state);
            if (target == b) { var p = frame.Get<FlipFriendsPlayer>(b); p.Carrier = a; frame.Set(b, p); }
            else { var box = frame.Get<FlipFriendsBox>(boxEntity); box.Carrier = a; frame.Set(boxEntity, box); }
        }
        Reset(); Require(!frame.Get<FlipFriendsPlayer>(a).HasCheckpoint, "체크포인트 미접촉");
        FlipFriendsDeviceSystem.TouchCheckpoint(frame, a, checkpoint);
        var first = frame.Get<FlipFriendsPlayer>(a); var same = checkpoint; same.Destination.X += 1;
        FlipFriendsDeviceSystem.TouchCheckpoint(frame, a, same);
        Require(frame.Get<FlipFriendsPlayer>(a).CheckpointPosition == first.CheckpointPosition && !frame.Get<FlipFriendsPlayer>(b).HasCheckpoint, "같은ID 최초 위치/플레이어별 독립");
        var higher = checkpoint; higher.CheckpointId = 5; higher.Destination = new FPVector2(2, 1);
        FlipFriendsDeviceSystem.TouchCheckpoint(frame, a, higher); FlipFriendsDeviceSystem.TouchCheckpoint(frame, a, checkpoint);
        Require(frame.Get<FlipFriendsPlayer>(a).CheckpointId == 5, "이전ID 거부/더큰ID 수용");
        FlipFriendsRespawnSystem.QueuePlayer(frame, a); new FlipFriendsRespawnSystem(true).Update(frame);
        Require(frame.Get<Transform2D>(a).Position == higher.Destination && frame.Get<FlipFriendsPlayer>(a).HomePosition == first.HomePosition, "체크포인트 복귀/최초 생성 위치 보존");
        Reset(); Carry(b); FlipFriendsDeviceSystem.TouchCheckpoint(frame, b, checkpoint);
        Require(frame.Get<FlipFriendsPlayer>(b).HasCheckpoint, "들린 플레이어 체크포인트 접촉 유지");
        Reset(); var saved = config.Devices;
        try
        {
            var low = checkpoint; var high = higher; low.Center = high.Center = new FPVector2(0, 1);
            config.Devices = new[] { high, low };
            var deviceQuery = frame.Filter<FlipFriendsDevice>(); while (deviceQuery.Next(out var entity, out _)) frame.Destroy(entity);
            frame.FlipFriendsDevicesInitialized = false;
            new FlipFriendsDeviceSystem().Update(frame);
            Require(frame.Get<FlipFriendsPlayer>(a).CheckpointId == 5, "같은틱 다중 접촉 최대ID");
        }
        finally
        {
            config.Devices = saved; var query = frame.Filter<FlipFriendsDevice>(); while (query.Next(out var entity, out _)) frame.Destroy(entity);
            frame.FlipFriendsDevicesInitialized = false; new FlipFriendsDeviceSystem().Update(frame);
        }
        var destination = new FPVector2(5, FP.FromString("0.5"));
        Reset(); Require(!FlipFriendsRespawnSystem.QueueSection(frame, a, destination, true), "onlyBox 빈손 거부");
        Carry(b); Require(!FlipFriendsRespawnSystem.QueueSection(frame, a, destination, true), "onlyBox 플레이어 운반 거부");
        foreach (var target in new[] { b, boxEntity })
        {
            Reset(); Carry(target);
            var blocker = frame.Create(); frame.Add(blocker, new Transform2D { Position = destination + FlipFriendsInteractionSystem.HeldOffset });
            frame.Add(blocker, new FlipFriendsBox { HalfSize = new FPVector2(1, FP.FromString("0.4")), ContentIndex = 63 });
            try
            {
                Require(FlipFriendsRespawnSystem.QueueSection(frame, a, destination, target == boxEntity), "구간 운반 복귀 요청/onlyBox 상자 허용");
                var p = frame.Get<FlipFriendsPlayer>(a);
                Require(p.SectionPending && p.RespawnPending && p.Held == target, "운반 전체 판정 막힘/관계 유지 대기");
                Require(!FlipFriendsRespawnSystem.QueueSection(frame, a, destination, false), "중복 구간 요청 차단");
                FlipFriendsRespawnSystem.QueuePlayer(frame, a); FlipFriendsRespawnSystem.QueuePlayer(frame, b);
                Require(!FlipFriendsInteractionSystem.RequestEscape(frame, b) && !FlipFriendsInteractionSystem.Interact(frame, a, true), "대기 입력/탈출/수동Reset 무시");
                FlipFriendsDeviceSystem.TouchCheckpoint(frame, a, higher);
                var d = new FlipFriendsDevice(); if (target == boxEntity) Require(!FlipFriendsDeviceSystem.TrySummon(frame, summon, ref d), "대기 중 소환 거부");
                new FlipFriendsRespawnSystem(false).Update(frame); new FlipFriendsRespawnSystem(true).Update(frame);
                Require(frame.Get<FlipFriendsPlayer>(a).Held == target && frame.Get<FlipFriendsPlayer>(a).SectionPending && !frame.Get<FlipFriendsPlayer>(a).HasCheckpoint, "대기 우선순위/관계 불변");
            }
            finally { frame.Destroy(blocker); }
            FlipFriendsRespawnSystem.TryRestoreSection(frame, a);
            var restored = frame.Get<FlipFriendsPlayer>(a);
            Require(!restored.RespawnPending && !restored.SectionPending && restored.Held == target && restored.PreviousPosition == destination && restored.Velocity == default && restored.ReleaseFrame == frame.Number + 9, "공간 해제 후 원자적 운반 복귀/타이머");
            Require(frame.Get<Transform2D>(target).Position == destination + FlipFriendsInteractionSystem.HeldOffset, "들린 대상 동시 좌표 복귀");
            Require(!FlipFriendsInteractionSystem.Interact(frame, a, false), "복귀틱 들기/던지기 차단");
        }
        Reset(); Carry(boxEntity); Place(b, summon.Destination);
        var initialBox = frame.Get<FlipFriendsBox>(boxEntity); initialBox.Velocity = new FPVector2(6, -2); initialBox.VelocityXSmoothing = 3; frame.Set(boxEntity, initialBox);
        var initialPosition = frame.Get<Transform2D>(boxEntity).Position; var device = new FlipFriendsDevice();
        Require(!FlipFriendsDeviceSystem.TrySummon(frame, summon, ref device) && device.CooldownUntil == 0 && frame.Get<FlipFriendsBox>(boxEntity).Equals(initialBox) && frame.Get<Transform2D>(boxEntity).Position == initialPosition && frame.Get<FlipFriendsPlayer>(a).Held == boxEntity, "막힌 소환 위치/속도/관계/쿨다운 완전 불변");
        Place(b, new FPVector2(-7, 1));
        Require(FlipFriendsDeviceSystem.TrySummon(frame, summon, ref device), "소환 성공");
        var summoned = frame.Get<FlipFriendsBox>(boxEntity);
        Require(!summoned.Carrier.IsValid && !frame.Get<FlipFriendsPlayer>(a).Held.IsValid && summoned.HomePosition == originalBox.HomePosition && summoned.Velocity == default && summoned.VelocityXSmoothing == 0 && frame.Get<Transform2D>(boxEntity).Position == summon.Destination && device.CooldownUntil == frame.Number + 300, "소환 성공 원자적 관계/속도 정리와 쿨다운");
        Require(!FlipFriendsDeviceSystem.TrySummon(frame, summon, ref device), "쿨다운 중/동일틱 중복 거부");
        summoned.RespawnFrame = 0; frame.Set(boxEntity, summoned); device.LastSummonFrame = 0; device.CooldownUntil = frame.Number + 1;
        Require(!FlipFriendsDeviceSystem.TrySummon(frame, summon, ref device), "쿨다운 경계 직전");
        device.CooldownUntil = frame.Number;
        Require(FlipFriendsDeviceSystem.TrySummon(frame, summon, ref device), "쿨다운 정확한 만료틱");
        summoned = frame.Get<FlipFriendsBox>(boxEntity); summoned.RespawnFrame = 0; frame.Set(boxEntity, summoned);
        Place(boxEntity, new FPVector2(config.FallMaxX + 1, 0)); new FlipFriendsRespawnSystem(true).Update(frame);
        Require(frame.Get<Transform2D>(boxEntity).Position == originalBox.HomePosition, "소환 뒤 추락 최초 배치 복귀");
        Reset(); FlipFriendsDeviceSystem.TouchCheckpoint(frame, a, higher);
        Place(a, new FPVector2(config.FallMaxX + 1, 0)); new FlipFriendsRespawnSystem(true).Update(frame);
        Require(frame.Get<Transform2D>(a).Position == higher.Destination && frame.Get<FlipFriendsPlayer>(a).HomePosition == first.HomePosition, "체크포인트 갱신 후 추락/최초 위치 보존");
        Reset(); Carry(boxEntity); Place(b, destination);
        FlipFriendsRespawnSystem.QueueSection(frame, a, destination, false);
        Require(frame.Get<FlipFriendsPlayer>(a).SectionPending, "운반자 몸체 목적지 점유 대기");
        Place(b, new FPVector2(-7, 1)); FlipFriendsRespawnSystem.TryRestoreSection(frame, a);
        Require(!frame.Get<FlipFriendsPlayer>(a).RespawnPending && !frame.Get<FlipFriendsBox>(boxEntity).RespawnPending, "몸체 공간 해제 원자 복귀");
        Reset(); Place(a, summon.Center + new FPVector2(FP.FromString("-0.4"), FP.FromString("0.4")));
        Place(b, summon.Center + new FPVector2(FP.FromString("0.4"), FP.FromString("0.4")));
        var deviceSystem = new FlipFriendsDeviceSystem(); deviceSystem.Update(frame); deviceSystem.Update(frame);
        var deviceQuery2 = frame.Filter<FlipFriendsDevice>();
        while (deviceQuery2.Next(out _, out var d))
            if (config.Devices[d.ContentIndex].Kind == 2) Require(d.SuccessCount == 1, "두 플레이어 동시 진입/유지 접촉 소환1회");
        var beforeContactBox = frame.Get<FlipFriendsBox>(boxEntity); beforeContactBox.RespawnFrame = 0; frame.Set(boxEntity, beforeContactBox);
        var deviceQuery3 = frame.Filter<FlipFriendsDevice>();
        while (deviceQuery3.Next(out var entity, out var d))
            if (config.Devices[d.ContentIndex].Kind == 2) { d.CooldownUntil = frame.Number; d.LastSummonFrame = 0; frame.Set(entity, d); }
        deviceSystem.Update(frame);
        var afterContact = frame.Filter<FlipFriendsDevice>();
        while (afterContact.Next(out _, out var d)) if (config.Devices[d.ContentIndex].Kind == 2) Require(d.SuccessCount == 1, "쿨다운 만료 후 유지 접촉 재실행 없음");
        Reset(); device = new FlipFriendsDevice(); var secondDevice = new FlipFriendsDevice();
        Require(FlipFriendsDeviceSystem.TrySummon(frame, summon, ref device) && !FlipFriendsDeviceSystem.TrySummon(frame, summon, ref secondDevice), "여러 장치 동일 상자 동일틱 소환1회");
        var solidBlocker = frame.Create(); frame.Add(solidBlocker, new Transform2D { Position = summon.Destination });
        frame.Add(solidBlocker, new FlipFriendsBox { HalfSize = new FPVector2(1, 1), ContentIndex = 63 });
        try
        {
            var box = frame.Get<FlipFriendsBox>(boxEntity); box.RespawnFrame = 0; frame.Set(boxEntity, box);
            secondDevice = new FlipFriendsDevice();
            Require(!FlipFriendsDeviceSystem.TrySummon(frame, summon, ref secondDevice) && frame.Get<FlipFriendsBox>(boxEntity).Equals(box) && secondDevice.CooldownUntil == 0, "다른 상자에 막힌 소환 불변");
        }
        finally { frame.Destroy(solidBlocker); }
        Reset(); Place(boxEntity, config.Devices.First(d => d.Kind == 1).Center); new FlipFriendsDeviceSystem().Update(frame);
        Require(frame.Get<FlipFriendsBox>(boxEntity).RespawnPending && !frame.Get<FlipFriendsPlayer>(a).SectionPending, "상자 직접Reset 별도 경로");
        Reset(); var blocked = frame.Get<FlipFriendsPlayer>(a); blocked.IsFinished = true; frame.Set(a, blocked);
        FlipFriendsDeviceSystem.TouchCheckpoint(frame, a, higher); Require(!frame.Get<FlipFriendsPlayer>(a).HasCheckpoint, "도착 후 접촉 무시");
        frame.FlipFriendsCompletedFrame = frame.Number;
        var completedDevice = new FlipFriendsDevice(); Require(!FlipFriendsDeviceSystem.TrySummon(frame, summon, ref completedDevice), "완료 후 소환 무시");
        frame.FlipFriendsCompletedFrame = 0;
    }

    public static async void RunStandalone()
    {
        try { await Run(); }
        catch (Exception error) { Status = "실패: " + error; Debug.LogException(error); File.WriteAllText("Logs/QuantumDeviceChecks.txt", Status); }
    }
    private static async Task Wait(Func<bool> condition, int milliseconds)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(milliseconds);
        while (!condition() && DateTime.UtcNow < deadline && EditorApplication.isPlaying) await Task.Delay(20);
        Require(condition(), "장치 비동기 제한 시간");
    }
    private static void Require(bool value, string label) { if (!value) throw new InvalidOperationException("Quantum 장치 검사: " + label); }
}
