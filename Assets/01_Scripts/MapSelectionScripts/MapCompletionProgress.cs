using System;
using System.IO;
using UnityEngine;

public static class MapCompletionProgress
{
    private const string SaveDirectoryName = "Progress";
    private static MapCompletionRepository repository;

    public static event Action Changed;

    public static MapCompletionState GetState(MapCompletionTarget target) =>
        Repository.GetState(target);

    public static CurrentRevisionVerificationState GetVerificationState(
        MapCompletionTarget target) =>
        Repository.GetCurrentRevisionVerification(target);

    public static bool TryRecord(MapCompletionTarget target)
    {
        if (Repository.TryRecordCompletion(target, out string error))
            return true;

        Debug.LogError(
            $"맵 완료 결과를 로컬에 저장하지 못했습니다: " +
            $"map={target.MapKey}, revision={target.Revision}, reason={error}");
        return false;
    }

    public static RoomCreationEligibility EvaluateRoomCreation(
        bool isPublicRoom,
        int maximumPlayers,
        LobbyMapMetadata metadata,
        MapCompletionTarget completionTarget)
    {
        CurrentRevisionVerificationState verification =
            Repository.GetCurrentRevisionVerification(completionTarget);
        RoomCreationEligibility result = RoomCreationRules.Evaluate(
            isPublicRoom,
            maximumPlayers,
            metadata,
            completionTarget,
            verification);
        if (!result.CanCreate &&
            result.BlockReason == RoomCreationBlockReason.CompletionStatusUnavailable)
        {
            Debug.LogWarning(
                $"완료 기록을 확인할 수 없어 공개방 생성을 거부했습니다: " +
                $"loadState={Repository.LoadState}, map={completionTarget.MapKey}");
        }

        return result;
    }

    private static MapCompletionRepository Repository
    {
        get
        {
            if (repository != null) return repository;

            string directory = Path.Combine(
                Application.persistentDataPath, SaveDirectoryName);
            repository = new MapCompletionRepository(
                directory,
                logWarning: message => Debug.LogWarning(message),
                logError: (message, exception) =>
                {
                    Debug.LogError(message);
                    Debug.LogException(exception);
                });
            repository.Changed += () => Changed?.Invoke();
            return repository;
        }
    }
}

public static class MapCompletionDisplay
{
    public static string GetLabel(MapCompletionState state)
    {
        return state switch
        {
            MapCompletionState.NotCompleted => "미완료",
            MapCompletionState.PreviousRevisionCompleted => "과거 리비전 완료",
            MapCompletionState.CurrentRevisionCompleted => "현재 리비전 완료",
            _ => "완료 상태 확인 불가"
        };
    }
}
