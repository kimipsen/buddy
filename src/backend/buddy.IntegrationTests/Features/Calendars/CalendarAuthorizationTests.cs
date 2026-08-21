using Alba;

using buddy.Features.Calendars;
using buddy.Features.Groups;
using buddy.IntegrationTests.Features.Groups;
using buddy.IntegrationTests.Fixtures;

using Xunit;

namespace buddy.IntegrationTests.Features.Calendars;

// Covers the group/calendar permission resolution contract from
// docs/backend/analysis/group-owned-calendars-and-permissions.md: an explicit Calendar.Members
// grant always wins over a group-derived role (even a lower one), a group member with no
// explicit grant falls back to CalendarPermissionPolicy, and deleting a group cascades to its
// owned calendars. This is deliberately separate from CreateCalendarTests/GetCalendarTests etc.
// since it exercises the cross-feature resolution logic in CalendarAuthorization.ResolveRole
// directly, not any single endpoint.
[Collection(BuddyApiCollection.Name)]
public sealed class CalendarAuthorizationTests(BuddyApiFixture fixture)
{
    [Fact]
    public async Task An_explicit_calendar_grant_wins_over_a_higher_group_derived_role()
    {
        var (_, ownerToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var groupId = await GroupTestHelpers.CreateGroupAsync(fixture, ownerToken, "Team");
        var calendarId = await CalendarTestHelpers.CreateCalendarAsync(fixture, ownerToken, "Team Calendar", groupId);

        // Default policy maps GroupRole.Admin -> CalendarRole.Contributor, but an explicit
        // Viewer grant directly on the calendar should still win.
        var (_, adminToken, adminId) = await fixture.CreateAuthenticatedUserAsync();

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {ownerToken}");
            _.Put.Json(new { Role = GroupRole.Admin }).ToUrl($"/groups/{groupId}/members/{adminId}");
            _.StatusCodeShouldBe(204);
        });

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {ownerToken}");
            _.Put.Json(new { Role = CalendarRole.Viewer }).ToUrl($"/calendars/{calendarId}/members/{adminId}");
            _.StatusCodeShouldBe(204);
        });

        await CalendarTestHelpers.CreateEventAsync(fixture, adminToken, calendarId, expectedStatus: 403);
    }

    [Fact]
    public async Task A_group_member_with_no_explicit_grant_gets_their_role_from_the_policy()
    {
        var (_, ownerToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var groupId = await GroupTestHelpers.CreateGroupAsync(fixture, ownerToken, "Team");
        var calendarId = await CalendarTestHelpers.CreateCalendarAsync(fixture, ownerToken, "Team Calendar", groupId);

        var (_, adminToken, adminId) = await fixture.CreateAuthenticatedUserAsync();

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {ownerToken}");
            _.Put.Json(new { Role = GroupRole.Admin }).ToUrl($"/groups/{groupId}/members/{adminId}");
            _.StatusCodeShouldBe(204);
        });

        // No explicit Calendar.Members grant at all -- default policy maps Admin -> Contributor,
        // which is enough to create items.
        var item = await CalendarTestHelpers.CreateEventAsync(fixture, adminToken, calendarId);
        Assert.NotNull(item);
    }

    [Fact]
    public async Task Deleting_the_owning_group_cascades_to_delete_its_calendar()
    {
        var (_, ownerToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var groupId = await GroupTestHelpers.CreateGroupAsync(fixture, ownerToken, "Team");
        var calendarId = await CalendarTestHelpers.CreateCalendarAsync(fixture, ownerToken, "Team Calendar", groupId);

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {ownerToken}");
            _.Delete.Url($"/groups/{groupId}");
            _.StatusCodeShouldBe(204);
        });

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {ownerToken}");
            _.Get.Url($"/calendars/{calendarId}");
            _.StatusCodeShouldBe(404);
        });
    }
}
