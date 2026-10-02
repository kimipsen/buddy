using Alba;

using buddy.Common;
using buddy.Features.Calendars;
using buddy.Features.Groups;
using buddy.IntegrationTests.Features.Calendars;
using buddy.IntegrationTests.Features.Groups;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Xunit;

namespace buddy.IntegrationTests.Features.Calendars.CreateItem;

[Collection(BuddyApiCollection.Name)]
public sealed class CreateItemTests(BuddyApiFixture fixture)
{
    [Fact]
    [CoversEndpoint("CreateCalendarItem")]
    public async Task A_contributor_can_create_an_event_item()
    {
        var (_, ownerToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var calendarId = await CalendarTestHelpers.CreateCalendarAsync(fixture, ownerToken, "Shared");

        var item = await CalendarTestHelpers.CreateEventAsync(fixture, ownerToken, calendarId, "Standup");

        Assert.NotNull(item);
        Assert.Equal("Standup", item.Title);
        Assert.Equal(CalendarItemKind.Event, item.Kind);
        Assert.NotNull(item.Period);
    }

    [Fact]
    public async Task Can_create_an_all_day_event_spanning_multiple_days()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var calendarId = await CalendarTestHelpers.CreateCalendarAsync(fixture, token, "Personal");
        var start = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1);

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Post.Json(new
            {
                Kind = CalendarItemKind.Event,
                Title = "Trip",
                Icon = "calendar",
                Color = "#00ff00",
                StartsAt = new { Date = start, Time = TimeOnly.MinValue },
                EndsAt = new { Date = start.AddDays(3), Time = TimeOnly.MinValue },
                IsAllDay = true
            }).ToUrl($"/calendars/{calendarId}/items");
            _.StatusCodeShouldBeOk();
        });

        var item = response.ReadAsJson<CalendarItemDto>();
        Assert.True(item.Period!.IsAllDay);
        Assert.Equal(start, item.Period.StartsAt.Date);
        Assert.Equal(start.AddDays(3), item.Period.EndsAt.Date);
    }

    [Fact]
    public async Task Can_create_an_all_day_task()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var calendarId = await CalendarTestHelpers.CreateCalendarAsync(fixture, token, "Personal");

        var item = await CalendarTestHelpers.CreateTaskAsync(fixture, token, calendarId, "Anniversary", isAllDay: true);

        Assert.NotNull(item);
        Assert.True(item.DueDate!.IsAllDay);
    }

    [Fact]
    public async Task Can_create_a_task_item_with_a_due_date()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var calendarId = await CalendarTestHelpers.CreateCalendarAsync(fixture, token, "Personal");
        var due = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(3);

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Post.Json(new
            {
                Kind = CalendarItemKind.Task,
                IsAllDay = false,
                Title = "File taxes",
                Icon = "task",
                Color = "#ff0000",
                DueDate = new { Date = due, Time = new TimeOnly(17, 0) }
            }).ToUrl($"/calendars/{calendarId}/items");
            _.StatusCodeShouldBeOk();
        });

        var item = response.ReadAsJson<CalendarItemDto>();
        Assert.Equal(CalendarItemKind.Task, item.Kind);
        Assert.NotNull(item.DueDate);
    }

    [Fact]
    public async Task An_event_missing_an_end_time_is_rejected()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var calendarId = await CalendarTestHelpers.CreateCalendarAsync(fixture, token, "Personal");
        var day = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1);

        var error = await PostInvalidItemAsync(token, calendarId, new
        {
            Kind = CalendarItemKind.Event,
            IsAllDay = false,
            Title = "Incomplete",
            Icon = "calendar",
            Color = "#00ff00",
            StartsAt = new { Date = day, Time = new TimeOnly(9, 0) }
        });

        Assert.Contains("EndsAt", error.Details.Keys);
    }

    [Fact]
    public async Task An_event_missing_a_start_time_is_rejected()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var calendarId = await CalendarTestHelpers.CreateCalendarAsync(fixture, token, "Personal");
        var day = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1);

        var error = await PostInvalidItemAsync(token, calendarId, new
        {
            Kind = CalendarItemKind.Event,
            IsAllDay = false,
            Title = "Incomplete",
            Icon = "calendar",
            Color = "#00ff00",
            EndsAt = new { Date = day, Time = new TimeOnly(9, 30) }
        });

        Assert.Contains("StartsAt", error.Details.Keys);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-30)]
    public async Task An_event_that_does_not_end_after_it_starts_is_rejected(int endOffsetMinutes)
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var calendarId = await CalendarTestHelpers.CreateCalendarAsync(fixture, token, "Personal");
        var day = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1);
        var start = new TimeOnly(9, 0);

        var error = await PostInvalidItemAsync(token, calendarId, new
        {
            Kind = CalendarItemKind.Event,
            IsAllDay = false,
            Title = "Backwards",
            Icon = "calendar",
            Color = "#00ff00",
            StartsAt = new { Date = day, Time = start },
            EndsAt = new { Date = day, Time = start.AddMinutes(endOffsetMinutes) }
        });

        // The end-after-start rule validates the whole command (RuleFor(x => x)), so FluentValidation
        // reports it under the empty property name.
        Assert.Contains("", error.Details.Keys);
    }

    [Fact]
    public async Task A_task_missing_a_due_date_is_rejected()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var calendarId = await CalendarTestHelpers.CreateCalendarAsync(fixture, token, "Personal");

        var error = await PostInvalidItemAsync(token, calendarId, new
        {
            Kind = CalendarItemKind.Task,
            IsAllDay = false,
            Title = "No due date",
            Icon = "task",
            Color = "#ff0000"
        });

        Assert.Contains("DueDate", error.Details.Keys);
    }

    [Fact]
    public async Task An_item_title_longer_than_200_characters_is_rejected()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var calendarId = await CalendarTestHelpers.CreateCalendarAsync(fixture, token, "Personal");

        var error = await PostInvalidItemAsync(token, calendarId, new
        {
            Kind = CalendarItemKind.Task,
            IsAllDay = false,
            Title = new string('t', 201),
            Icon = "task",
            Color = "#ff0000",
            DueDate = new { Date = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1), Time = new TimeOnly(17, 0) }
        });

        Assert.Contains("Title", error.Details.Keys);
    }

    [Fact]
    public async Task An_item_title_of_exactly_200_characters_is_accepted()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var calendarId = await CalendarTestHelpers.CreateCalendarAsync(fixture, token, "Personal");
        var title = new string('t', 200);

        var item = await CalendarTestHelpers.CreateTaskAsync(fixture, token, calendarId, title);

        Assert.NotNull(item);
        Assert.Equal(title, item.Title);
    }

    [Fact]
    public async Task A_recurrence_interval_count_below_one_is_rejected()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var calendarId = await CalendarTestHelpers.CreateCalendarAsync(fixture, token, "Personal");

        var error = await PostInvalidItemAsync(token, calendarId, new
        {
            Kind = CalendarItemKind.Task,
            IsAllDay = false,
            Title = "Water plants",
            Icon = "task",
            Color = "#ff0000",
            DueDate = new { Date = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1), Time = new TimeOnly(17, 0) },
            Recurrence = new { Frequency = RecurrenceFrequency.Daily, IntervalCount = 0, Until = (DateOnly?)null }
        });

        Assert.Contains("Recurrence.IntervalCount", error.Details.Keys);
    }

    [Fact]
    public async Task A_recurrence_interval_count_of_one_is_accepted()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var calendarId = await CalendarTestHelpers.CreateCalendarAsync(fixture, token, "Personal");

        var item = await CalendarTestHelpers.CreateTaskAsync(
            fixture, token, calendarId, "Water plants", recurrence: new RecurrenceRuleRequest(RecurrenceFrequency.Daily, 1, null));

        Assert.NotNull(item);
        Assert.NotNull(item.Recurrence);
        Assert.Equal(1, item.Recurrence.IntervalCount);
    }

    [Fact]
    public async Task A_viewer_cannot_create_items()
    {
        var (_, ownerToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var calendarId = await CalendarTestHelpers.CreateCalendarAsync(fixture, ownerToken, "Shared");
        var (_, viewerToken, viewerId) = await fixture.CreateAuthenticatedUserAsync();

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {ownerToken}");
            _.Put.Json(new { Role = CalendarRole.Viewer }).ToUrl($"/calendars/{calendarId}/members/{viewerId}");
            _.StatusCodeShouldBe(204);
        });

        await CalendarTestHelpers.CreateEventAsync(fixture, viewerToken, calendarId, expectedStatus: 403);
    }

    [Fact]
    public async Task A_non_member_gets_not_found()
    {
        var (_, ownerToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var calendarId = await CalendarTestHelpers.CreateCalendarAsync(fixture, ownerToken, "Private");
        var (_, outsiderToken, _) = await fixture.CreateAuthenticatedUserAsync();

        await CalendarTestHelpers.CreateEventAsync(fixture, outsiderToken, calendarId, expectedStatus: 404);
    }

    [Fact]
    public async Task A_task_can_be_assigned_to_a_fellow_group_member()
    {
        var (_, ownerToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var groupId = await GroupTestHelpers.CreateGroupAsync(fixture, ownerToken, "Household");
        var calendarId = await CalendarTestHelpers.CreateCalendarAsync(fixture, ownerToken, "Shared", groupId);
        var (member, memberToken, memberId) = await fixture.CreateAuthenticatedUserAsync();
        await GroupTestHelpers.AddMemberAsync(fixture, ownerToken, groupId, memberToken, member.Email, GroupRole.Member);

        var item = await CalendarTestHelpers.CreateTaskAsync(fixture, ownerToken, calendarId, assignedTo: memberId);

        Assert.NotNull(item);
        Assert.Equal(memberId, item.AssignedTo);
    }

    [Fact]
    public async Task Assigning_a_task_to_someone_without_calendar_access_is_rejected()
    {
        var (_, ownerToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var calendarId = await CalendarTestHelpers.CreateCalendarAsync(fixture, ownerToken, "Shared");
        var (_, _, outsiderId) = await fixture.CreateAuthenticatedUserAsync();

        await CalendarTestHelpers.CreateTaskAsync(fixture, ownerToken, calendarId, assignedTo: outsiderId, expectedStatus: 400);
    }

    [Fact]
    public async Task An_event_cannot_be_assigned_to_someone()
    {
        var (_, ownerToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var calendarId = await CalendarTestHelpers.CreateCalendarAsync(fixture, ownerToken, "Shared");

        var error = await PostInvalidItemAsync(ownerToken, calendarId, new
        {
            Kind = CalendarItemKind.Event,
            IsAllDay = false,
            Title = "Standup",
            Icon = "calendar",
            Color = "#00ff00",
            StartsAt = new { Date = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1), Time = new TimeOnly(9, 0) },
            EndsAt = new { Date = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1), Time = new TimeOnly(9, 30) },
            AssignedTo = Guid.NewGuid()
        });

        // Asserting the field key matters: without the validator rule, the handler's own
        // assignee-access check would still reject this random id with a 400 under "".
        Assert.Contains("AssignedTo", error.Details.Keys);
    }

    private async Task<ErrorEnvelope> PostInvalidItemAsync(string token, Guid calendarId, object body)
    {
        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Post.Json(body).ToUrl($"/calendars/{calendarId}/items");
            _.StatusCodeShouldBe(400);
        });

        var error = response.ReadAsJson<ErrorEnvelope>();
        Assert.Equal("validation_error", error.Code);
        return error;
    }
}
