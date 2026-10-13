using Alba;

using buddy.Features.Calendars;
using buddy.Features.TransitionWarnings;
using buddy.IntegrationTests.Features.Calendars;
using buddy.IntegrationTests.Features.Groups;
using buddy.IntegrationTests.Features.Guardians;
using buddy.IntegrationTests.Fixtures;

namespace buddy.IntegrationTests.Features.TransitionWarnings;

// Response shapes of the TransitionWarnings endpoints (TransitionWarningsResponses.cs).
internal sealed record QuietHoursDto(TimeOnly From, TimeOnly To);

internal sealed record ItemWarningOverrideDto(Guid ItemId, IReadOnlyList<int> LeadMinutes);

internal sealed record ItemWarningOverrideDetailDto(Guid ItemId, string Title, string Icon, CalendarItemKind Kind, IReadOnlyList<int> LeadMinutes);

internal sealed record MyTransitionWarningsDto(
    bool Enabled, IReadOnlyList<int> LeadMinutes, IReadOnlyList<WarningKind> Kinds, QuietHoursDto? QuietHours, IReadOnlyList<ItemWarningOverrideDto> Overrides);

internal sealed record ChildTransitionWarningsDto(
    Guid ChildId, bool Enabled, IReadOnlyList<int> LeadMinutes, IReadOnlyList<WarningKind> Kinds, QuietHoursDto? QuietHours, IReadOnlyList<ItemWarningOverrideDetailDto> Overrides);

// A guardian with one child, a family calendar and one event in it.
internal sealed record WarningFamily(string GuardianToken, Guid GuardianId, ChildResponseDto Child, Guid CalendarId, CalendarItemDto Event);

internal static class TransitionWarningsTestHelpers
{
    public static readonly WarningKind[] AllKinds = [WarningKind.Event, WarningKind.Task, WarningKind.DropOff, WarningKind.PickUp];

    public static async Task<WarningFamily> CreateFamilyAsync(BuddyApiFixture fixture, string eventTitle = "Football")
    {
        var (_, token, guardianId) = await fixture.CreateAuthenticatedUserAsync();
        var groupId = await GroupTestHelpers.CreateGroupAsync(fixture, token, "Family");
        var calendarId = await CalendarTestHelpers.CreateCalendarAsync(fixture, token, "Family", groupId);
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token, "Emil");
        var @event = await CalendarTestHelpers.CreateEventAsync(fixture, token, calendarId, eventTitle)
            ?? throw new InvalidOperationException("The event was not created.");

        return new WarningFamily(token, guardianId, child, calendarId, @event);
    }

    public static object Settings(bool enabled, int[] leadMinutes, WarningKind[]? kinds = null, QuietHoursDto? quietHours = null) =>
        new { Enabled = enabled, LeadMinutes = leadMinutes, Kinds = kinds ?? AllKinds, QuietHours = quietHours };

    public static async Task<ChildTransitionWarningsDto?> ConfigureAsync(BuddyApiFixture fixture, string token, Guid childId, object body, int expectedStatus = 200)
    {
        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Put.Json(body).ToUrl($"/transition-warnings/children/{childId}");
            _.StatusCodeShouldBe(expectedStatus);
        });

        return expectedStatus == 200 ? response.ReadAsJson<ChildTransitionWarningsDto>() : null;
    }

    public static async Task<ChildTransitionWarningsDto?> GetForChildAsync(BuddyApiFixture fixture, string token, Guid childId, int expectedStatus = 200)
    {
        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Get.Url($"/transition-warnings/children/{childId}");
            _.StatusCodeShouldBe(expectedStatus);
        });

        return expectedStatus == 200 ? response.ReadAsJson<ChildTransitionWarningsDto>() : null;
    }

    public static async Task<MyTransitionWarningsDto> GetMineAsync(BuddyApiFixture fixture, string token)
    {
        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Get.Url("/transition-warnings/me");
            _.StatusCodeShouldBeOk();
        });

        return response.ReadAsJson<MyTransitionWarningsDto>();
    }

    public static async Task<IScenarioResult> SetOverrideAsync(BuddyApiFixture fixture, string token, Guid childId, Guid itemId, int[] leadMinutes, int expectedStatus = 200) =>
        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Put.Json(new { LeadMinutes = leadMinutes }).ToUrl($"/transition-warnings/children/{childId}/items/{itemId}");
            _.StatusCodeShouldBe(expectedStatus);
        });

    public static async Task ClearOverrideAsync(BuddyApiFixture fixture, string token, Guid childId, Guid itemId, int expectedStatus = 204) =>
        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Delete.Url($"/transition-warnings/children/{childId}/items/{itemId}");
            _.StatusCodeShouldBe(expectedStatus);
        });

    public static async Task RevokeLinkAsync(BuddyApiFixture fixture, string token, Guid childId) =>
        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Delete.Url($"/users/me/children/{childId}/guardian-link");
            _.StatusCodeShouldBe(204);
        });
}
