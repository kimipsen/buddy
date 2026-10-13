using buddy.Features.Calendars;
using buddy.Features.TransitionWarnings;
using buddy.Features.Users;

using Xunit;

namespace buddy.IntegrationTests.EventShapeTests;

public sealed class TransitionWarningEventShapeTests
{
    private static readonly UserId FixedChildId = new(Guid.Parse("00000000-0000-0000-0000-000000000003"));
    private static readonly UserId FixedGuardianId = new(Guid.Parse("00000000-0000-0000-0000-000000000001"));
    private static readonly TransitionWarningSettingsId FixedId = TransitionWarningSettingsId.ForChild(FixedChildId);
    private static readonly CalendarItemId FixedItemId = new(Guid.Parse("00000000-0000-0000-0000-0000000000c1"));
    private static readonly DateTimeOffset FixedInstant = new(2025, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private static readonly WarningDefaults Enabled = WarningDefaults.Create(
        true, [10, 2], [WarningKind.Event, WarningKind.DropOff], new QuietHours(new TimeOnly(21, 0), new TimeOnly(7, 0)));

    [Fact]
    public void TransitionWarningsStarted() => EventShapeTestSupport.AssertMatchesGoldenFile(
        new TransitionWarningsStarted(FixedId, FixedChildId, FixedInstant),
        "TransitionWarnings/TransitionWarningsStarted.json");

    [Fact]
    public void TransitionWarningDefaultsConfigured() => EventShapeTestSupport.AssertMatchesGoldenFile(
        new TransitionWarningDefaultsConfigured(FixedId, WarningDefaults.Initial, Enabled, FixedGuardianId, FixedInstant),
        "TransitionWarnings/TransitionWarningDefaultsConfigured.json");

    [Fact]
    public void ItemWarningOverrideSet() => EventShapeTestSupport.AssertMatchesGoldenFile(
        new ItemWarningOverrideSet(FixedId, FixedItemId, null, new ItemWarningOverride.LeadTimes([30, 10]), FixedGuardianId, FixedInstant),
        "TransitionWarnings/ItemWarningOverrideSet.json");

    [Fact]
    public void ItemWarningOverrideSet_to_off() => EventShapeTestSupport.AssertMatchesGoldenFile(
        new ItemWarningOverrideSet(FixedId, FixedItemId, new ItemWarningOverride.LeadTimes([30, 10]), new ItemWarningOverride.Off(), FixedGuardianId, FixedInstant),
        "TransitionWarnings/ItemWarningOverrideSet_Off.json");

    [Fact]
    public void ItemWarningOverrideCleared() => EventShapeTestSupport.AssertMatchesGoldenFile(
        new ItemWarningOverrideCleared(FixedId, FixedItemId, new ItemWarningOverride.Off(), FixedGuardianId, FixedInstant),
        "TransitionWarnings/ItemWarningOverrideCleared.json");

    // ItemWarningOverride goes through a hand-written kind converter: both cases read back.
    [Fact]
    public void An_item_override_reads_back_from_its_golden_file() => EventShapeTestSupport.AssertGoldenFileReadsBackAs(
        new ItemWarningOverrideSet(FixedId, FixedItemId, new ItemWarningOverride.LeadTimes([30, 10]), new ItemWarningOverride.Off(), FixedGuardianId, FixedInstant),
        "TransitionWarnings/ItemWarningOverrideSet_Off.json");

    [Fact]
    public void The_defaults_read_back_from_their_golden_file() => EventShapeTestSupport.AssertGoldenFileReadsBackAs(
        new TransitionWarningDefaultsConfigured(FixedId, WarningDefaults.Initial, Enabled, FixedGuardianId, FixedInstant),
        "TransitionWarnings/TransitionWarningDefaultsConfigured.json");
}
