using System.Collections.Generic;
using NUnit.Framework;

public class MapSelectionCoreTests
{
    private const string CustomMapId = "0123456789abcdef0123456789abcdef";

    [Test]
    public void OfficialMapId_NormalizesCanonicalValue()
    {
        bool result = OfficialMapId.TryNormalize(
            " Official.Stage.01 ", out string normalizedMapId);

        Assert.That(result, Is.True);
        Assert.That(normalizedMapId, Is.EqualTo("official.stage.01"));
    }

    [TestCase("stage.01")]
    [TestCase("official..01")]
    [TestCase("official.stage_01")]
    [TestCase("official.-stage")]
    [TestCase("official.스테이지")]
    public void OfficialMapId_RejectsInvalidValue(string mapId)
    {
        Assert.That(OfficialMapId.TryNormalize(mapId, out _), Is.False);
    }

    [Test]
    public void MapKey_UsesKindAsPartOfIdentity()
    {
        MapKey.TryCreate(LobbyMapKind.Official, "official.stage.01", out MapKey official);
        MapKey.TryCreate(LobbyMapKind.Custom, CustomMapId, out MapKey custom);

        Assert.That(official, Is.Not.EqualTo(custom));
        Assert.That(official.ToString(), Is.EqualTo("Official:official.stage.01"));
    }

    [Test]
    public void MapKey_NormalizesCustomGuid()
    {
        bool result = MapKey.TryCreate(LobbyMapKind.Custom, CustomMapId, out MapKey mapKey);

        Assert.That(result, Is.True);
        Assert.That(mapKey.MapId, Is.EqualTo(CustomMapId));
    }

    [Test]
    public void RoomPolicy_AllowsOnlyConfiguredMapKind()
    {
        Assert.That(RoomMapPolicy.OfficialOnly.Allows(LobbyMapKind.Official), Is.True);
        Assert.That(RoomMapPolicy.OfficialOnly.Allows(LobbyMapKind.Custom), Is.False);
        Assert.That(RoomMapPolicy.CustomOnly.Allows(LobbyMapKind.Custom), Is.True);
        Assert.That(RoomMapPolicy.CustomOnly.Allows(LobbyMapKind.None), Is.False);
    }

    [Test]
    public void CatalogRules_RejectDuplicateMapIds()
    {
        var descriptors = new List<OfficialMapDescriptor>
        {
            CreateDescriptor("official.stage.01"),
            CreateDescriptor("official.stage.01")
        };

        IReadOnlyList<string> errors = OfficialMapCatalogRules.Validate(descriptors);

        Assert.That(errors, Has.Some.Contains("중복된 공식맵 ID"));
    }

    [Test]
    public void CatalogRules_AcceptValidDescriptors()
    {
        var descriptors = new List<OfficialMapDescriptor>
        {
            CreateDescriptor("official.stage.01"),
            CreateDescriptor("official.stage.02")
        };

        Assert.That(OfficialMapCatalogRules.Validate(descriptors), Is.Empty);
    }

    [TestCase(0)]
    [TestCase(5)]
    public void MapPlayRequirements_RejectsOutOfRangeMinimum(int minimumPlayers)
    {
        Assert.That(
            MapPlayRequirements.TryCreate(minimumPlayers, out _),
            Is.False);
    }

    [TestCase(1)]
    [TestCase(4)]
    public void MapPlayRequirements_AcceptsSupportedMinimum(int minimumPlayers)
    {
        Assert.That(
            MapPlayRequirements.TryCreate(minimumPlayers, out var requirements),
            Is.True);
        Assert.That(requirements.MinimumPlayersToClear, Is.EqualTo(minimumPlayers));
    }

    [Test]
    public void CatalogRules_RejectsInvalidRequirements()
    {
        var descriptors = new List<OfficialMapDescriptor>
        {
            new("official.stage.01", "공식 스테이지", "Flip Friends", "1.0", 0, 0)
        };

        IReadOnlyList<string> errors = OfficialMapCatalogRules.Validate(descriptors);

        Assert.That(errors, Has.Some.Contains("최소 클리어 인원"));
        Assert.That(errors, Has.Some.Contains("완료 리비전"));
    }

    [TestCase(4, 4, true)]
    [TestCase(2, 3, false)]
    [TestCase(0, 1, false)]
    [TestCase(4, 5, false)]
    public void RoomCreationRules_ChecksCapacityAgainstMapMinimum(
        int maximumPlayers,
        int minimumPlayersToClear,
        bool expected)
    {
        Assert.That(
            RoomCreationRules.HasEnoughCapacity(maximumPlayers, minimumPlayersToClear),
            Is.EqualTo(expected));
    }

    [Test]
    public void LobbyMapMetadata_RejectsPolicyAndMapKindMismatch()
    {
        bool result = LobbyMapMetadata.TryCreate(
            RoomMapPolicy.OfficialOnly,
            LobbyMapKind.Custom,
            CustomMapId,
            "커스텀맵",
            "제작자",
            "1.0",
            1,
            out _);

        Assert.That(result, Is.False);
    }

    [TestCase(0)]
    [TestCase(5)]
    public void LobbyMapMetadata_RejectsInvalidMinimumPlayers(int minimumPlayers)
    {
        bool result = LobbyMapMetadata.TryCreate(
            RoomMapPolicy.OfficialOnly,
            LobbyMapKind.Official,
            "official.stage.01",
            "공식맵",
            "Flip Friends",
            "1.0",
            minimumPlayers,
            out _);

        Assert.That(result, Is.False);
    }

    [Test]
    public void LobbyMapFilter_FiltersByKindAndSupportedPartySize()
    {
        LobbyMapMetadata.TryCreate(
            RoomMapPolicy.CustomOnly,
            LobbyMapKind.Custom,
            CustomMapId,
            "2인 커스텀맵",
            "제작자",
            "1.0",
            2,
            out LobbyMapMetadata metadata);

        Assert.That(new LobbyMapFilter(LobbyMapKind.Custom, 2).Matches(metadata), Is.True);
        Assert.That(new LobbyMapFilter(LobbyMapKind.Official, 2).Matches(metadata), Is.False);
        Assert.That(new LobbyMapFilter(LobbyMapKind.Custom, 1).Matches(metadata), Is.False);
        Assert.That(LobbyMapFilter.All.Matches(metadata), Is.True);
    }

    [TestCase(1, false)]
    [TestCase(2, true)]
    [TestCase(4, true)]
    public void RoomMapSessionRules_RequiresConfiguredKeyMinimumAndReadyPlayers(
        int readyPlayerCount,
        bool expected)
    {
        LobbyMapMetadata.TryCreate(
            RoomMapPolicy.OfficialOnly,
            LobbyMapKind.Official,
            "official.stage.01",
            "공식맵",
            "Flip Friends",
            "1.0",
            2,
            out LobbyMapMetadata metadata);

        bool result = RoomMapSessionRules.CanStart(
            metadata,
            LobbyMapKind.Official,
            "official.stage.01",
            2,
            readyPlayerCount);

        Assert.That(result, Is.EqualTo(expected));
    }

    [Test]
    public void RoomMapSessionRules_RejectsDifferentMapKeyOrMinimum()
    {
        LobbyMapMetadata.TryCreate(
            RoomMapPolicy.OfficialOnly,
            LobbyMapKind.Official,
            "official.stage.01",
            "공식맵",
            "Flip Friends",
            "1.0",
            2,
            out LobbyMapMetadata metadata);

        Assert.That(
            RoomMapSessionRules.IsConfiguredSelectionValid(
                metadata, LobbyMapKind.Official, "official.stage.02", 2),
            Is.False);
        Assert.That(
            RoomMapSessionRules.IsConfiguredSelectionValid(
                metadata, LobbyMapKind.Official, "official.stage.01", 1),
            Is.False);
    }

    [Test]
    public void RoomMapSessionRules_AllowsTransferOnlyForCustomOnlyRoom()
    {
        LobbyMapMetadata.TryCreate(
            RoomMapPolicy.OfficialOnly,
            LobbyMapKind.Official,
            "official.stage.01",
            "공식맵",
            "Flip Friends",
            "1.0",
            1,
            out LobbyMapMetadata official);
        LobbyMapMetadata.TryCreate(
            RoomMapPolicy.CustomOnly,
            LobbyMapKind.Custom,
            CustomMapId,
            "커스텀맵",
            "제작자",
            "1.0",
            1,
            out LobbyMapMetadata custom);

        Assert.That(RoomMapSessionRules.AllowsCustomTransfer(official), Is.False);
        Assert.That(RoomMapSessionRules.AllowsCustomTransfer(custom), Is.True);
    }

    private static OfficialMapDescriptor CreateDescriptor(string mapId) =>
        new(mapId, "공식 스테이지", "Flip Friends", "1.0", 1, 1);
}
