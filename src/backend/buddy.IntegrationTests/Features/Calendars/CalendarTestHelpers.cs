using Alba;

using buddy.Features.Calendars;
using buddy.IntegrationTests.Features.Groups;
using buddy.IntegrationTests.Fixtures;

namespace buddy.IntegrationTests.Features.Calendars;

internal static class CalendarTestHelpers
{
    public const string DefaultTimeZone = "Europe/Copenhagen";

    // GroupId is required by CreateCalendar; when a test doesn't care which group owns the
    // calendar (most of them -- they're really testing calendar behavior, not group ownership),
    // this transparently stands up a throwaway group with ownerToken as its Owner. The group's
    // default CalendarPermissionPolicy maps GroupRole.Owner -> CalendarRole.Owner, so ownerToken
    // resolves to CalendarRole.Owner exactly as it did for a pre-refactor personally-owned
    // calendar -- every existing call site keeps working unchanged.
    public static async Task<Guid> CreateCalendarAsync(BuddyApiFixture fixture, string ownerToken, string name, Guid? groupId = null)
    {
        var resolvedGroupId = groupId ?? await GroupTestHelpers.CreateGroupAsync(fixture, ownerToken, $"{name} group");

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {ownerToken}");
            _.Post.Json(new { Name = name, TimeZoneId = DefaultTimeZone, GroupId = resolvedGroupId }).ToUrl("/calendars/");
            _.StatusCodeShouldBeOk();
        });

        return response.ReadAsJson<CalendarResponseDto>().Id;
    }

    public static async Task<CalendarResponseDto> GetCalendarAsync(BuddyApiFixture fixture, string token, Guid calendarId)
    {
        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Get.Url($"/calendars/{calendarId}");
            _.StatusCodeShouldBeOk();
        });

        return response.ReadAsJson<CalendarResponseDto>();
    }

    public static async Task<CalendarItemDto?> CreateEventAsync(
        BuddyApiFixture fixture, string token, Guid calendarId, string title = "Standup",
        DateOnly? date = null, int expectedStatus = 200)
    {
        var day = date ?? DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1);

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Post.Json(new
            {
                Kind = CalendarItemKind.Event,
                Title = title,
                Icon = "calendar",
                Color = "#00ff00",
                StartsAt = new { Date = day, Time = new TimeOnly(9, 0) },
                EndsAt = new { Date = day, Time = new TimeOnly(9, 30) }
            }).ToUrl($"/calendars/{calendarId}/items");
            _.StatusCodeShouldBe(expectedStatus);
        });

        return expectedStatus == 200 ? response.ReadAsJson<CalendarItemDto>() : null;
    }

    public static async Task<CalendarItemDto?> CreateTaskAsync(
        BuddyApiFixture fixture, string token, Guid calendarId, string title = "File taxes",
        DateOnly? dueDate = null, RecurrenceRuleRequest? recurrence = null, int expectedStatus = 200)
    {
        var day = dueDate ?? DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1);

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Post.Json(new
            {
                Kind = CalendarItemKind.Task,
                Title = title,
                Icon = "task",
                Color = "#ff0000",
                DueDate = new { Date = day, Time = new TimeOnly(17, 0) },
                Recurrence = recurrence
            }).ToUrl($"/calendars/{calendarId}/items");
            _.StatusCodeShouldBe(expectedStatus);
        });

        return expectedStatus == 200 ? response.ReadAsJson<CalendarItemDto>() : null;
    }

    public static async Task<IcalTokenResponseDto> CreateIcalTokenAsync(BuddyApiFixture fixture, string ownerToken, Guid calendarId)
    {
        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {ownerToken}");
            _.Post.Url($"/calendars/{calendarId}/ical-tokens");
            _.StatusCodeShouldBeOk();
        });

        return response.ReadAsJson<IcalTokenResponseDto>();
    }
}
