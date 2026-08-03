using System;
using System.IO;
using System.Linq;
using NUnit.Framework;

public class MapCompletionRepositoryTests
{
    private const string OfficialMapId = "official.stage.01";
    private const string CustomMapId = "0123456789abcdef0123456789abcdef";
    private const string FirstHash =
        "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
    private const string SecondHash =
        "abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789";

    private string temporaryDirectory;

    [SetUp]
    public void SetUp()
    {
        temporaryDirectory = Path.Combine(
            Path.GetTempPath(), "FlipFriends-CompletionTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temporaryDirectory);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(temporaryDirectory))
            Directory.Delete(temporaryDirectory, true);
    }

    [Test]
    public void OfficialCompletion_FirstSaveCanBeQueried()
    {
        MapCompletionRepository repository = CreateRepository();
        MapCompletionTarget.TryCreateOfficial(OfficialMapId, 1, out var target);

        Assert.That(repository.TryRecordCompletion(target, out _), Is.True);
        Assert.That(repository.HasCompleted(target.MapKey), Is.True);
        Assert.That(repository.HasCompletedCurrentRevision(target), Is.True);
        Assert.That(CreateRepository().GetState(target),
            Is.EqualTo(MapCompletionState.CurrentRevisionCompleted));
        StringAssert.Contains("\"schemaVersion\": 1", File.ReadAllText(repository.FilePath));
    }

    [Test]
    public void OfficialCompletion_DuplicateSaveIsIdempotent()
    {
        MapCompletionRepository repository = CreateRepository();
        MapCompletionTarget.TryCreateOfficial(OfficialMapId, 1, out var target);

        repository.TryRecordCompletion(target, out _);
        string firstJson = File.ReadAllText(repository.FilePath);

        Assert.That(repository.TryRecordCompletion(target, out _), Is.True);
        Assert.That(repository.RecordCount, Is.EqualTo(1));
        Assert.That(File.ReadAllText(repository.FilePath), Is.EqualTo(firstJson));
    }

    [Test]
    public void OfficialCompletion_RevisionChangeSeparatesGeneralAndCurrentState()
    {
        MapCompletionRepository repository = CreateRepository();
        MapCompletionTarget.TryCreateOfficial(OfficialMapId, 1, out var previous);
        MapCompletionTarget.TryCreateOfficial(OfficialMapId, 2, out var current);
        repository.TryRecordCompletion(previous, out _);

        Assert.That(repository.HasCompleted(current.MapKey), Is.True);
        Assert.That(repository.HasCompletedCurrentRevision(current), Is.False);
        Assert.That(repository.GetState(current),
            Is.EqualTo(MapCompletionState.PreviousRevisionCompleted));
    }

    [Test]
    public void CustomCompletion_FirstSaveCanBeQueried()
    {
        MapCompletionRepository repository = CreateRepository();
        MapCompletionTarget.TryCreateCustom(CustomMapId, FirstHash, out var target);

        Assert.That(repository.TryRecordCompletion(target, out _), Is.True);
        Assert.That(CreateRepository().GetState(target),
            Is.EqualTo(MapCompletionState.CurrentRevisionCompleted));
    }

    [Test]
    public void CustomCompletion_HashChangeSeparatesGeneralAndCurrentState()
    {
        MapCompletionRepository repository = CreateRepository();
        MapCompletionTarget.TryCreateCustom(CustomMapId, FirstHash, out var previous);
        MapCompletionTarget.TryCreateCustom(CustomMapId, SecondHash, out var current);
        repository.TryRecordCompletion(previous, out _);

        Assert.That(repository.HasCompleted(current.MapKey), Is.True);
        Assert.That(repository.HasCompletedCurrentRevision(current), Is.False);
        Assert.That(repository.GetState(current),
            Is.EqualTo(MapCompletionState.PreviousRevisionCompleted));
    }

    [Test]
    public void StorageIdentity_SeparatesSameStringIdByMapKind()
    {
        const string sameId = "same-string-id";
        var official = new MapCompletionStorageIdentity(LobbyMapKind.Official, sameId);
        var custom = new MapCompletionStorageIdentity(LobbyMapKind.Custom, sameId);

        Assert.That(official, Is.Not.EqualTo(custom));
    }

    [Test]
    public void GameplaySession_RequiresEverySnapshottedParticipant()
    {
        GameplayCompletionSession.TryCreate(
            LobbyMapKind.Official,
            OfficialMapId,
            string.Empty,
            new[] { 10, 20 },
            out GameplayCompletionSession session);

        Assert.That(session.AreAllParticipantsComplete(new[] { 10 }), Is.False);
        Assert.That(session.AreAllParticipantsComplete(new[] { 10, 20 }), Is.True);
        Assert.That(session.AreAllParticipantsComplete(new[] { 10, 20, 30 }), Is.False);
    }

    [Test]
    public void CorruptedJson_IsQuarantinedAndRepositoryRecoversEmpty()
    {
        string filePath = GetFilePath();
        File.WriteAllText(filePath, "{ broken-json");

        MapCompletionRepository repository = CreateRepository();

        Assert.That(repository.LoadState,
            Is.EqualTo(MapCompletionLoadState.CorruptedRecovered));
        Assert.That(repository.RecordCount, Is.Zero);
        Assert.That(File.Exists(filePath), Is.False);
        Assert.That(Directory.GetFiles(temporaryDirectory, "*.corrupt.*"), Has.Length.EqualTo(1));
    }

    [Test]
    public void UnsupportedSchema_IsPreservedAndCannotBeOverwritten()
    {
        string filePath = GetFilePath();
        string json = "{\"schemaVersion\":99,\"records\":[]}";
        File.WriteAllText(filePath, json);
        MapCompletionRepository repository = CreateRepository();
        MapCompletionTarget.TryCreateOfficial(OfficialMapId, 1, out var target);

        Assert.That(repository.LoadState,
            Is.EqualTo(MapCompletionLoadState.UnsupportedSchema));
        Assert.That(repository.TryRecordCompletion(target, out _), Is.False);
        Assert.That(File.ReadAllText(filePath), Is.EqualTo(json));
    }

    [Test]
    public void InvalidMapIdRevisionAndContentHash_AreRejected()
    {
        Assert.That(MapCompletionTarget.TryCreateOfficial("stage.01", 1, out _), Is.False);
        Assert.That(MapCompletionTarget.TryCreateOfficial(OfficialMapId, 0, out _), Is.False);
        Assert.That(MapCompletionTarget.TryCreateCustom("not-guid", FirstHash, out _), Is.False);
        Assert.That(MapCompletionTarget.TryCreateCustom(CustomMapId, "not-hash", out _), Is.False);
        Assert.That(MapCompletionTarget.TryCreateCustom(
            CustomMapId, FirstHash.ToUpperInvariant(), out _), Is.False);
    }

    [Test]
    public void DuplicateRecords_AreNormalizedDuringLoad()
    {
        string record =
            "{\"mapKind\":\"Official\",\"mapId\":\"official.stage.01\"," +
            "\"completionRevision\":1,\"contentHash\":null}";
        File.WriteAllText(
            GetFilePath(),
            $"{{\"schemaVersion\":1,\"records\":[{record},{record}]}}");

        MapCompletionRepository repository = CreateRepository();

        Assert.That(repository.RecordCount, Is.EqualTo(1));
    }

    [Test]
    public void AtomicReplaceFailure_PreservesExistingRecords()
    {
        MapCompletionRepository initial = CreateRepository();
        MapCompletionTarget.TryCreateOfficial(OfficialMapId, 1, out var first);
        MapCompletionTarget.TryCreateOfficial("official.stage.02", 1, out var second);
        initial.TryRecordCompletion(first, out _);
        string originalJson = File.ReadAllText(initial.FilePath);

        var failingFileSystem = new ReplaceFailingFileSystem();
        var repository = new MapCompletionRepository(
            temporaryDirectory, failingFileSystem);

        Assert.That(repository.TryRecordCompletion(second, out _), Is.False);
        Assert.That(File.ReadAllText(repository.FilePath), Is.EqualTo(originalJson));
        Assert.That(CreateRepository().HasCompletedCurrentRevision(first), Is.True);
        Assert.That(CreateRepository().HasCompletedCurrentRevision(second), Is.False);
    }

    [Test]
    public void SuccessfulNewCompletion_RaisesOneListRefreshBoundaryEvent()
    {
        MapCompletionRepository repository = CreateRepository();
        MapCompletionTarget.TryCreateOfficial(OfficialMapId, 1, out var target);
        int refreshCount = 0;
        repository.Changed += () => refreshCount++;

        repository.TryRecordCompletion(target, out _);
        repository.TryRecordCompletion(target, out _);

        Assert.That(refreshCount, Is.EqualTo(1));
    }

    [TestCase(LobbyMapKind.Official)]
    [TestCase(LobbyMapKind.Custom)]
    public void PrivateRoom_AllowsUncompletedMap(LobbyMapKind kind)
    {
        MapCompletionRepository repository = CreateRepository();
        MapCompletionTarget target = CreateTarget(kind, current: true);

        RoomCreationEligibility result = EvaluateRoomCreation(
            repository, false, 4, 1, target);

        Assert.That(result.CanCreate, Is.True);
    }

    [TestCase(LobbyMapKind.Official)]
    [TestCase(LobbyMapKind.Custom)]
    public void PublicRoom_RejectsUncompletedMapWithSameRule(LobbyMapKind kind)
    {
        MapCompletionRepository repository = CreateRepository();
        MapCompletionTarget target = CreateTarget(kind, current: true);

        RoomCreationEligibility result = EvaluateRoomCreation(
            repository, true, 4, 1, target);

        Assert.That(result.CanCreate, Is.False);
        Assert.That(result.BlockReason,
            Is.EqualTo(RoomCreationBlockReason.CurrentRevisionNotCompleted));
    }

    [TestCase(LobbyMapKind.Official)]
    [TestCase(LobbyMapKind.Custom)]
    public void PublicRoom_RejectsPreviousRevisionCompletion(LobbyMapKind kind)
    {
        MapCompletionRepository repository = CreateRepository();
        repository.TryRecordCompletion(CreateTarget(kind, current: false), out _);
        MapCompletionTarget current = CreateTarget(kind, current: true);

        RoomCreationEligibility result = EvaluateRoomCreation(
            repository, true, 4, 1, current);

        Assert.That(repository.GetState(current),
            Is.EqualTo(MapCompletionState.PreviousRevisionCompleted));
        Assert.That(result.CanCreate, Is.False);
    }

    [TestCase(LobbyMapKind.Official)]
    [TestCase(LobbyMapKind.Custom)]
    public void PublicRoom_AllowsCurrentRevisionCompletion(LobbyMapKind kind)
    {
        MapCompletionRepository repository = CreateRepository();
        MapCompletionTarget current = CreateTarget(kind, current: true);
        repository.TryRecordCompletion(current, out _);

        RoomCreationEligibility result = EvaluateRoomCreation(
            repository, true, 4, 1, current);

        Assert.That(result.CanCreate, Is.True);
    }

    [Test]
    public void CapacityAndCompletionFailure_PrioritizesCapacityReason()
    {
        MapCompletionRepository repository = CreateRepository();
        MapCompletionTarget target = CreateTarget(LobbyMapKind.Official, current: true);

        RoomCreationEligibility result = EvaluateRoomCreation(
            repository, true, 1, 2, target);

        Assert.That(result.CanCreate, Is.False);
        Assert.That(result.BlockReason,
            Is.EqualTo(RoomCreationBlockReason.InsufficientCapacity));
    }

    [Test]
    public void CompletionSavedEvent_ReevaluatesHostRoomCreationBoundary()
    {
        MapCompletionRepository repository = CreateRepository();
        MapCompletionTarget target = CreateTarget(LobbyMapKind.Official, current: true);
        RoomCreationEligibility latest = EvaluateRoomCreation(
            repository, true, 4, 1, target);
        repository.Changed += () => latest = EvaluateRoomCreation(
            repository, true, 4, 1, target);

        repository.TryRecordCompletion(target, out _);

        Assert.That(latest.CanCreate, Is.True);
    }

    [Test]
    public void DirectPublicLobbyGate_RevalidatesCompletion()
    {
        MapCompletionRepository repository = CreateRepository();
        MapCompletionTarget target = CreateTarget(LobbyMapKind.Custom, current: true);

        RoomCreationEligibility result = EvaluateRoomCreation(
            repository, true, 4, 1, target);

        Assert.That(result.BlockReason,
            Is.EqualTo(RoomCreationBlockReason.CurrentRevisionNotCompleted));
    }

    [Test]
    public void UnsupportedSchema_RejectsPublicRoomButAllowsPrivateRoom()
    {
        File.WriteAllText(GetFilePath(), "{\"schemaVersion\":99,\"records\":[]}");
        MapCompletionRepository repository = CreateRepository();
        MapCompletionTarget target = CreateTarget(LobbyMapKind.Official, current: true);

        Assert.That(EvaluateRoomCreation(repository, true, 4, 1, target).BlockReason,
            Is.EqualTo(RoomCreationBlockReason.CompletionStatusUnavailable));
        Assert.That(EvaluateRoomCreation(repository, false, 4, 1, target).CanCreate,
            Is.True);
    }

    [Test]
    public void FileSystemReadFailure_RejectsPublicRoom()
    {
        var repository = new MapCompletionRepository(
            temporaryDirectory,
            new ReadFailingFileSystem());
        MapCompletionTarget target = CreateTarget(LobbyMapKind.Custom, current: true);

        RoomCreationEligibility result = EvaluateRoomCreation(
            repository, true, 4, 1, target);

        Assert.That(repository.LoadState,
            Is.EqualTo(MapCompletionLoadState.FileSystemFailure));
        Assert.That(result.BlockReason,
            Is.EqualTo(RoomCreationBlockReason.CompletionStatusUnavailable));
    }

    private MapCompletionRepository CreateRepository() =>
        new(temporaryDirectory);

    private string GetFilePath() =>
        Path.Combine(temporaryDirectory, MapCompletionRepository.DefaultFileName);

    private static MapCompletionTarget CreateTarget(LobbyMapKind kind, bool current)
    {
        if (kind == LobbyMapKind.Official)
        {
            MapCompletionTarget.TryCreateOfficial(
                OfficialMapId, current ? 2 : 1, out MapCompletionTarget official);
            return official;
        }

        MapCompletionTarget.TryCreateCustom(
            CustomMapId, current ? SecondHash : FirstHash, out MapCompletionTarget custom);
        return custom;
    }

    private static RoomCreationEligibility EvaluateRoomCreation(
        MapCompletionRepository repository,
        bool isPublicRoom,
        int maximumPlayers,
        int minimumPlayers,
        MapCompletionTarget target)
    {
        LobbyMapMetadata.TryCreate(
            target.MapKey.Kind == LobbyMapKind.Official
                ? RoomMapPolicy.OfficialOnly
                : RoomMapPolicy.CustomOnly,
            target.MapKey.Kind,
            target.MapKey.MapId,
            "테스트 맵",
            "테스트 제작자",
            "1.0",
            minimumPlayers,
            out LobbyMapMetadata metadata);
        return RoomCreationRules.Evaluate(
            isPublicRoom,
            maximumPlayers,
            metadata,
            target,
            repository.GetCurrentRevisionVerification(target));
    }

    private sealed class ReplaceFailingFileSystem : IMapCompletionFileSystem
    {
        private readonly PhysicalMapCompletionFileSystem inner = new();

        public bool FileExists(string path) => inner.FileExists(path);
        public string ReadAllText(string path) => inner.ReadAllText(path);
        public void CreateDirectory(string path) => inner.CreateDirectory(path);
        public void WriteAllTextDurable(string path, string content) =>
            inner.WriteAllTextDurable(path, content);
        public void MoveFile(string sourcePath, string destinationPath) =>
            inner.MoveFile(sourcePath, destinationPath);
        public void ReplaceFile(string sourcePath, string destinationPath, string backupPath) =>
            throw new IOException("의도한 원자적 교체 실패");
        public void DeleteFile(string path) => inner.DeleteFile(path);
    }

    private sealed class ReadFailingFileSystem : IMapCompletionFileSystem
    {
        public bool FileExists(string path) => true;
        public string ReadAllText(string path) => throw new IOException("의도한 읽기 실패");
        public void CreateDirectory(string path) { }
        public void WriteAllTextDurable(string path, string content) { }
        public void MoveFile(string sourcePath, string destinationPath) { }
        public void ReplaceFile(string sourcePath, string destinationPath, string backupPath) { }
        public void DeleteFile(string path) { }
    }
}
