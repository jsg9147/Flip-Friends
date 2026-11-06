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

public static class QuantumCustomMapChecks
{
    public const int TargetFrame = 720;
    public static string Status { get; private set; } = "미실행";

    public static Quantum.Input InputFor(int frame, int slot)
    {
        int direction = frame < 60 ? 0 : frame < 210 ? 1 : frame < 420 ? -1 : 1;
        return new Quantum.Input { Horizontal = slot == 0 ? direction : -direction,
            Jump = frame % 90 >= 30 && frame % 90 < 40 };
    }

    [MenuItem("Tools/Flip Friends/Quantum/Check Custom Map Conversion And Replay")]
    public static void Run()
    {
        if (!EditorApplication.isPlaying) throw new InvalidOperationException("커스텀 맵 시험 씬을 Play한 뒤 실행하세요.");
        var palette = AssetDatabase.LoadAssetAtPath<MapEditorPalette>("Assets/07_ScriptableObject/MapEditorPalette.asset");
        var source = AssetDatabase.LoadAssetAtPath<TextAsset>(QuantumCustomMapSetup.SourcePath);
        MapData Fresh() => JsonUtility.FromJson<MapData>(source.text);
        var baseline = QuantumMapConverter.Convert(Fresh(), palette);
        var reversed = Fresh(); reversed.objects.Reverse();
        Require(baseline.CanonicalContent == QuantumMapConverter.Convert(reversed, palette).CanonicalContent, "역순 생성 결과");
        for (int i = 0; i < 10; i++) Require(baseline.ContentHash == QuantumMapConverter.Convert(Fresh(), palette).ContentHash, "반복 변환");
        var moved = Fresh(); moved.objects[3].position.x += 0.25f;
        Require(baseline.ContentHash != QuantumMapConverter.Convert(moved, palette).ContentHash, "지형 변경 해시");
        var rotated = Fresh(); rotated.objects[4].rotation = 90;
        var turn = QuantumMapConverter.Convert(rotated, palette);
        Require(turn.Boxes.Any(box => box.Size.X == 3 && box.Size.Y == 1), "90도 회전의 크기 교환");
        var flipped = Fresh(); flipped.objects[6].scale.x *= -1;
        var flip = QuantumMapConverter.Convert(flipped, palette);
        Require(flip.Boxes.Any(box => box.Size.X == 13 && box.Size.Y == 1), "음수 스케일 크기");
        Require(QuantumMapConverter.Q(1f / 131072).RawValue == 1 && QuantumMapConverter.Q(-1f / 131072).RawValue == -1, "고정소수점 중간값 반올림");
        var fractional = Fresh(); fractional.objects[4].scale.x = 1.00002f;
        Require(QuantumMapConverter.Convert(fractional, palette).Boxes.All(box => box.Size.X.RawValue % 2 == 0 && box.Size.Y.RawValue % 2 == 0), "반너비 베이크 격자");
        Reject(Fresh(), palette, data => data.objects[3].prefabID = "interactive.box");
        Reject(Fresh(), palette, data => data.objects[3].rotation = 45);
        Reject(Fresh(), palette, data => data.objects[3].scale.x = float.NaN);
        Reject(Fresh(), palette, data => data.objects[3].scale.y = 0);
        Reject(Fresh(), palette, data => data.objects[3].position.x = float.PositiveInfinity);
        Reject(Fresh(), palette, data => data.objects[3].position.z = 1);
        Reject(Fresh(), palette, data => data.version = "9");
        bool mismatch = false;
        try { QuantumMapAgreement.RequireMatch("1:1:" + baseline.ContentHash, "2:1:" + baseline.ContentHash); }
        catch (InvalidOperationException) { mismatch = true; }
        Require(mismatch, "변환 버전 불일치 거부");
        mismatch = false;
        try { QuantumMapAgreement.RequireMatch("1:1:" + baseline.ContentHash, "1:1:다른해시"); }
        catch (InvalidOperationException) { mismatch = true; }
        Require(mismatch, "해시 불일치 거부");
        mismatch = false;
        try { QuantumMapAgreement.RequireMatch("1:1:" + baseline.ContentHash, "1:2:" + baseline.ContentHash); }
        catch (InvalidOperationException) { mismatch = true; }
        Require(mismatch, "팔레트 버전 불일치 거부");
        CheckUnsupportedCollider(Fresh(), palette);
        RunReplay(baseline.ContentHash);
    }

    private static void CheckUnsupportedCollider(MapData data, MapEditorPalette original)
    {
        var palette = ScriptableObject.CreateInstance<MapEditorPalette>();
        var prefab = new GameObject("미지원 콜라이더 검사");
        try
        {
            prefab.SetActive(false);
            prefab.AddComponent<CircleCollider2D>();
            palette.entries = original.entries.Select(entry => new PaletteEntry
            {
                id = entry.id, prefab = entry.id == QuantumMapConverter.Ground ? prefab : entry.prefab,
                validationRule = entry.validationRule
            }).ToList();
            prefab.SetActive(true);
            Reject(data, palette, _ => { });
        }
        finally { UnityEngine.Object.DestroyImmediate(prefab); UnityEngine.Object.DestroyImmediate(palette); }
    }

    private static void Reject(MapData data, MapEditorPalette palette, Action<MapData> change)
    {
        change(data);
        bool rejected = false;
        try { QuantumMapConverter.Convert(data, palette); }
        catch (InvalidOperationException error) { rejected = !string.IsNullOrEmpty(error.Message); }
        Require(rejected, "잘못된 데이터 거부");
    }

    public static void RunReplay(string hash, bool isRopeMap = false)
    {
        int targetFrame = isRopeMap ? QuantumRopeChecks.TargetFrame : TargetFrame;
        var bootstrap = UnityEngine.Object.FindAnyObjectByType<QuantumTestBootstrap>();
        if (bootstrap == null) throw new InvalidOperationException("독립 Quantum 시험 씬이 필요합니다.");
        var config = JsonUtility.FromJson<RuntimeConfig>(JsonUtility.ToJson(bootstrap.Config));
        AssetDatabase.LoadAssetAtPath<QuantumCustomMapAsset>(isRopeMap ? QuantumRopeTestSetup.ManifestPath : QuantumCustomMapSetup.ManifestPath).ValidateAndApply(config);
        var session = JsonUtility.FromJson<DeterministicSessionConfig>(JsonUtility.ToJson(QuantumDeterministicSessionConfigAsset.DefaultConfig));
        session.UpdateFPS = 60; session.PlayerCount = 2;
        QuantumRunner original = null, replay = null;
        IQuantumMovementEvidence evidence = isRopeMap ? new QuantumRopeChecks.RopeEvidence() : new MovementEvidence();
        var expected = new Dictionary<int, ulong>();
        var actual = new Dictionary<int, ulong>();
        try
        {
            using (QuantumCallback.SubscribeManual<CallbackPollInput>(callback =>
            {
                if (callback.Game == original?.Game)
                    callback.SetInput(isRopeMap ? QuantumRopeChecks.InputFor(callback.Frame, callback.PlayerSlot) : InputFor(callback.Frame, callback.PlayerSlot), DeterministicInputFlags.Repeatable);
            }))
            using (QuantumCallback.SubscribeManual<CallbackSimulateFinished>(callback =>
            {
                if (!callback.Frame.IsVerified || callback.Frame.Number > targetFrame) return;
                if (callback.Game == original?.Game)
                {
                    evidence.Observe(callback.Frame);
                    if (callback.Frame.Number % 120 == 0) expected[callback.Frame.Number] = callback.Frame.CalculateChecksum();
                }
                if (callback.Game == replay?.Game && callback.Frame.Number % 120 == 0)
                    actual[callback.Frame.Number] = callback.Frame.CalculateChecksum();
            }))
            {
                original = QuantumRunner.StartGame(new SessionRunner.Arguments
                {
                    RunnerFactory = QuantumRunnerUnityFactory.DefaultFactory,
                    GameParameters = QuantumRunnerUnityFactory.CreateGameParameters,
                    RunnerId = "FF_CUSTOM_MAP_RECORD", RuntimeConfig = config, SessionConfig = session,
                    GameMode = DeterministicGameMode.Local, PlayerCount = 2,
                    RecordingFlags = RecordingFlags.Input | RecordingFlags.Checksums
                });
                original.IsSessionUpdateDisabled = true;
                for (int i = 0; i < 60 && !original.Session.IsRunning; i++) original.Service(1f / 60);
                Require(original.Session.IsRunning, "로컬 세션 시작 완료");
                original.Game.AddPlayer(0, new RuntimePlayer()); original.Game.AddPlayer(1, new RuntimePlayer());
                for (int i = 0; i < targetFrame + 240 && !expected.ContainsKey(targetFrame); i++) original.Service(1f / 60);
                evidence.AssertComplete();
                Require(expected.ContainsKey(targetFrame), "확정 프레임 도달");
                var record = original.Game.GetRecordedReplay(includeChecksums: true);
                var serializer = new QuantumUnityJsonSerializer();
                replay = QuantumRunner.StartGame(new SessionRunner.Arguments
                {
                    RunnerFactory = QuantumRunnerUnityFactory.DefaultFactory,
                    GameParameters = QuantumRunnerUnityFactory.CreateGameParameters,
                    RunnerId = "FF_CUSTOM_MAP_REPLAY", RuntimeConfig = serializer.ConfigFromByteArray<RuntimeConfig>(record.RuntimeConfigData.Decode(), compressed: true),
                    SessionConfig = record.DeterministicConfig, ReplayProvider = record.CreateInputProvider(),
                    InitialTick = record.InitialTick, FrameData = record.InitialFrameData,
                    GameMode = DeterministicGameMode.Replay, PlayerCount = 2
                });
                replay.IsSessionUpdateDisabled = true;
                for (int i = 0; i < targetFrame + 240 && !actual.ContainsKey(targetFrame); i++) replay.Service(1f / 60);
                Require(expected.Count == targetFrame / 120 && actual.Count == expected.Count && expected.All(pair => actual.TryGetValue(pair.Key, out var value) && value == pair.Value), "동일 확정 프레임 재생 체크섬");
                Directory.CreateDirectory("Logs");
                string prefix = isRopeMap ? "Logs/QuantumRope" : "Logs/QuantumCustomMap";
                File.WriteAllText(prefix + "Replay.json", JsonUtility.ToJson(record));
                Status = $"통과: 맵={hash}, 변환={QuantumMapConverter.ConversionVersion} 팔레트={QuantumMapConverter.PaletteVersion}, Unity={Application.unityVersion}, 60Hz, 확정 120~{targetFrame}(120간격) 재생 일치\n" +
                    string.Join("\n", expected.Select(pair => $"{pair.Key}: {pair.Value}")) + "\n" + evidence.Summary;
                File.WriteAllText(prefix + "Checks.txt", Status);
                Debug.Log("[QuantumMap] " + Status);
            }
        }
        finally { replay?.Shutdown(); original?.Shutdown(); }
    }

    private static void Require(bool value, string label)
    {
        if (!value) throw new InvalidOperationException("자동 검사 실패: " + label);
    }

    public sealed class MovementEvidence : IQuantumMovementEvidence
    {
        private readonly float[] minX = { float.PositiveInfinity, float.PositiveInfinity };
        private readonly float[] maxX = { float.NegativeInfinity, float.NegativeInfinity };
        private readonly bool[] floor = new bool[2], ceiling = new bool[2], spawn = new bool[2];
        private readonly bool[] left = new bool[2], right = new bool[2];
        private readonly int[] firstFrame = new int[2];
        public string Summary => string.Join("\n", Enumerable.Range(0, 2).Select(i =>
            $"플레이어 {i}: 생성={spawn[i]}(프레임 {firstFrame[i]}), 바닥={floor[i]}, 천장={ceiling[i]}, 좌벽={left[i]}, 우벽={right[i]}, X=[{minX[i]:F5},{maxX[i]:F5}]"));

        public void Observe(Frame frame)
        {
            var query = frame.Filter<Transform2D, FlipFriendsPlayer>();
            while (query.Next(out var entity, out var transform, out var player))
            {
                int slot = (int)player.Player;
                Require(slot >= 0 && slot < 2, "플레이어 슬롯");
                float x = transform.Position.X.AsFloat, y = transform.Position.Y.AsFloat;
                if (firstFrame[slot] == 0)
                {
                    firstFrame[slot] = frame.Number;
                    var expected = frame.RuntimeConfig.CustomMapSpawns[slot];
                    spawn[slot] = Math.Abs(x - expected.X.AsFloat) < 0.067f && Math.Abs(y - expected.Y.AsFloat) < 0.02f;
                }
                Require(x >= -5.101f && x <= 5.101f && y >= 0.499f && y <= 1.501f, "벽·바닥·천장 관통 없음");
                minX[slot] = Math.Min(minX[slot], x); maxX[slot] = Math.Max(maxX[slot], x);
                floor[slot] |= player.IsGrounded && Math.Abs(y - 0.5f) < 0.01f;
                left[slot] |= player.IsTouchingLeft;
                right[slot] |= player.IsTouchingRight;
                ceiling[slot] |= Math.Abs(y - 1.5f) < 0.01f && player.Velocity.Y == 0 && !player.IsGrounded;
            }
        }

        public void AssertComplete()
        {
            for (int i = 0; i < 2; i++)
                Require(spawn[i] && floor[i] && ceiling[i] && left[i] && right[i] && minX[i] < -5.09f && maxX[i] > 5.09f, "두 플레이어 생성·점프·바닥·양쪽 벽·천장 도달: " + Summary);
        }
    }
}
