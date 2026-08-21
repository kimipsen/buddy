using Alba;

using buddy.Features.Calendars;
using buddy.IntegrationTests.Features.Calendars;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Xunit;

namespace buddy.IntegrationTests.Features.Calendars.DeleteCalendar;

[Collection(BuddyApiCollection.Name)]
public sealed class DeleteCalendarTests(BuddyApiFixture fixture)
{
    [Fact]
    [CoversEndpoint("DeleteCalendar")]
    public async Task The_owner_can_delete_their_calendar()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var calendarId = await CalendarTestHelpers.CreateCalendarAsync(fixture, token, "Personal");

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Delete.Url($"/calendars/{calendarId}");
            _.StatusCodeShouldBe(204);
        });

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Get.Url($"/calendars/{calendarId}");
            _.StatusCodeShouldBe(404);
        });
    }

    [Fact]
    public async Task A_contributor_cannot_delete_the_calendar()
    {
        var (_, ownerToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var calendarId = await CalendarTestHelpers.CreateCalendarAsync(fixture, ownerToken, "Shared");

        var (_, contributorToken, contributorId) = await fixture.CreateAuthenticatedUserAsync();

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {ownerToken}");
            _.Put.Json(new { Role = CalendarRole.Contributor }).ToUrl($"/calendars/{calendarId}/members/{contributorId}");
            _.StatusCodeShouldBe(204);
        });

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {contributorToken}");
            _.Delete.Url($"/calendars/{calendarId}");
            _.StatusCodeShouldBe(403);
        });
    }
}
