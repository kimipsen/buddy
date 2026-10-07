using Alba;

using buddy.Common;
using buddy.Features.Calendars;
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
                Schedule = new
                {
                    Kind = CalendarItemKind.Event,
                    StartsAt = new { Date = newDay, Time = new TimeOnly(14, 0) },
                    EndsAt = new { Date = newDay, Time = new TimeOnly(15, 0) },
                    IsAllDay = false
                }
            }).ToUrl($"/calendars/{calendarId}/items/{item.Id}/schedule");
            _.StatusCodeShouldBeOk();
        });

        var updated = response.ReadAsJson<CalendarItemDto>();
        Assert.Equal(newDay, updated.Period!.StartsAt.Date);
    }

    [Fact]
    public async Task Rescheduling_a_task_moves_its_due_date_and_keeps_its_assignee()
    {
        var (_, token, userId) = await fixture.CreateAuthenticatedUserAsync();
        var calendarId = await CalendarTestHelpers.CreateCalendarAsync(fixture, token, "Personal");
        var task = await CalendarTestHelpers.CreateTaskAsync(fixture, token, calendarId, assignedTo: userId);
        Assert.NotNull(task);

        var newDay = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(5);

        var updated = await RescheduleAsync(token, calendarId, task.Id, new
        {
            Kind = CalendarItemKind.Task,
            DueDate = new { Date = newDay, Time = new TimeOnly(18, 0) },
            IsAllDay = false
        }, 200);

        var item = updated.ReadAsJson<CalendarItemDto>();
        Assert.Equal(newDay, item.DueDate!.Date);
        Assert.Equal(userId, item.AssignedTo);
    }

    [Fact]
    public async Task Rescheduling_an_event_with_a_task_timing_is_rejected()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var calendarId = await CalendarTestHelpers.CreateCalendarAsync(fixture, token, "Personal");
        var item = await CalendarTestHelpers.CreateEventAsync(fixture, token, calendarId);
        Assert.NotNull(item);

        await RescheduleAsync(token, calendarId, item.Id, new
        {
            Kind = CalendarItemKind.Task,
            DueDate = new { Date = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(5), Time = new TimeOnly(18, 0) },
            IsAllDay = false
        }, 400);
    }

    [Fact]
    public async Task Rescheduling_a_task_with_an_event_timing_is_rejected()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var calendarId = await CalendarTestHelpers.CreateCalendarAsync(fixture, token, "Personal");
        var task = await CalendarTestHelpers.CreateTaskAsync(fixture, token, calendarId);
        Assert.NotNull(task);
        var day = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(5);

        await RescheduleAsync(token, calendarId, task.Id, new
        {
            Kind = CalendarItemKind.Event,
            StartsAt = new { Date = day, Time = new TimeOnly(9, 0) },
            EndsAt = new { Date = day, Time = new TimeOnly(10, 0) },
            IsAllDay = false
        }, 400);
    }

    private Task<IScenarioResult> RescheduleAsync(string token, Guid calendarId, Guid itemId, object schedule, int expectedStatus) =>
        fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Patch.Json(new { Schedule = schedule }).ToUrl($"/calendars/{calendarId}/items/{itemId}/schedule");
            _.StatusCodeShouldBe(expectedStatus);
        });

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
                Schedule = new
                {
                    Kind = CalendarItemKind.Event,
                    StartsAt = new { Date = day, Time = TimeOnly.MinValue },
                    EndsAt = new { Date = day.AddDays(1), Time = TimeOnly.MinValue },
                    IsAllDay = true
                }
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

        // A complete, well-formed body, so the 400 comes from the handler's Period.TryCreate check
        // rather than from request binding.
        var response = await RescheduleAsync(token, calendarId, item.Id, new
        {
            Kind = CalendarItemKind.Event,
            StartsAt = new { Date = day, Time = new TimeOnly(10, 0) },
            EndsAt = new { Date = day, Time = new TimeOnly(9, 0) },
            IsAllDay = false
        }, 400);

        var message = Assert.Single(Assert.Single(response.ReadAsJson<ErrorEnvelope>().Details.Values));
        Assert.Equal("An event's end time must be after its start time.", message);
    }
}
