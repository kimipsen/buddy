using buddy.Features.Calendars;
using buddy.Features.Users;
using buddy.Features.WorkLocations;

using Xunit;

namespace buddy.IntegrationTests.EventShapeTests;

public sealed class WorkLocationEventShapeTests
{
    private static readonly WorkLocationScheduleId FixedScheduleId = new(Guid.Parse("00000000-0000-0000-0000-000000000001"));
    private static readonly UserId FixedGuardianId = new(Guid.Parse("00000000-0000-0000-0000-000000000001"));
    private static readonly WorkLocationId FixedLocationId = new(Guid.Parse("00000000-0000-0000-0000-000000000080"));
    private static readonly WorkLocationId OtherLocationId = new(Guid.Parse("00000000-0000-0000-0000-000000000081"));
    private static readonly DateTimeOffset FixedInstant = new(2025, 1, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly FixedDate = new(2025, 6, 2);
    private static readonly DateOnly FixedMonday = new(2024, 12, 30);

    [Fact]
    public void WorkLocationScheduleStarted() => EventShapeTestSupport.AssertMatchesGoldenFile(
        new WorkLocationScheduleStarted(FixedScheduleId, FixedGuardianId, FixedInstant),
        "WorkLocations/WorkLocationScheduleStarted.json");

    [Fact]
    public void WorkLocationAdded() => EventShapeTestSupport.AssertMatchesGoldenFile(
        new WorkLocationAdded(FixedScheduleId, FixedLocationId, "Stil", new Icon("🏢"), new Color("#2563eb"), FixedInstant),
        "WorkLocations/WorkLocationAdded.json");

    [Fact]
    public void WorkLocationDetailsChanged() => EventShapeTestSupport.AssertMatchesGoldenFile(
        new WorkLocationDetailsChanged(
            FixedScheduleId, FixedLocationId,
            new WorkLocationDetails("Stil", new Icon("🏢"), new Color("#2563eb")),
            new WorkLocationDetails("Kontoret", new Icon("🏬"), new Color("#dc2626")),
            FixedInstant),
        "WorkLocations/WorkLocationDetailsChanged.json");

    [Fact]
    public void WorkLocationArchived() => EventShapeTestSupport.AssertMatchesGoldenFile(
        new WorkLocationArchived(FixedScheduleId, FixedLocationId, FixedInstant),
        "WorkLocations/WorkLocationArchived.json");

    [Fact]
    public void WorkPatternReplaced() => EventShapeTestSupport.AssertMatchesGoldenFile(
        new WorkPatternReplaced(
            FixedScheduleId,
            new WorkPattern(1, FixedMonday, []),
            new WorkPattern(2, FixedMonday,
            [
                new WorkPatternDay(0, DayOfWeek.Tuesday, FixedLocationId),
                new WorkPatternDay(1, DayOfWeek.Thursday, OtherLocationId)
            ]),
            FixedInstant),
        "WorkLocations/WorkPatternReplaced.json");

    [Fact]
    public void WorkLocationOverridden_to_a_location() => EventShapeTestSupport.AssertMatchesGoldenFile(
        new WorkLocationOverridden(FixedScheduleId, FixedDate, Before: null, new WorkDayOverride(FixedLocationId), FixedInstant),
        "WorkLocations/WorkLocationOverridden_Location.json");

    [Fact]
    public void WorkLocationOverridden_to_off() => EventShapeTestSupport.AssertMatchesGoldenFile(
        new WorkLocationOverridden(FixedScheduleId, FixedDate, new WorkDayOverride(FixedLocationId), new WorkDayOverride(null), FixedInstant),
        "WorkLocations/WorkLocationOverridden_Off.json");

    [Fact]
    public void WorkLocationOverrideCleared() => EventShapeTestSupport.AssertMatchesGoldenFile(
        new WorkLocationOverrideCleared(FixedScheduleId, FixedDate, new WorkDayOverride(FixedLocationId), FixedInstant),
        "WorkLocations/WorkLocationOverrideCleared.json");
}
