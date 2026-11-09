using buddy.Features.Babysitters;
using buddy.Features.NightDuty;
using buddy.Features.Users;

using Xunit;

namespace buddy.IntegrationTests.EventShapeTests;

public sealed class NightDutyEventShapeTests
{
    private static readonly NightScheduleId FixedScheduleId = new(Guid.Parse("00000000-0000-0000-0000-000000000002"));
    private static readonly UserId FixedChildId = new(Guid.Parse("00000000-0000-0000-0000-000000000002"));
    private static readonly UserId FixedGuardianId = new(Guid.Parse("00000000-0000-0000-0000-000000000001"));
    private static readonly BabysitterId FixedBabysitterId = new(Guid.Parse("00000000-0000-0000-0000-000000000090"));
    private static readonly DateTimeOffset FixedInstant = new(2025, 1, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly FixedDate = new(2025, 6, 2);
    private static readonly DateOnly FixedMonday = new(2024, 12, 30);

    private static readonly NightEntry GuardianEntry = new(new NightResponsible.Guardian(FixedGuardianId), "");

    [Fact]
    public void NightScheduleStarted() => EventShapeTestSupport.AssertMatchesGoldenFile(
        new NightScheduleStarted(FixedScheduleId, FixedChildId, FixedInstant),
        "NightDuty/NightScheduleStarted.json");

    // Every NightResponsible case appears once, so this file pins all four persisted shapes.
    [Fact]
    public void NightPatternReplaced() => EventShapeTestSupport.AssertMatchesGoldenFile(
        new NightPatternReplaced(
            FixedScheduleId,
            new NightPattern(FixedDate, 2, FixedMonday,
            [
                new NightPatternDay(0, DayOfWeek.Monday, GuardianEntry),
                new NightPatternDay(0, DayOfWeek.Tuesday, new NightEntry(new NightResponsible.Babysitter(FixedGuardianId, FixedBabysitterId), "")),
                new NightPatternDay(0, DayOfWeek.Friday, new NightEntry(new NightResponsible.Unassigned(), "Screen night 📱")),
                new NightPatternDay(1, DayOfWeek.Saturday, new NightEntry(new NightResponsible.Sleepover("Grandma Lis", "Skovvej 12", "20 12 34 56"), ""))
            ]),
            FixedGuardianId,
            FixedInstant),
        "NightDuty/NightPatternReplaced.json");

    [Fact]
    public void NightPatternReplaced_ReadsBack() => Assert.True(
        EventShapeTestSupport.ReadGoldenFile<NightPatternReplaced>("NightDuty/NightPatternReplaced.json").Pattern.IsSameAs(
            new NightPattern(FixedDate, 2, FixedMonday,
            [
                new NightPatternDay(0, DayOfWeek.Monday, GuardianEntry),
                new NightPatternDay(0, DayOfWeek.Tuesday, new NightEntry(new NightResponsible.Babysitter(FixedGuardianId, FixedBabysitterId), "")),
                new NightPatternDay(0, DayOfWeek.Friday, new NightEntry(new NightResponsible.Unassigned(), "Screen night 📱")),
                new NightPatternDay(1, DayOfWeek.Saturday, new NightEntry(new NightResponsible.Sleepover("Grandma Lis", "Skovvej 12", "20 12 34 56"), ""))
            ])));

    [Fact]
    public void NightOverridden_planned() => EventShapeTestSupport.AssertMatchesGoldenFile(
        new NightOverridden(FixedScheduleId, FixedDate, new NightOverride.Planned(GuardianEntry with { Note = "Screen night" }), FixedGuardianId, FixedInstant),
        "NightDuty/NightOverridden_Planned.json");

    [Fact]
    public void NightOverridden_planned_ReadsBack() => EventShapeTestSupport.AssertGoldenFileReadsBackAs(
        new NightOverridden(FixedScheduleId, FixedDate, new NightOverride.Planned(GuardianEntry with { Note = "Screen night" }), FixedGuardianId, FixedInstant),
        "NightDuty/NightOverridden_Planned.json");

    [Fact]
    public void NightOverridden_cleared() => EventShapeTestSupport.AssertMatchesGoldenFile(
        new NightOverridden(FixedScheduleId, FixedDate, new NightOverride.Cleared(), FixedGuardianId, FixedInstant),
        "NightDuty/NightOverridden_Cleared.json");

    [Fact]
    public void NightOverridden_cleared_ReadsBack() => EventShapeTestSupport.AssertGoldenFileReadsBackAs(
        new NightOverridden(FixedScheduleId, FixedDate, new NightOverride.Cleared(), FixedGuardianId, FixedInstant),
        "NightDuty/NightOverridden_Cleared.json");

    [Fact]
    public void NightOverrideCleared() => EventShapeTestSupport.AssertMatchesGoldenFile(
        new NightOverrideCleared(FixedScheduleId, FixedDate, new NightOverride.Planned(GuardianEntry), FixedGuardianId, FixedInstant),
        "NightDuty/NightOverrideCleared.json");
}
