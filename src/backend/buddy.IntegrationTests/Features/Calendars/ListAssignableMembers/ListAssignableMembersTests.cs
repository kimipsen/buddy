using buddy.Features.Groups;
using buddy.IntegrationTests.Features.Calendars;
using buddy.IntegrationTests.Features.Groups;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Xunit;

namespace buddy.IntegrationTests.Features.Calendars.ListAssignableMembers;

[Collection(BuddyApiCollection.Name)]
public sealed class ListAssignableMembersTests(BuddyApiFixture fixture)
{
    [Fact]
    [CoversEndpoint("ListAssignableCalendarMembers")]
    public async Task A_contributor_sees_every_group_member_as_assignable()
    {
        var (owner, ownerToken, ownerId) = await fixture.CreateAuthenticatedUserAsync();
        var groupId = await GroupTestHelpers.CreateGroupAsync(fixture, ownerToken, "Household");
        var calendarId = await CalendarTestHelpers.CreateCalendarAsync(fixture, ownerToken, "Shared", groupId);
        var (member, memberToken, memberId) = await fixture.CreateAuthenticatedUserAsync();
        await GroupTestHelpers.AddMemberAsync(fixture, ownerToken, groupId, memberToken, member.Email, GroupRole.Member);

        var assignable = await CalendarTestHelpers.ListAssignableMembersAsync(fixture, ownerToken, calendarId);

        Assert.Contains(assignable, m => m.UserId == ownerId);
        Assert.Contains(assignable, m => m.UserId == memberId);
    }

    [Fact]
    public async Task A_non_member_gets_not_found()
    {
        var (_, ownerToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var calendarId = await CalendarTestHelpers.CreateCalendarAsync(fixture, ownerToken, "Private");
        var (_, outsiderToken, _) = await fixture.CreateAuthenticatedUserAsync();

        await CalendarTestHelpers.ListAssignableMembersAsync(fixture, outsiderToken, calendarId, expectedStatus: 404);
    }

    [Fact]
    public async Task A_viewer_cannot_see_the_assignable_list()
    {
        var (_, ownerToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var calendarId = await CalendarTestHelpers.CreateCalendarAsync(fixture, ownerToken, "Shared");
        var (_, viewerToken, viewerId) = await fixture.CreateAuthenticatedUserAsync();

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {ownerToken}");
            _.Put.Json(new { Role = buddy.Features.Calendars.CalendarRole.Viewer }).ToUrl($"/calendars/{calendarId}/members/{viewerId}");
            _.StatusCodeShouldBe(204);
        });

        await CalendarTestHelpers.ListAssignableMembersAsync(fixture, viewerToken, calendarId, expectedStatus: 403);
    }
}
