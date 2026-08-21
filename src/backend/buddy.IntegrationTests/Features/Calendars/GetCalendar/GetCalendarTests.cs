using Alba;

using buddy.Features.Groups;
using buddy.IntegrationTests.Features.Calendars;
using buddy.IntegrationTests.Features.Groups;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Xunit;

namespace buddy.IntegrationTests.Features.Calendars.GetCalendar;

[Collection(BuddyApiCollection.Name)]
public sealed class GetCalendarTests(BuddyApiFixture fixture)
{
    [Fact]
    [CoversEndpoint("GetCalendar")]
    public async Task The_owner_can_view_the_calendar_they_created()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var calendarId = await CalendarTestHelpers.CreateCalendarAsync(fixture, token, "Personal");

        var body = await CalendarTestHelpers.GetCalendarAsync(fixture, token, calendarId);
        Assert.Equal("Personal", body.Name);
    }

    [Fact]
    public async Task A_non_member_gets_not_found_rather_than_forbidden()
    {
        var (_, ownerToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var calendarId = await CalendarTestHelpers.CreateCalendarAsync(fixture, ownerToken, "Private");

        var (_, outsiderToken, _) = await fixture.CreateAuthenticatedUserAsync();

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {outsiderToken}");
            _.Get.Url($"/calendars/{calendarId}");
            _.StatusCodeShouldBe(404);
        });
    }

    [Fact]
    public async Task A_group_member_can_view_a_group_owned_calendar_via_the_default_permission_policy()
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

        // Default policy maps GroupRole.Member -> CalendarRole.Viewer, so a plain group member
        // can view the group's calendar without ever being added to Calendar.Members directly.
        var body = await CalendarTestHelpers.GetCalendarAsync(fixture, memberToken, calendarId);
        Assert.Equal("Team Calendar", body.Name);
    }
}
