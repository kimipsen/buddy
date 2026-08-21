using Alba;

using buddy.Features.Calendars;
using buddy.IntegrationTests.Features.Calendars;
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

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Post.Json(new
            {
                Kind = CalendarItemKind.Event,
                Title = "Incomplete",
                Icon = "calendar",
                Color = "#00ff00",
                StartsAt = new { Date = day, Time = new TimeOnly(9, 0) }
            }).ToUrl($"/calendars/{calendarId}/items");
            _.StatusCodeShouldBe(400);
        });
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
}
