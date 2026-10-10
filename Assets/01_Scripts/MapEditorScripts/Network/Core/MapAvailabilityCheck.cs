using System;
using System.Collections.Generic;

public enum MapContentAvailability
{
    Available,
    Missing,
    HashMismatch,
    InvalidLocalData
}

public enum MapTransferFailure
{
    None,
    TransferPending,
    TransferTimedOut,
    InvalidChunk,
    HashVerificationFailed,
    MapValidationFailed,
    SelectionChanged
}

public enum MapAvailabilityStepKind
{
    // 처리할 일이 없다. 대기 중이거나 비활성 상태다.
    None,
    // 오래되었거나 중복된 응답이다. 상태는 바뀌지 않았다.
    Ignored,
    // 응답을 반영했고 다른 참여자를 기다린다.
    Accepted,
    // manifest를 모두 받았고 콘텐츠가 필요한 참여자가 있다.
    TransferRequired,
    // 모든 참여자가 맵을 보유한다.
    Completed,
    // 검사를 중단했다. 검사는 비활성 상태가 된다.
    Failed
}

public enum MapAvailabilityFailureReason
{
    None,
    IdentityMismatch,
    InvalidLocalData,
    ParticipantReportedFailure,
    ManifestTimedOut,
    TransferTimedOut
}

public readonly struct MapAvailabilityStep
{
    public MapAvailabilityStepKind Kind { get; }
    public MapTransferFailure Failure { get; }
    public MapAvailabilityFailureReason Reason { get; }

    private MapAvailabilityStep(
        MapAvailabilityStepKind kind,
        MapTransferFailure failure,
        MapAvailabilityFailureReason reason)
    {
        Kind = kind;
        Failure = failure;
        Reason = reason;
    }

    public static MapAvailabilityStep None => new(
        MapAvailabilityStepKind.None, MapTransferFailure.None, MapAvailabilityFailureReason.None);
    public static MapAvailabilityStep Ignored => new(
        MapAvailabilityStepKind.Ignored, MapTransferFailure.None, MapAvailabilityFailureReason.None);
    public static MapAvailabilityStep Accepted => new(
        MapAvailabilityStepKind.Accepted, MapTransferFailure.None, MapAvailabilityFailureReason.None);
    public static MapAvailabilityStep TransferRequired => new(
        MapAvailabilityStepKind.TransferRequired, MapTransferFailure.None,
        MapAvailabilityFailureReason.None);
    public static MapAvailabilityStep Completed => new(
        MapAvailabilityStepKind.Completed, MapTransferFailure.None,
        MapAvailabilityFailureReason.None);

    public static MapAvailabilityStep Fail(
        MapTransferFailure failure,
        MapAvailabilityFailureReason reason) =>
        new(MapAvailabilityStepKind.Failed, failure, reason);
}

// 게임 시작 전 참여자 맵 보유 검사의 서버 상태 전이.
// Mirror 연결과 Coroutine 대신 참여자 키와 외부 시계를 받아 Edit Mode에서 검증할 수 있게 한다.
public sealed class MapAvailabilityCheck<TParticipant>
{
    private enum ParticipantState
    {
        ManifestPending,
        TransferPending,
        Ready
    }

    private enum Phase
    {
        Idle,
        AwaitingManifests,
        Transferring
    }

    private readonly Dictionary<TParticipant, ParticipantState> participants = new();
    private readonly double manifestTimeoutSeconds;
    private readonly double transferTimeoutSeconds;
    private Phase phase;
    private double manifestDeadline;
    private double transferDeadline;
    private bool hasTransferDeadline;

    public uint Generation { get; private set; }
    public string MapId { get; private set; } = string.Empty;
    public string TransferId { get; private set; } = string.Empty;
    public string ContentHash { get; private set; } = string.Empty;
    public bool IsActive => phase != Phase.Idle;
    public bool IsAwaitingManifests => phase == Phase.AwaitingManifests;
    public IReadOnlyCollection<TParticipant> Participants => participants.Keys;

    public MapAvailabilityCheck(double manifestTimeoutSeconds, double transferTimeoutSeconds)
    {
        if (manifestTimeoutSeconds <= 0)
            throw new ArgumentOutOfRangeException(nameof(manifestTimeoutSeconds));
        if (transferTimeoutSeconds <= 0)
            throw new ArgumentOutOfRangeException(nameof(transferTimeoutSeconds));

        this.manifestTimeoutSeconds = manifestTimeoutSeconds;
        this.transferTimeoutSeconds = transferTimeoutSeconds;
    }

    public bool Begin(
        uint generation,
        string mapId,
        string transferId,
        string contentHash,
        IEnumerable<TParticipant> activeParticipants,
        double now)
    {
        Cancel();
        if (activeParticipants == null) return false;

        foreach (TParticipant participant in activeParticipants)
        {
            if (participant != null)
                participants.TryAdd(participant, ParticipantState.ManifestPending);
        }
        if (participants.Count == 0) return false;

        Generation = generation;
        MapId = mapId ?? string.Empty;
        TransferId = transferId ?? string.Empty;
        ContentHash = contentHash ?? string.Empty;
        manifestDeadline = now + manifestTimeoutSeconds;
        phase = Phase.AwaitingManifests;
        return true;
    }

    public MapAvailabilityStep ReceiveManifest(
        TParticipant participant,
        uint generation,
        string mapId,
        string transferId,
        string contentHash,
        MapContentAvailability availability)
    {
        if (phase != Phase.AwaitingManifests || participant == null)
            return MapAvailabilityStep.None;
        if (generation != Generation) return MapAvailabilityStep.Ignored;
        if (!MatchesIdentity(mapId, transferId, contentHash))
        {
            return Fail(
                MapTransferFailure.MapValidationFailed,
                MapAvailabilityFailureReason.IdentityMismatch);
        }
        if (!participants.TryGetValue(participant, out ParticipantState state))
            return MapAvailabilityStep.None;
        if (state != ParticipantState.ManifestPending) return MapAvailabilityStep.Ignored;

        switch (availability)
        {
            case MapContentAvailability.Available:
                participants[participant] = ParticipantState.Ready;
                break;
            case MapContentAvailability.Missing:
            case MapContentAvailability.HashMismatch:
                participants[participant] = ParticipantState.TransferPending;
                break;
            default:
                return Fail(
                    MapTransferFailure.MapValidationFailed,
                    MapAvailabilityFailureReason.InvalidLocalData);
        }

        if (HasParticipantIn(ParticipantState.ManifestPending))
            return MapAvailabilityStep.Accepted;
        if (AllParticipantsReady()) return Complete();

        phase = Phase.Transferring;
        return MapAvailabilityStep.TransferRequired;
    }

    // 전송 제한 시간은 첫 청크를 실제로 보낸 시점부터 잰다. 큐 대기 시간을 참여자 탓으로 돌리지 않기 위해서다.
    public void NotifyChunkSent(double now)
    {
        if (phase != Phase.Transferring || hasTransferDeadline) return;

        transferDeadline = now + transferTimeoutSeconds;
        hasTransferDeadline = true;
    }

    public MapAvailabilityStep ReceiveTransferResult(
        TParticipant participant,
        uint generation,
        string transferId,
        string mapId,
        string contentHash,
        MapTransferFailure failure)
    {
        if (!IsActive || participant == null ||
            generation != Generation ||
            !MatchesIdentity(mapId, transferId, contentHash) ||
            !participants.TryGetValue(participant, out ParticipantState state) ||
            state != ParticipantState.TransferPending)
            return MapAvailabilityStep.Ignored;

        if (failure != MapTransferFailure.None)
            return Fail(failure, MapAvailabilityFailureReason.ParticipantReportedFailure);

        participants[participant] = ParticipantState.Ready;
        return AllParticipantsReady() ? Complete() : MapAvailabilityStep.Accepted;
    }

    public MapAvailabilityStep Tick(double now)
    {
        if (phase == Phase.AwaitingManifests && now >= manifestDeadline)
        {
            return Fail(
                MapTransferFailure.TransferTimedOut,
                MapAvailabilityFailureReason.ManifestTimedOut);
        }
        if (phase == Phase.Transferring && hasTransferDeadline && now >= transferDeadline)
        {
            return Fail(
                MapTransferFailure.TransferTimedOut,
                MapAvailabilityFailureReason.TransferTimedOut);
        }

        return MapAvailabilityStep.None;
    }

    public bool IsTransferPending(TParticipant participant) =>
        IsActive &&
        participant != null &&
        participants.TryGetValue(participant, out ParticipantState state) &&
        state == ParticipantState.TransferPending;

    public List<TParticipant> GetTransferPendingParticipants()
    {
        var result = new List<TParticipant>();
        foreach (KeyValuePair<TParticipant, ParticipantState> pair in participants)
        {
            if (pair.Value == ParticipantState.TransferPending)
                result.Add(pair.Key);
        }

        return result;
    }

    public void Cancel()
    {
        participants.Clear();
        phase = Phase.Idle;
        hasTransferDeadline = false;
        MapId = string.Empty;
        TransferId = string.Empty;
        ContentHash = string.Empty;
    }

    private bool MatchesIdentity(string mapId, string transferId, string contentHash) =>
        mapId == MapId && transferId == TransferId && contentHash == ContentHash;

    private bool HasParticipantIn(ParticipantState target)
    {
        foreach (ParticipantState state in participants.Values)
        {
            if (state == target) return true;
        }

        return false;
    }

    private bool AllParticipantsReady() =>
        participants.Count > 0 &&
        !HasParticipantIn(ParticipantState.ManifestPending) &&
        !HasParticipantIn(ParticipantState.TransferPending);

    private MapAvailabilityStep Complete()
    {
        Cancel();
        return MapAvailabilityStep.Completed;
    }

    private MapAvailabilityStep Fail(
        MapTransferFailure failure,
        MapAvailabilityFailureReason reason)
    {
        Cancel();
        return MapAvailabilityStep.Fail(failure, reason);
    }
}
