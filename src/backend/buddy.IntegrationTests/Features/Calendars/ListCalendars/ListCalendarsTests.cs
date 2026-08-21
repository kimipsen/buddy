using Alba;

using buddy.Features.Calendars;
using buddy.Features.Groups;
using buddy.IntegrationTests.Features.Calendars;
using buddy.IntegrationTests.Features.Groups;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Xunit;

namespace buddy.IntegrationTests.Features.Calendars.ListCalendars;

[Collection(BuddyApiCollection.Name)]
public sealed class ListCalendarsTests(BuddyApiFixture fixture)
{
    [Fact]
    [CoversEndpoint("ListCalendars")]
    public async Task Lists_calendars_the_caller_explicitly_belongs_to()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var calendarId = await CalendarTestHelpers.CreateCalendarAsync(fixture, token, "Mine");

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Get.Url("/calendars/");
            _.StatusCodeShouldBeOk();
        });

        var body = response.ReadAsJson<List<CalendarSummaryDto>>();
        var summary = Assert.Single(body, c => c.Id == calendarId);
        Assert.Equal(CalendarRole.Owner, summary.Role);
    }

    [Fact]
    public async Task Also_lists_calendars_owned_by_a_group_the_caller_belongs_to()
    {
        var (_, ownerToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var groupId = await GroupTestHelpers.CreateGroupAsync(fixture, ownerToken, "Team");
        var calendarId = await CalendarTestHelpers.CreateCalendarAsync(fixture, ownerToken, "Team Calendar", groupId);

        var (_, memberToken, memberId) = await fixture.CreateAuthenticatedUserAsync();

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {ownerToken}");
            _.Put.Json(new { Role = GroupRole.Member }).ToUrl($"/groups/{groupId}/members/{memberId}");
            _.StatusCodeShouldBe(204);
        });

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {memberToken}");
            _.Get.Url("/calendars/");
            _.StatusCodeShouldBeOk();
        });

        var body = response.ReadAsJson<List<CalendarSummaryDto>>();
        var summary = Assert.Single(body, c => c.Id == calendarId);
        Assert.Equal(CalendarRole.Viewer, summary.Role);
    }
}
