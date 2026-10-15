using Alba;

using buddy.Common;
using buddy.Features.Calendars;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Microsoft.Extensions.DependencyInjection;

using Xunit;

namespace buddy.IntegrationTests.Features.Calendars.UpdateItemAlarms;

[Collection(BuddyApiCollection.Name)]
public sealed class UpdateItemAlarmsTests(BuddyApiFixture fixture)
{
    [Fact]
    [CoversEndpoint("UpdateCalendarItemAlarms")]
    public async Task A_contributor_can_set_alarms_on_an_item_and_they_come_back_sorted()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var calendarId = await CalendarTestHelpers.CreateCalendarAsync(fixture, token, "Personal");
        var day = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1);
        var item = await CalendarTestHelpers.CreateEventAsync(fixture, token, calendarId, "Dentist", day);
        Assert.NotNull(item);
        Assert.Equal([], item.Alarms);

        var response = await PatchAsync(token, calendarId, item.Id, new[] { -1440, -120, -120 });

        Assert.Equal([-1440, -120], response.ReadAsJson<CalendarItemDto>().Alarms);

        var occurrences = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Get.Url($"/calendars/{calendarId}/occurrences?from={day:yyyy-MM-dd}&to={day:yyyy-MM-dd}");
            _.StatusCodeShouldBeOk();
        });

        Assert.Equal([-1440, -120], Assert.Single(occurrences.ReadAsJson<List<CalendarItemOccurrenceDto>>()).Alarms);
    }

    [Fact]
    public async Task An_empty_list_clears_every_alarm()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var calendarId = await CalendarTestHelpers.CreateCalendarAsync(fixture, token, "Personal");
        var item = await CalendarTestHelpers.CreateEventAsync(fixture, token, calendarId, alarms: [-60]);
        Assert.NotNull(item);
        Assert.Equal([-60], item.Alarms);

        var response = await PatchAsync(token, calendarId, item.Id, Array.Empty<int>());

        Assert.Equal([], response.ReadAsJson<CalendarItemDto>().Alarms);
    }

    [Fact]
    public async Task Re_sending_the_same_alarms_appends_nothing()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var calendarId = await CalendarTestHelpers.CreateCalendarAsync(fixture, token, "Personal");
        var item = await CalendarTestHelpers.CreateEventAsync(fixture, token, calendarId);
        Assert.NotNull(item);
        var items = fixture.Host.Services.GetRequiredService<ICalendarItemEventStore>();
        var id = new CalendarItemId(item.Id);

        await PatchAsync(token, calendarId, item.Id, new[] { -15, -60 });
        var afterFirst = (await items.ReadAsync(id, CancellationToken.None)).Count;
        // Same set, different order and a duplicate: still the same alarms.
        var second = await PatchAsync(token, calendarId, item.Id, new[] { -60, -15, -15 });

        Assert.Equal(afterFirst, (await items.ReadAsync(id, CancellationToken.None)).Count);
        Assert.Equal([-60, -15], second.ReadAsJson<CalendarItemDto>().Alarms);
    }

    [Fact]
    public async Task Clearing_alarms_on_an_item_without_any_appends_nothing()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var calendarId = await CalendarTestHelpers.CreateCalendarAsync(fixture, token, "Personal");
        var item = await CalendarTestHelpers.CreateTaskAsync(fixture, token, calendarId);
        Assert.NotNull(item);
        var items = fixture.Host.Services.GetRequiredService<ICalendarItemEventStore>();
        var id = new CalendarItemId(item.Id);
        var before = (await items.ReadAsync(id, CancellationToken.None)).Count;

        await PatchAsync(token, calendarId, item.Id, Array.Empty<int>());

        Assert.Equal(before, (await items.ReadAsync(id, CancellationToken.None)).Count);
    }

    [Fact]
    public async Task More_than_three_alarms_are_rejected()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var calendarId = await CalendarTestHelpers.CreateCalendarAsync(fixture, token, "Personal");
        var item = await CalendarTestHelpers.CreateEventAsync(fixture, token, calendarId);
        Assert.NotNull(item);

        var response = await PatchAsync(token, calendarId, item.Id, new[] { 0, -5, -15, -30 }, expectedStatus: 400);

        var error = response.ReadAsJson<ErrorEnvelope>();
        Assert.Equal("validation_error", error.Code);
        Assert.Contains(ItemAlarmRules.Key, error.Details.Keys);
    }

    [Theory]
    [InlineData(ItemAlarms.MinOffset - 1)]
    [InlineData(ItemAlarms.MaxOffset + 1)]
    public async Task An_offset_out_of_range_is_rejected(int offset)
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var calendarId = await CalendarTestHelpers.CreateCalendarAsync(fixture, token, "Personal");
        var item = await CalendarTestHelpers.CreateEventAsync(fixture, token, calendarId);
        Assert.NotNull(item);

        var response = await PatchAsync(token, calendarId, item.Id, new[] { offset }, expectedStatus: 400);

        Assert.Contains(ItemAlarmRules.Key, response.ReadAsJson<ErrorEnvelope>().Details.Keys);
    }

    [Theory]
    [InlineData(ItemAlarms.MinOffset)]
    [InlineData(ItemAlarms.MaxOffset)]
    public async Task An_offset_at_either_end_of_the_range_is_accepted(int offset)
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var calendarId = await CalendarTestHelpers.CreateCalendarAsync(fixture, token, "Personal");
        var item = await CalendarTestHelpers.CreateEventAsync(fixture, token, calendarId);
        Assert.NotNull(item);

        var response = await PatchAsync(token, calendarId, item.Id, new[] { offset });

        Assert.Equal([offset], response.ReadAsJson<CalendarItemDto>().Alarms);
    }

    [Fact]
    public async Task A_viewer_is_forbidden()
    {
        var (_, ownerToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var calendarId = await CalendarTestHelpers.CreateCalendarAsync(fixture, ownerToken, "Shared");
        var item = await CalendarTestHelpers.CreateEventAsync(fixture, ownerToken, calendarId);
        Assert.NotNull(item);
        var (_, viewerToken, viewerId) = await fixture.CreateAuthenticatedUserAsync();

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {ownerToken}");
            _.Put.Json(new { Role = CalendarRole.Viewer }).ToUrl($"/calendars/{calendarId}/members/{viewerId}");
            _.StatusCodeShouldBe(204);
        });

        await PatchAsync(viewerToken, calendarId, item.Id, new[] { -60 }, expectedStatus: 403);
    }

    [Fact]
    public async Task A_non_member_gets_not_found()
    {
        var (_, ownerToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var calendarId = await CalendarTestHelpers.CreateCalendarAsync(fixture, ownerToken, "Private");
        var item = await CalendarTestHelpers.CreateEventAsync(fixture, ownerToken, calendarId);
        Assert.NotNull(item);
        var (_, outsiderToken, _) = await fixture.CreateAuthenticatedUserAsync();

        await PatchAsync(outsiderToken, calendarId, item.Id, new[] { -60 }, expectedStatus: 404);
    }

    [Fact]
    public async Task An_unknown_item_gets_not_found()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var calendarId = await CalendarTestHelpers.CreateCalendarAsync(fixture, token, "Personal");

        await PatchAsync(token, calendarId, Guid.CreateVersion7(), new[] { -60 }, expectedStatus: 404);
    }

    [Fact]
    public async Task An_item_from_another_calendar_gets_not_found()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var calendarId = await CalendarTestHelpers.CreateCalendarAsync(fixture, token, "Personal");
        var otherCalendarId = await CalendarTestHelpers.CreateCalendarAsync(fixture, token, "Work");
        var item = await CalendarTestHelpers.CreateEventAsync(fixture, token, otherCalendarId);
        Assert.NotNull(item);

        await PatchAsync(token, calendarId, item.Id, new[] { -60 }, expectedStatus: 404);
    }

    // Rescheduling keeps the alarms: they are offsets from the new start (calendar-item-alarms.md,
    // Failure and edge-case behavior).
    [Fact]
    public async Task Rescheduling_an_item_keeps_its_alarms()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var calendarId = await CalendarTestHelpers.CreateCalendarAsync(fixture, token, "Personal");
        var day = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1);
        var item = await CalendarTestHelpers.CreateTaskAsync(fixture, token, calendarId, dueDate: day, alarms: [-30]);
        Assert.NotNull(item);

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Patch.Json(new { Schedule = new { Kind = (int)CalendarItemKind.Task, DueDate = new { Date = day.AddDays(2), Time = new TimeOnly(8, 0) }, IsAllDay = false } })
                .ToUrl($"/calendars/{calendarId}/items/{item.Id}/schedule");
            _.StatusCodeShouldBeOk();
        });

        Assert.Equal([-30], response.ReadAsJson<CalendarItemDto>().Alarms);
    }

    private Task<IScenarioResult> PatchAsync(string token, Guid calendarId, Guid itemId, int[] alarms, int expectedStatus = 200) =>
        fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Patch.Json(new { Alarms = alarms }).ToUrl($"/calendars/{calendarId}/items/{itemId}/alarms");
            _.StatusCodeShouldBe(expectedStatus);
        });
}
