using NUnit.Framework;

public sealed class MapAvailabilityCheckTests
{
    private const double ManifestTimeout = 10;
    private const double TransferTimeout = 30;
    private const uint Generation = 7;
    private const string MapId = "0123456789abcdef0123456789abcdef";
    private const string TransferId = "transfer-a";
    private const string ContentHash = "hash-a";
    private const string Host = "host";
    private const string Guest = "guest";

    private MapAvailabilityCheck<string> check;
    private double now;

    [SetUp]
    public void SetUp()
    {
        check = new MapAvailabilityCheck<string>(ManifestTimeout, TransferTimeout);
        now = 100;
    }

    [Test]
    public void 모든참여자가보유하면완료된다()
    {
        Begin(Host, Guest);

        Assert.That(Manifest(Host, MapContentAvailability.Available).Kind,
            Is.EqualTo(MapAvailabilityStepKind.Accepted));
        Assert.That(Manifest(Guest, MapContentAvailability.Available).Kind,
            Is.EqualTo(MapAvailabilityStepKind.Completed));
        Assert.That(check.IsActive, Is.False);
    }

    [Test]
    public void 누락참여자에게만전송하고완료응답으로끝난다()
    {
        Begin(Host, Guest);
        Manifest(Host, MapContentAvailability.Available);

        Assert.That(Manifest(Guest, MapContentAvailability.Missing).Kind,
            Is.EqualTo(MapAvailabilityStepKind.TransferRequired));
        Assert.That(check.GetTransferPendingParticipants(), Is.EqualTo(new[] { Guest }));
        Assert.That(check.IsTransferPending(Host), Is.False);

        Assert.That(TransferResult(Guest, MapTransferFailure.None).Kind,
            Is.EqualTo(MapAvailabilityStepKind.Completed));
    }

    [Test]
    public void manifest응답이없으면제한시간에실패한다()
    {
        Begin(Host, Guest);
        Manifest(Host, MapContentAvailability.Available);

        Assert.That(check.Tick(now + ManifestTimeout - 0.01).Kind,
            Is.EqualTo(MapAvailabilityStepKind.None));

        MapAvailabilityStep step = check.Tick(now + ManifestTimeout);
        Assert.That(step.Kind, Is.EqualTo(MapAvailabilityStepKind.Failed));
        Assert.That(step.Failure, Is.EqualTo(MapTransferFailure.TransferTimedOut));
        Assert.That(step.Reason, Is.EqualTo(MapAvailabilityFailureReason.ManifestTimedOut));
        Assert.That(check.IsActive, Is.False);
    }

    [Test]
    public void 전송제한시간은첫청크송신부터잰다()
    {
        BeginTransfer();

        // 큐 대기만으로는 시간 초과가 나지 않는다.
        Assert.That(check.Tick(now + 1000).Kind, Is.EqualTo(MapAvailabilityStepKind.None));

        double sentAt = now + 1000;
        check.NotifyChunkSent(sentAt);
        check.NotifyChunkSent(sentAt + 20);
        Assert.That(check.Tick(sentAt + TransferTimeout - 0.01).Kind,
            Is.EqualTo(MapAvailabilityStepKind.None));

        MapAvailabilityStep step = check.Tick(sentAt + TransferTimeout);
        Assert.That(step.Kind, Is.EqualTo(MapAvailabilityStepKind.Failed));
        Assert.That(step.Reason, Is.EqualTo(MapAvailabilityFailureReason.TransferTimedOut));
    }

    [Test]
    public void manifest대기중에는청크송신알림이전송시계를시작하지않는다()
    {
        Begin(Host, Guest);
        check.NotifyChunkSent(now);
        Manifest(Host, MapContentAvailability.Available);
        Manifest(Guest, MapContentAvailability.HashMismatch);

        Assert.That(check.Tick(now + TransferTimeout).Kind,
            Is.EqualTo(MapAvailabilityStepKind.None));
    }

    [Test]
    public void 취소하면이후응답과시간초과를무시한다()
    {
        BeginTransfer();
        check.NotifyChunkSent(now);

        check.Cancel();

        Assert.That(check.IsActive, Is.False);
        Assert.That(check.IsTransferPending(Guest), Is.False);
        Assert.That(TransferResult(Guest, MapTransferFailure.None).Kind,
            Is.EqualTo(MapAvailabilityStepKind.Ignored));
        Assert.That(check.Tick(now + TransferTimeout).Kind,
            Is.EqualTo(MapAvailabilityStepKind.None));
    }

    [Test]
    public void 새검사는이전세대응답과마감을무시한다()
    {
        Begin(Host, Guest);
        double secondStart = now + ManifestTimeout - 1;
        check.Begin(Generation + 1, MapId, TransferId, ContentHash,
            new[] { Host, Guest }, secondStart);

        Assert.That(Manifest(Host, MapContentAvailability.Available).Kind,
            Is.EqualTo(MapAvailabilityStepKind.Ignored));
        Assert.That(check.Tick(now + ManifestTimeout).Kind,
            Is.EqualTo(MapAvailabilityStepKind.None));
        Assert.That(check.Tick(secondStart + ManifestTimeout).Kind,
            Is.EqualTo(MapAvailabilityStepKind.Failed));
    }

    [Test]
    public void 중복manifest와명단밖참여자는상태를바꾸지않는다()
    {
        Begin(Host, Guest);
        Manifest(Host, MapContentAvailability.Missing);

        Assert.That(Manifest(Host, MapContentAvailability.Available).Kind,
            Is.EqualTo(MapAvailabilityStepKind.Ignored));
        Assert.That(Manifest("stranger", MapContentAvailability.Available).Kind,
            Is.EqualTo(MapAvailabilityStepKind.None));
        Assert.That(check.IsTransferPending(Host), Is.True);
        Assert.That(check.IsAwaitingManifests, Is.True);
    }

    [Test]
    public void 식별자가다른manifest는검증실패다()
    {
        Begin(Host);

        MapAvailabilityStep step = check.ReceiveManifest(
            Host, Generation, MapId, "other-transfer", ContentHash,
            MapContentAvailability.Available);

        Assert.That(step.Kind, Is.EqualTo(MapAvailabilityStepKind.Failed));
        Assert.That(step.Failure, Is.EqualTo(MapTransferFailure.MapValidationFailed));
        Assert.That(step.Reason, Is.EqualTo(MapAvailabilityFailureReason.IdentityMismatch));
    }

    [Test]
    public void 로컬데이터가잘못되면검증실패다()
    {
        Begin(Host, Guest);

        MapAvailabilityStep step = Manifest(Guest, MapContentAvailability.InvalidLocalData);

        Assert.That(step.Kind, Is.EqualTo(MapAvailabilityStepKind.Failed));
        Assert.That(step.Reason, Is.EqualTo(MapAvailabilityFailureReason.InvalidLocalData));
    }

    [Test]
    public void 참여자가보고한전송실패를그대로전달한다()
    {
        BeginTransfer();

        MapAvailabilityStep step = TransferResult(Guest, MapTransferFailure.HashVerificationFailed);

        Assert.That(step.Kind, Is.EqualTo(MapAvailabilityStepKind.Failed));
        Assert.That(step.Failure, Is.EqualTo(MapTransferFailure.HashVerificationFailed));
        Assert.That(step.Reason,
            Is.EqualTo(MapAvailabilityFailureReason.ParticipantReportedFailure));
    }

    [Test]
    public void 전송대상이아닌참여자의완료응답을무시한다()
    {
        BeginTransfer();

        Assert.That(TransferResult(Host, MapTransferFailure.None).Kind,
            Is.EqualTo(MapAvailabilityStepKind.Ignored));
        Assert.That(check.IsTransferPending(Guest), Is.True);
    }

    [Test]
    public void 여러참여자전송은모두완료되어야끝난다()
    {
        const string third = "third";
        Begin(Host, Guest, third);
        Manifest(Host, MapContentAvailability.Available);
        Manifest(Guest, MapContentAvailability.Missing);
        Manifest(third, MapContentAvailability.HashMismatch);

        Assert.That(TransferResult(Guest, MapTransferFailure.None).Kind,
            Is.EqualTo(MapAvailabilityStepKind.Accepted));
        Assert.That(TransferResult(Guest, MapTransferFailure.None).Kind,
            Is.EqualTo(MapAvailabilityStepKind.Ignored));
        Assert.That(TransferResult(third, MapTransferFailure.None).Kind,
            Is.EqualTo(MapAvailabilityStepKind.Completed));
    }

    [Test]
    public void 참여자가없으면시작하지않는다()
    {
        Assert.That(check.Begin(Generation, MapId, TransferId, ContentHash,
            new string[0], now), Is.False);
        Assert.That(check.IsActive, Is.False);
    }

    [Test]
    public void SelectionUploadLease_마감시각이지나면만료된다()
    {
        var lease = new SelectionUploadLease();
        lease.Begin(TransferId, MapId, ContentHash, now + 12);

        Assert.That(lease.HasExpired(now + 11.99), Is.False);
        Assert.That(lease.HasExpired(now + 12), Is.True);

        lease.Invalidate();
        Assert.That(lease.HasExpired(now + 100), Is.False);
    }

    private void Begin(params string[] participants)
    {
        Assert.That(
            check.Begin(Generation, MapId, TransferId, ContentHash, participants, now),
            Is.True);
    }

    private void BeginTransfer()
    {
        Begin(Host, Guest);
        Manifest(Host, MapContentAvailability.Available);
        Assert.That(Manifest(Guest, MapContentAvailability.Missing).Kind,
            Is.EqualTo(MapAvailabilityStepKind.TransferRequired));
    }

    private MapAvailabilityStep Manifest(string participant, MapContentAvailability availability) =>
        check.ReceiveManifest(
            participant, Generation, MapId, TransferId, ContentHash, availability);

    private MapAvailabilityStep TransferResult(string participant, MapTransferFailure failure) =>
        check.ReceiveTransferResult(
            participant, Generation, TransferId, MapId, ContentHash, failure);
}
