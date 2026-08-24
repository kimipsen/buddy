using Alba;

using buddy.Features.Calendars;
using buddy.Features.Groups;
using buddy.IntegrationTests.Features.Calendars;
using buddy.IntegrationTests.Features.Groups;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Xunit;

namespace buddy.IntegrationTests.Features.Calendars.TransferCalendarToGroup;

[Collection(BuddyApiCollection.Name)]
public sealed class TransferCalendarToGroupTests(BuddyApiFixture fixture)
{
    [Fact]
    [CoversEndpoint("TransferCalendarToGroup")]
    public async Task The_owner_can_move_a_calendar_to_a_different_group_they_manage()
    {
        var (_, ownerToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var groupA = await GroupTestHelpers.CreateGroupAsync(fixture, ownerToken, "Old group");
        var groupB = await GroupTestHelpers.CreateGroupAsync(fixture, ownerToken, "New group");
        var calendarId = await CalendarTestHelpers.CreateCalendarAsync(fixture, ownerToken, "Family Calendar", groupA);

        // A plain member of the old group has default-policy access before the move.
        var (_, oldMemberToken, oldMemberId) = await fixture.CreateAuthenticatedUserAsync();
        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {ownerToken}");
            _.Put.Json(new { Role = GroupRole.Member }).ToUrl($"/groups/{groupA}/members/{oldMemberId}");
            _.StatusCodeShouldBe(204);
        });
        await CalendarTestHelpers.GetCalendarAsync(fixture, oldMemberToken, calendarId);

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {ownerToken}");
            _.Put.Url($"/calendars/{calendarId}/group/{groupB}");
            _.StatusCodeShouldBe(204);
        });

        // The old group's member has lost access -- ownership fully moved, not just added to.
        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {oldMemberToken}");
            _.Get.Url($"/calendars/{calendarId}");
            _.StatusCodeShouldBe(404);
        });

        // A new group's default-policy member now has access.
        var (_, newMemberToken, newMemberId) = await fixture.CreateAuthenticatedUserAsync();
        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {ownerToken}");
            _.Put.Json(new { Role = GroupRole.Member }).ToUrl($"/groups/{groupB}/members/{newMemberId}");
            _.StatusCodeShouldBe(204);
        });
        var body = await CalendarTestHelpers.GetCalendarAsync(fixture, newMemberToken, calendarId);
        Assert.Equal("Family Calendar", body.Name);
    }

    [Fact]
    public async Task An_explicit_member_grant_survives_the_move()
    {
        var (_, ownerToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var groupA = await GroupTestHelpers.CreateGroupAsync(fixture, ownerToken, "Old group");
        var groupB = await GroupTestHelpers.CreateGroupAsync(fixture, ownerToken, "New group");
        var calendarId = await CalendarTestHelpers.CreateCalendarAsync(fixture, ownerToken, "Family Calendar", groupA);

        var (_, viewerToken, viewerId) = await fixture.CreateAuthenticatedUserAsync();
        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {ownerToken}");
            _.Put.Json(new { Role = CalendarRole.Viewer }).ToUrl($"/calendars/{calendarId}/members/{viewerId}");
            _.StatusCodeShouldBe(204);
        });

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {ownerToken}");
            _.Put.Url($"/calendars/{calendarId}/group/{groupB}");
            _.StatusCodeShouldBe(204);
        });

        // The explicit grant is independent of ownership, so it survives the move even though
        // this viewer was never added to groupB at all.
        await CalendarTestHelpers.GetCalendarAsync(fixture, viewerToken, calendarId);
    }

    [Fact]
    public async Task Moving_to_a_group_the_caller_is_not_even_a_member_of_is_not_found()
    {
        var (_, ownerToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var groupA = await GroupTestHelpers.CreateGroupAsync(fixture, ownerToken, "Old group");
        var calendarId = await CalendarTestHelpers.CreateCalendarAsync(fixture, ownerToken, "Family Calendar", groupA);

        var (_, otherOwnerToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var groupB = await GroupTestHelpers.CreateGroupAsync(fixture, otherOwnerToken, "Someone else's group");

        // Not a member of groupB at all -- GroupAuthorization.CheckManage collapses that into
        // NotFound, the same "can't distinguish private from missing" rule every other
        // two-sided-consent share/transfer already follows.
        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {ownerToken}");
            _.Put.Url($"/calendars/{calendarId}/group/{groupB}");
            _.StatusCodeShouldBe(404);
        });
    }

    [Fact]
    public async Task Moving_to_a_group_where_the_caller_is_only_a_plain_member_is_forbidden()
    {
        var (_, ownerToken, ownerId) = await fixture.CreateAuthenticatedUserAsync();
        var groupA = await GroupTestHelpers.CreateGroupAsync(fixture, ownerToken, "Old group");
        var calendarId = await CalendarTestHelpers.CreateCalendarAsync(fixture, ownerToken, "Family Calendar", groupA);

        var (_, otherOwnerToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var groupB = await GroupTestHelpers.CreateGroupAsync(fixture, otherOwnerToken, "Someone else's group");
        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {otherOwnerToken}");
            _.Put.Json(new { Role = GroupRole.Member }).ToUrl($"/groups/{groupB}/members/{ownerId}");
            _.StatusCodeShouldBe(204);
        });

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {ownerToken}");
            _.Put.Url($"/calendars/{calendarId}/group/{groupB}");
            _.StatusCodeShouldBe(403);
        });
    }

    [Fact]
    public async Task A_contributor_cannot_move_the_calendar()
    {
        var (_, ownerToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var groupA = await GroupTestHelpers.CreateGroupAsync(fixture, ownerToken, "Old group");
        var groupB = await GroupTestHelpers.CreateGroupAsync(fixture, ownerToken, "New group");
        var calendarId = await CalendarTestHelpers.CreateCalendarAsync(fixture, ownerToken, "Family Calendar", groupA);

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
            _.Put.Url($"/calendars/{calendarId}/group/{groupB}");
            _.StatusCodeShouldBe(403);
        });
    }

    [Fact]
    public async Task Moving_to_the_same_group_is_idempotent()
    {
        var (_, ownerToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var groupA = await GroupTestHelpers.CreateGroupAsync(fixture, ownerToken, "Group");
        var calendarId = await CalendarTestHelpers.CreateCalendarAsync(fixture, ownerToken, "Family Calendar", groupA);

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {ownerToken}");
            _.Put.Url($"/calendars/{calendarId}/group/{groupA}");
            _.StatusCodeShouldBe(204);
        });
    }
}
