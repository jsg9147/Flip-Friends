using System;
using System.IO;
using Photon.Deterministic;
using Quantum;
using UnityEditor;
using UnityEngine;

public static class QuantumReplayCheck
{
    [MenuItem("Tools/Flip Friends/Quantum/Check Recorded Replay")]
    public static void Run()
    {
        var source = QuantumRunner.Default;
        if (!EditorApplication.isPlaying || source?.Game.Frames.Verified == null)
            throw new InvalidOperationException("Quantum 테스트 씬을 Play한 뒤 실행하세요.");
        if (source.Session.GameMode != DeterministicGameMode.Local)
            throw new InvalidOperationException("이 검사는 오프라인 테스트 세션에서만 실행합니다.");
        var target = source.Game.Frames.Verified.Number;
        if (target < 120 || target > 10000)
            throw new InvalidOperationException("확정 프레임 120~10000 사이에서 검사하세요.");
        var expected = source.Game.Frames.Verified.CalculateChecksum();
        var record = source.Game.GetRecordedReplay(includeChecksums: true);
        var serializer = new QuantumUnityJsonSerializer();
        var isDisabled = source.IsSessionUpdateDisabled;
        source.IsSessionUpdateDisabled = true;
        QuantumRunner replay = null;
        ulong? actual = null;
        int players = 0;
        try
        {
            using (QuantumCallback.SubscribeManual<CallbackSimulateFinished>(callback =>
            {
                if (callback.Game != replay?.Game || !callback.Frame.IsVerified || callback.Frame.Number != target) return;
                actual = callback.Frame.CalculateChecksum();
                var query = callback.Frame.Filter<Transform2D, FlipFriendsPlayer>();
                while (query.Next(out var entity, out var transform, out var player)) players++;
            }))
            {
                replay = QuantumRunner.StartGame(new SessionRunner.Arguments
                {
                    RunnerId = "FLIP_FRIENDS_REPLAY_CHECK",
                    RuntimeConfig = serializer.ConfigFromByteArray<RuntimeConfig>(record.RuntimeConfigData.Decode(), compressed: true),
                    SessionConfig = record.DeterministicConfig,
                    ReplayProvider = record.CreateInputProvider(),
                    GameMode = DeterministicGameMode.Replay,
                    PlayerCount = record.DeterministicConfig.PlayerCount,
                    InitialTick = record.InitialTick,
                    FrameData = record.InitialFrameData
                });
                replay.IsSessionUpdateDisabled = true;
                for (int i = 0; i < target + 120 && !actual.HasValue; i++) replay.Service(1.0f / 60);
            }
            if (!actual.HasValue || actual.Value != expected || players != 2)
                throw new InvalidOperationException($"입력 재생 검증 실패: frame={target}, expected={expected}, actual={actual}, players={players}");
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/QuantumMovementReplay.json", JsonUtility.ToJson(record));
            File.WriteAllText("Logs/QuantumReplayCheck.json", $"{{\"frame\":{target},\"expected\":\"{expected}\",\"actual\":\"{actual}\",\"players\":{players}}}");
            Debug.Log($"[QuantumTest] 입력 재생 통과: 확정 프레임 {target}, 체크섬 {expected}, 플레이어 {players}명");
        }
        finally
        {
            replay?.Shutdown();
            source.IsSessionUpdateDisabled = isDisabled;
        }
    }
}
