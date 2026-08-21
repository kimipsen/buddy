using Alba;

using buddy.Features.Calendars;
using buddy.IntegrationTests.Features.Calendars;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Xunit;

namespace buddy.IntegrationTests.Features.Calendars.SetMemberRole;

[Collection(BuddyApiCollection.Name)]
public sealed class SetMemberRoleTests(BuddyApiFixture fixture)
{
    [Fact]
    [CoversEndpoint("SetCalendarMemberRole")]
    public async Task The_owner_can_grant_a_role_to_a_new_member()
    {
        var (_, ownerToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var calendarId = await CalendarTestHelpers.CreateCalendarAsync(fixture, ownerToken, "Shared");
        var (_, _, memberId) = await fixture.CreateAuthenticatedUserAsync();

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {ownerToken}");
            _.Put.Json(new { Role = CalendarRole.Contributor }).ToUrl($"/calendars/{calendarId}/members/{memberId}");
            _.StatusCodeShouldBe(204);
        });

        var calendar = await CalendarTestHelpers.GetCalendarAsync(fixture, ownerToken, calendarId);
        var member = Assert.Single(calendar.Members, m => m.UserId == memberId);
        Assert.Equal(CalendarRole.Contributor, member.Role);
    }

    [Fact]
    public async Task Granting_ownership_through_this_endpoint_is_rejected()
    {
        var (_, ownerToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var calendarId = await CalendarTestHelpers.CreateCalendarAsync(fixture, ownerToken, "Shared");
        var (_, _, otherId) = await fixture.CreateAuthenticatedUserAsync();

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {ownerToken}");
            _.Put.Json(new { Role = CalendarRole.Owner }).ToUrl($"/calendars/{calendarId}/members/{otherId}");
            _.StatusCodeShouldBe(400);
        });
    }

    [Fact]
    public async Task The_owner_cannot_change_their_own_role()
    {
        var (_, ownerToken, ownerId) = await fixture.CreateAuthenticatedUserAsync();
        var calendarId = await CalendarTestHelpers.CreateCalendarAsync(fixture, ownerToken, "Shared");

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {ownerToken}");
            _.Put.Json(new { Role = CalendarRole.Viewer }).ToUrl($"/calendars/{calendarId}/members/{ownerId}");
            _.StatusCodeShouldBe(403);
        });
    }

    [Fact]
    public async Task A_contributor_cannot_grant_roles_to_others()
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

        var (_, _, targetId) = await fixture.CreateAuthenticatedUserAsync();

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {contributorToken}");
            _.Put.Json(new { Role = CalendarRole.Viewer }).ToUrl($"/calendars/{calendarId}/members/{targetId}");
            _.StatusCodeShouldBe(403);
        });
    }
}
