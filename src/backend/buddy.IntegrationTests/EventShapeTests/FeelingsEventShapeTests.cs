using buddy.Features.Feelings;
using buddy.Features.Users;

using Xunit;

namespace buddy.IntegrationTests.EventShapeTests;

public sealed class FeelingsEventShapeTests
{
    private static readonly UserId FixedChildId = new(Guid.Parse("00000000-0000-0000-0000-000000000003"));
    private static readonly FeelingsJournalId FixedJournalId = FeelingsJournalId.ForChild(FixedChildId);
    private static readonly FeelingCheckInId FixedCheckInId = new(Guid.Parse("00000000-0000-0000-0000-0000000000c1"));
    private static readonly FeelingsShareTokenId FixedTokenId = new(Guid.Parse("00000000-0000-0000-0000-0000000000a0"));
    private static readonly UserId FixedGuardianId = new(Guid.Parse("00000000-0000-0000-0000-000000000001"));
    private static readonly DateTimeOffset FixedInstant = new(2025, 1, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly FixedDate = new(2025, 6, 1);

    private static readonly FeelingCheckIn SelfReport = new(
        FixedCheckInId, FeelingZone.Yellow, [FeelingTag.Worried, FeelingTag.School], "Maths test", FixedDate, new TimeOnly(15, 30), FixedChildId, FixedInstant);

    private static readonly FeelingCheckIn Observation = SelfReport with
    {
        Zone = FeelingZone.Red,
        Tags = [FeelingTag.Overwhelmed],
        Note = "",
        LoggedBy = FixedGuardianId
    };

    [Fact]
    public void FeelingsJournalStarted() => EventShapeTestSupport.AssertMatchesGoldenFile(
        new FeelingsJournalStarted(FixedJournalId, FixedChildId, FixedInstant),
        "Feelings/FeelingsJournalStarted.json");

    [Fact]
    public void FeelingCheckedIn_self_report() => EventShapeTestSupport.AssertMatchesGoldenFile(
        new FeelingCheckedIn(FixedJournalId, SelfReport, FixedInstant),
        "Feelings/FeelingCheckedIn.json");

    [Fact]
    public void FeelingCheckInEdited() => EventShapeTestSupport.AssertMatchesGoldenFile(
        new FeelingCheckInEdited(FixedJournalId, Observation, Observation with { Zone = FeelingZone.Yellow, Tags = [FeelingTag.Frustrated] }, FixedGuardianId, FixedInstant),
        "Feelings/FeelingCheckInEdited.json");

    [Fact]
    public void FeelingCheckInRemoved() => EventShapeTestSupport.AssertMatchesGoldenFile(
        new FeelingCheckInRemoved(FixedJournalId, SelfReport, FixedChildId, FixedInstant),
        "Feelings/FeelingCheckInRemoved.json");

    [Fact]
    public void CheckInPromptTimesSet() => EventShapeTestSupport.AssertMatchesGoldenFile(
        new CheckInPromptTimesSet(FixedJournalId, [], [new TimeOnly(7, 30), new TimeOnly(15, 0)], FixedGuardianId, FixedInstant),
        "Feelings/CheckInPromptTimesSet.json");

    [Fact]
    public void FeelingsDisplayModeSet() => EventShapeTestSupport.AssertMatchesGoldenFile(
        new FeelingsDisplayModeSet(FixedJournalId, FeelingsDisplayMode.FacesAndWords, FeelingsDisplayMode.FacesOnly, FixedGuardianId, FixedInstant),
        "Feelings/FeelingsDisplayModeSet.json");

    [Fact]
    public void FeelingsShareTokenCreated() => EventShapeTestSupport.AssertMatchesGoldenFile(
        new FeelingsShareTokenCreated(FixedTokenId, FixedChildId, "ABC123", FixedGuardianId, FixedInstant.AddDays(30), FixedInstant),
        "Feelings/FeelingsShareTokenCreated.json");

    [Fact]
    public void FeelingsShareTokenRevoked() => EventShapeTestSupport.AssertMatchesGoldenFile(
        new FeelingsShareTokenRevoked(FixedTokenId, FixedGuardianId, FixedInstant),
        "Feelings/FeelingsShareTokenRevoked.json");
}
