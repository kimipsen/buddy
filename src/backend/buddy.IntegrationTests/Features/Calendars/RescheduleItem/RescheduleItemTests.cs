using Alba;

using buddy.IntegrationTests.Features.Calendars;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Xunit;

namespace buddy.IntegrationTests.Features.Calendars.RescheduleItem;

[Collection(BuddyApiCollection.Name)]
public sealed class RescheduleItemTests(BuddyApiFixture fixture)
{
    [Fact]
    [CoversEndpoint("RescheduleCalendarItem")]
    public async Task A_contributor_can_reschedule_an_event()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var calendarId = await CalendarTestHelpers.CreateCalendarAsync(fixture, token, "Personal");
        var item = await CalendarTestHelpers.CreateEventAsync(fixture, token, calendarId);
        Assert.NotNull(item);

        var newDay = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(10);

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Patch.Json(new
            {
                StartsAt = new { Date = newDay, Time = new TimeOnly(14, 0) },
                EndsAt = new { Date = newDay, Time = new TimeOnly(15, 0) }
            }).ToUrl($"/calendars/{calendarId}/items/{item.Id}/schedule");
            _.StatusCodeShouldBeOk();
        });

        var updated = response.ReadAsJson<CalendarItemDto>();
        Assert.Equal(newDay, updated.Period!.StartsAt.Date);
    }

    [Fact]
    public async Task An_event_can_be_toggled_to_all_day()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var calendarId = await CalendarTestHelpers.CreateCalendarAsync(fixture, token, "Personal");
        var item = await CalendarTestHelpers.CreateEventAsync(fixture, token, calendarId);
        Assert.NotNull(item);

        var day = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(5);

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Patch.Json(new
            {
                StartsAt = new { Date = day, Time = TimeOnly.MinValue },
                EndsAt = new { Date = day.AddDays(1), Time = TimeOnly.MinValue },
                IsAllDay = true
            }).ToUrl($"/calendars/{calendarId}/items/{item.Id}/schedule");
            _.StatusCodeShouldBeOk();
        });

        var updated = response.ReadAsJson<CalendarItemDto>();
        Assert.True(updated.Period!.IsAllDay);
    }

    [Fact]
    public async Task Rescheduling_an_event_to_end_before_it_starts_is_rejected()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var calendarId = await CalendarTestHelpers.CreateCalendarAsync(fixture, token, "Personal");
        var item = await CalendarTestHelpers.CreateEventAsync(fixture, token, calendarId);
        Assert.NotNull(item);
        var day = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1);

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Patch.Json(new
            {
                StartsAt = new { Date = day, Time = new TimeOnly(10, 0) },
                EndsAt = new { Date = day, Time = new TimeOnly(9, 0) }
            }).ToUrl($"/calendars/{calendarId}/items/{item.Id}/schedule");
            _.StatusCodeShouldBe(400);
        });
    }
}
