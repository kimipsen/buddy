using buddy.Features.Calendars;
using buddy.Features.Groups;
using buddy.Features.Users;

using Xunit;

namespace buddy.IntegrationTests.EventShapeTests;

public sealed class CalendarEventShapeTests
{
    private static readonly CalendarId FixedCalendarId = new(Guid.Parse("00000000-0000-0000-0000-000000000020"));
    private static readonly CalendarItemId FixedItemId = new(Guid.Parse("00000000-0000-0000-0000-000000000030"));
    private static readonly IcalTokenId FixedTokenId = new(Guid.Parse("00000000-0000-0000-0000-000000000040"));
    private static readonly GroupId FixedGroupId = new(Guid.Parse("00000000-0000-0000-0000-000000000010"));
    private static readonly UserId FixedUserId = new(Guid.Parse("00000000-0000-0000-0000-000000000001"));
    private static readonly UserId OtherUserId = new(Guid.Parse("00000000-0000-0000-0000-000000000002"));
    private static readonly DateTimeOffset FixedInstant = new(2025, 1, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeZoneId FixedTimeZone = TimeZoneId.New("Europe/Copenhagen");

    private static readonly StartsAt FixedStartsAt = new(new DateOnly(2025, 6, 1), new TimeOnly(9, 0));
    private static readonly EndsAt FixedEndsAt = new(new DateOnly(2025, 6, 1), new TimeOnly(9, 30));
    private static readonly DueDate FixedDueDate = new(new DateOnly(2025, 6, 1), new TimeOnly(17, 0));
    private static readonly Period FixedPeriod = Period.TryCreate(FixedStartsAt, FixedEndsAt, out var period) ? period! : throw new InvalidOperationException();

    // -- Calendar-level events (ICalendarEventStore / MartenCalendarEventStore) --

    [Fact]
    public void CalendarCreated() => EventShapeTestSupport.AssertMatchesGoldenFile(
        new CalendarCreated(FixedCalendarId, FixedUserId, "Personal", FixedTimeZone, FixedInstant),
        "Calendars/CalendarCreated.json");

    [Fact]
    public void CalendarCreatedForGroup() => EventShapeTestSupport.AssertMatchesGoldenFile(
        new CalendarCreatedForGroup(FixedCalendarId, FixedGroupId, "Team Calendar", FixedTimeZone, FixedInstant),
        "Calendars/CalendarCreatedForGroup.json");

    [Fact]
    public void CalendarDeleted() => EventShapeTestSupport.AssertMatchesGoldenFile(
        new CalendarDeleted(FixedCalendarId, FixedUserId, FixedInstant),
        "Calendars/CalendarDeleted.json");

    [Fact]
    public void MemberRoleGranted() => EventShapeTestSupport.AssertMatchesGoldenFile(
        new MemberRoleGranted(FixedCalendarId, OtherUserId, CalendarRole.Contributor, FixedUserId, FixedInstant),
        "Calendars/MemberRoleGranted.json");

    [Fact]
    public void MemberRoleRevoked() => EventShapeTestSupport.AssertMatchesGoldenFile(
        new MemberRoleRevoked(FixedCalendarId, OtherUserId, FixedUserId, FixedInstant),
        "Calendars/MemberRoleRevoked.json");

    [Fact]
    public void IcalTokenIssued() => EventShapeTestSupport.AssertMatchesGoldenFile(
        new IcalTokenIssued(FixedCalendarId, FixedTokenId, "sha256-hash-of-token", FixedUserId, FixedInstant),
        "Calendars/IcalTokenIssued.json");

    [Fact]
    public void IcalTokenRevoked() => EventShapeTestSupport.AssertMatchesGoldenFile(
        new IcalTokenRevoked(FixedCalendarId, FixedTokenId, FixedUserId, FixedInstant),
        "Calendars/IcalTokenRevoked.json");

    // -- Item-level events (ICalendarItemEventStore / MartenCalendarItemEventStore) --

    [Fact]
    public void EventItemCreated() => EventShapeTestSupport.AssertMatchesGoldenFile(
        new EventItemCreated(FixedItemId, FixedCalendarId, FixedUserId, "Standup", Icon.New("calendar"), Color.New("#00ff00"), FixedPeriod, null, FixedInstant),
        "Calendars/EventItemCreated.json");

    [Fact]
    public void TaskItemCreated() => EventShapeTestSupport.AssertMatchesGoldenFile(
        new TaskItemCreated(FixedItemId, FixedCalendarId, FixedUserId, "File taxes", Icon.New("task"), Color.New("#ff0000"), FixedDueDate, null, FixedInstant),
        "Calendars/TaskItemCreated.json");

    [Fact]
    public void ItemDetailsUpdated() => EventShapeTestSupport.AssertMatchesGoldenFile(
        new ItemDetailsUpdated(
            FixedItemId,
            new ItemDetails("Standup", Icon.New("calendar"), Color.New("#00ff00")),
            new ItemDetails("Renamed", Icon.New("star"), Color.New("#123456")),
            FixedUserId,
            FixedInstant),
        "Calendars/ItemDetailsUpdated.json");

    [Fact]
    public void EventRescheduled() => EventShapeTestSupport.AssertMatchesGoldenFile(
        new EventRescheduled(FixedItemId, FixedPeriod, FixedPeriod, FixedUserId, FixedInstant),
        "Calendars/EventRescheduled.json");

    [Fact]
    public void TaskRescheduled() => EventShapeTestSupport.AssertMatchesGoldenFile(
        new TaskRescheduled(FixedItemId, FixedDueDate, FixedDueDate, FixedUserId, FixedInstant),
        "Calendars/TaskRescheduled.json");

    [Fact]
    public void RecurrenceUpdated() => EventShapeTestSupport.AssertMatchesGoldenFile(
        new RecurrenceUpdated(FixedItemId, null, new RecurrenceRule(RecurrenceFrequency.Weekly, 1, null), FixedUserId, FixedInstant),
        "Calendars/RecurrenceUpdated.json");

    [Fact]
    public void ItemDeleted() => EventShapeTestSupport.AssertMatchesGoldenFile(
        new ItemDeleted(FixedItemId, FixedUserId, FixedInstant),
        "Calendars/ItemDeleted.json");
}
