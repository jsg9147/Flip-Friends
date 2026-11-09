using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FlipFriends.Maps;
using FlipFriends.Network;
using Photon.Deterministic;
using Quantum;
using UnityEditor;
using UnityEngine;

public static class QuantumSwitchChecks
{
    private const int Horizon = 2400;
    private const int ChecksumInterval = 120;
    public static string Status { get; private set; } = "미실행";

    private static QuantumOfficialStageAsset Asset => AssetDatabase.LoadAssetAtPath<QuantumOfficialStageAsset>(
        $"{QuantumOfficialStageWriter.ContentRoot}/{QuantumSwitchSetup.StageId}/Stage.asset");

    private static RuntimeConfig Config()
    {
        var config = new RuntimeConfig();
        QuantumGameContent.Prepare(Asset, config);
        return config;
    }

    private static void Require(bool condition, string reason)
    {
        if (!condition) throw new InvalidOperationException("스위치 검사: " + reason);
    }

    public static void RunContent()
    {
        var read = QuantumOfficialStageWriter.ReadPrefab(QuantumSwitchSetup.PrefabPath);
        var terrain = ScriptableObject.CreateInstance<QuantumMapTerrain>();
        try
        {
            terrain.Configure(read.Terrain, read.TerrainBounds, read.TerrainSignature);
            QuantumMapConversion Convert() => QuantumOfficialStageConverter.Convert(read.Data, terrain, null);
            Require(Convert().ContentHash == Asset.ContentHash && Asset.Agreement.StartsWith("공식2:"), "재변환 해시/공식2");
            Array.Reverse(read.Data.switches);
            Require(Convert().ContentHash == Asset.ContentHash, "작성 배열 순서와 무관한 해시");
            var button = read.Data.switches.First(s => !s.isDoor);
            button.cooldown += .125f;
            Require(Convert().ContentHash != Asset.ContentHash, "쿨다운 해시");
            var original = read.Data.switches;
            foreach (var mutate in new Action<QuantumStageSwitchSource[]>[]
            {
                s => s[0] = null,
                s => s[0].id = s[1].id,
                s => s[0].targetGroup = "missing",
                s => s[0].size = Vector2.zero,
                s => s.First(b => b.isDoor).targetGroup = s.First(b => !b.isDoor).targetGroup,
                s => s.First(b => !b.isDoor).detectBoxes = true,
            })
            {
                read.Data.switches = JsonUtility.FromJson<QuantumOfficialStageData>(JsonUtility.ToJson(read.Data)).switches;
                mutate(read.Data.switches);
                bool rejected = false;
                try { Convert(); }
                catch (InvalidOperationException error) { rejected = error.Message.Contains("위치="); }
                Require(rejected, "잘못된 ID/참조/크기/구성 위치와 원인");
                read.Data.switches = original;
            }
            File.WriteAllText("Logs/QuantumSwitchContent.txt", "통과: 공식2 재변환·순서 독립·쿨다운 해시·거부6종");
        }
        finally { UnityEngine.Object.DestroyImmediate(terrain); }
    }

    private static Quantum.Input InputFor(Frame frame, int tick, int player)
    {
        if (frame == null || frame.FlipFriendsMotionStartedFrame == 0) return default;
        int age = tick - frame.FlipFriendsMotionStartedFrame;
        var players = frame.Filter<Transform2D, FlipFriendsPlayer>();
        while (players.Next(out _, out var transform, out var state))
        {
            if ((int)state.Player != player || state.IsFinished) continue;
            if (age < 80) return new Quantum.Input { Jump = state.HomePosition.X == -3 && age >= 45 && age < 65 };
            FP gap = 11 - transform.Position.X;
            return new Quantum.Input
            {
                Horizontal = FPMath.Clamp(gap * 3 - state.Velocity.X * 2, -FP._1, FP._1),
                Vertical = FPMath.Abs(gap) < FP._0_50 ? 1 : 0,
            };
        }
        return default;
    }

    private static string State(Frame frame)
    {
        var parts = new List<string>();
        var motions = frame.Filter<FlipFriendsMotion>();
        while (motions.Next(out _, out var motion))
            if (frame.RuntimeConfig.Motions[motion.ContentIndex].Kind == 4)
                parts.Add($"{motion.ContentIndex}:판정={motion.Collidable},세대={motion.Revision}");
        var buttons = frame.Filter<FlipFriendsStageSwitch>();
        while (buttons.Next(out _, out var button))
            parts.Add($"{button.ContentIndex}:누름={button.Active},접촉={button.Contacts},준비={button.ReadyFrame}");
        return string.Join(" ", parts) + $" 완료={frame.FlipFriendsCompletedFrame}";
    }

    private static void RequireTransitions(Frame frame)
    {
        var revisions = new Dictionary<string, int>();
        var states = frame.Filter<FlipFriendsMotion>();
        while (states.Next(out _, out var state))
            revisions[frame.RuntimeConfig.Motions[state.ContentIndex].ToggleGroup] = state.Revision;
        int complete = frame.FlipFriendsCompletedFrame;
        Require(complete > 0 && revisions["manual"] == 1 && revisions["door"] >= 2 && revisions["mixed"] >= 1,
            $"수동={revisions["manual"]}, 문={revisions["door"]}, 혼합={revisions["mixed"]}, 완료={complete}");
    }

    public static async void Run()
    {
        var session = new QuantumGameSession();
        QuantumRunner replay = null;
        Status = "실행 중";
        var expected = new Dictionary<int, ulong>();
        var actual = new Dictionary<int, ulong>();
        var trace = new List<string>();
        var replayTrace = new List<string>();
        string previous = null;
        string previousReplay = null;
        try
        {
            Require(EditorApplication.isPlaying, "Play 상태 필요");
            session.InputSource = (tick, slot) => InputFor(session.Runner?.Game.Frames.Verified, tick, slot);
            using (QuantumCallback.SubscribeManual<CallbackSimulateFinished>(callback =>
            {
                var frame = callback.Frame;
                if (!frame.IsVerified || frame.Number > Horizon) return;
                if (callback.Game == session.Runner?.Game)
                {
                    if (frame.Number % ChecksumInterval == 0) expected[frame.Number] = frame.CalculateChecksum();
                    if (frame.Number < ChecksumInterval) return;
                    string state = State(frame);
                    if (state != previous) trace.Add($"{frame.Number} {state}");
                    previous = state;
                }
                if (callback.Game == replay?.Game)
                {
                    if (frame.Number % ChecksumInterval == 0) actual[frame.Number] = frame.CalculateChecksum();
                    if (frame.Number < ChecksumInterval) return;
                    string state = State(frame);
                    if (state != previousReplay) replayTrace.Add($"{frame.Number} {state}");
                    previousReplay = state;
                }
            }))
            {
                await session.StartLocalAsync(Config());
                session.Runner.IsSessionUpdateDisabled = true;
                for (int i = 0; i < Horizon + 200 && !expected.ContainsKey(Horizon); i++) session.Runner.Service(1f / 60);
                var final = session.Runner.Game.Frames.Verified;
                int complete = final.FlipFriendsCompletedFrame;
                RequireTransitions(final);
                var record = session.Runner.Game.GetRecordedReplay(includeChecksums: true);
                var serializer = new QuantumUnityJsonSerializer();
                replay = QuantumRunner.StartGame(new SessionRunner.Arguments
                {
                    RunnerId = "FF_SWITCH_REPLAY", RuntimeConfig = serializer.ConfigFromByteArray<RuntimeConfig>(record.RuntimeConfigData.Decode(), compressed: true),
                    SessionConfig = record.DeterministicConfig, ReplayProvider = record.CreateInputProvider(),
                    InitialTick = record.InitialTick, FrameData = record.InitialFrameData, GameMode = DeterministicGameMode.Replay, PlayerCount = 2,
                });
                replay.IsSessionUpdateDisabled = true;
                for (int i = 0; i < Horizon * 2 && !actual.ContainsKey(Horizon); i++) replay.Service(i % 2 == 0 ? 1f / 30 : 1f / 120);
                Require(expected.Count == 20 && expected.Count == actual.Count &&
                    expected.All(p => actual.TryGetValue(p.Key, out ulong checksum) && checksum == p.Value),
                    $"입력 기록/재생 확정 체크섬 (기록 {expected.Count}, 재생 {actual.Count})");
                Require(trace.SequenceEqual(replayTrace), "스위치·문·지형·완료 전이 프레임");
                File.WriteAllText("Logs/QuantumSwitchReplay.json", JsonUtility.ToJson(record));
                File.WriteAllLines("Logs/QuantumSwitchChecksums.txt", expected.Select(p => $"{p.Key}: {p.Value}"));
                int direct = QuantumSwitchDirectChecks.Run(final);
                Status = $"통과: 별도 재생 확정20개·수동/문/혼합 상태·전원 완료={complete}·직접{direct}종";
            }
        }
        catch (Exception error) { Status = "실패: " + error; Debug.LogException(error); }
        finally
        {
            if (replay != null) await replay.ShutdownAsync();
            await session.StopAsync();
            File.WriteAllLines("Logs/QuantumSwitchTrace.txt", trace);
            File.WriteAllLines("Logs/QuantumSwitchReplayTrace.txt", replayTrace);
            File.WriteAllText("Logs/QuantumSwitchChecks.txt", Status);
        }
    }

    public static void RunMain() =>
        QuantumMotionChecks.RunOfficialMain(QuantumSwitchSetup.StageId, InputFor, "QuantumSwitchMain");

    public static void RunCloud() =>
        QuantumMotionChecks.RunOfficialCloud(Config, InputFor, State, "QuantumSwitchCloud");
}
