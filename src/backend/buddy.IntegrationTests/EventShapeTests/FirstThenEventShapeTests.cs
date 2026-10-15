using buddy.Features.Calendars;
using buddy.Features.FirstThen;
using buddy.Features.Users;

using Xunit;

namespace buddy.IntegrationTests.EventShapeTests;

public sealed class FirstThenEventShapeTests
{
    private static readonly UserId FixedChildId = new(Guid.Parse("00000000-0000-0000-0000-000000000003"));
    private static readonly FirstThenBoardId FixedBoardId = FirstThenBoardId.ForChild(FixedChildId);
    private static readonly UserId FixedGuardianId = new(Guid.Parse("00000000-0000-0000-0000-000000000001"));
    private static readonly DateTimeOffset FixedInstant = new(2025, 1, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void FirstThenBoardStarted() => EventShapeTestSupport.AssertMatchesGoldenFile(
        new FirstThenBoardStarted(FixedBoardId, FixedChildId, FixedInstant),
        "FirstThen/FirstThenBoardStarted.json");

    [Fact]
    public void FirstThenBoardSet() => EventShapeTestSupport.AssertMatchesGoldenFile(
        new FirstThenBoardSet(FixedBoardId, 1, new FirstThenCard("Homework", new Icon("📚")), new FirstThenCard("iPad", new Icon("📱")), 20, FixedGuardianId, FixedInstant),
        "FirstThen/FirstThenBoardSet.json");

    [Fact]
    public void FirstThenBoardSet_without_a_length() => EventShapeTestSupport.AssertMatchesGoldenFile(
        new FirstThenBoardSet(FixedBoardId, 2, new FirstThenCard("Shoes on", new Icon("👟")), new FirstThenCard("Play outside", new Icon("⚽")), null, FixedGuardianId, FixedInstant),
        "FirstThen/FirstThenBoardSet_NoLength.json");

    [Fact]
    public void FirstThenFirstCompleted() => EventShapeTestSupport.AssertMatchesGoldenFile(
        new FirstThenFirstCompleted(FixedBoardId, 1, FixedChildId, FixedInstant),
        "FirstThen/FirstThenFirstCompleted.json");

    [Fact]
    public void FirstThenFirstReopened() => EventShapeTestSupport.AssertMatchesGoldenFile(
        new FirstThenFirstReopened(FixedBoardId, 1, FixedGuardianId, FixedInstant),
        "FirstThen/FirstThenFirstReopened.json");

    [Fact]
    public void FirstThenBoardEnded() => EventShapeTestSupport.AssertMatchesGoldenFile(
        new FirstThenBoardEnded(FixedBoardId, 1, FixedChildId, FixedInstant),
        "FirstThen/FirstThenBoardEnded.json");
}
