using buddy.Features.Babysitters;
using buddy.Features.Users;

using Xunit;

namespace buddy.IntegrationTests.EventShapeTests;

public sealed class BabysitterEventShapeTests
{
    private static readonly BabysitterListId FixedListId = new(Guid.Parse("00000000-0000-0000-0000-000000000001"));
    private static readonly UserId FixedGuardianId = new(Guid.Parse("00000000-0000-0000-0000-000000000001"));
    private static readonly BabysitterId FixedBabysitterId = new(Guid.Parse("00000000-0000-0000-0000-000000000090"));
    private static readonly DateTimeOffset FixedInstant = new(2025, 1, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void BabysitterListStarted() => EventShapeTestSupport.AssertMatchesGoldenFile(
        new BabysitterListStarted(FixedListId, FixedGuardianId, FixedInstant),
        "Babysitters/BabysitterListStarted.json");

    [Fact]
    public void BabysitterAdded() => EventShapeTestSupport.AssertMatchesGoldenFile(
        new BabysitterAdded(FixedListId, FixedBabysitterId, "Anna", "+45 12 34 56 78", FixedInstant),
        "Babysitters/BabysitterAdded.json");

    [Fact]
    public void BabysitterDetailsChanged() => EventShapeTestSupport.AssertMatchesGoldenFile(
        new BabysitterDetailsChanged(
            FixedListId, FixedBabysitterId,
            new BabysitterDetails("Anna", "+45 12 34 56 78"),
            new BabysitterDetails("Anne", ""),
            FixedInstant),
        "Babysitters/BabysitterDetailsChanged.json");

    [Fact]
    public void BabysitterArchived() => EventShapeTestSupport.AssertMatchesGoldenFile(
        new BabysitterArchived(FixedListId, FixedBabysitterId, FixedInstant),
        "Babysitters/BabysitterArchived.json");
}
