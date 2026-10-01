using Alba;

using buddy.Common;
using buddy.Features.Calendars;
using buddy.Features.Groups;
using buddy.IntegrationTests.Features.Calendars;
using buddy.IntegrationTests.Features.Groups;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Xunit;

namespace buddy.IntegrationTests.Features.Calendars.CreateCalendar;

[Collection(BuddyApiCollection.Name)]
public sealed class CreateCalendarTests(BuddyApiFixture fixture)
{
    [Fact]
    public async Task Omitting_the_group_is_rejected()
    {
        // GroupId is required -- a calendar is always group-owned now. An omitted GroupId binds
        // to an empty Guid, which resolves to no group at all, collapsing into the same Forbidden
        // "not a manager of this group" already returns for any other unmanaged group.
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Post.Json(new { Name = "No group", TimeZoneId = CalendarTestHelpers.DefaultTimeZone }).ToUrl("/calendars/");
            _.StatusCodeShouldBe(403);
        });
    }

    [Fact]
    public async Task Rejects_an_unrecognized_time_zone()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Post.Json(new { Name = "Bad TZ", TimeZoneId = "Not/A_Real_Zone" }).ToUrl("/calendars/");
            _.StatusCodeShouldBe(400);
        });

        var error = response.ReadAsJson<ErrorEnvelope>();
        Assert.Equal("validation_error", error.Code);
        Assert.Contains("TimeZoneId", error.Details.Keys);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task A_blank_calendar_name_is_rejected(string name)
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var groupId = await GroupTestHelpers.CreateGroupAsync(fixture, token, "Team");

        var error = await PostInvalidCalendarAsync(token, name, groupId);

        Assert.Contains("Name", error.Details.Keys);
    }

    [Fact]
    public async Task A_calendar_name_longer_than_200_characters_is_rejected()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var groupId = await GroupTestHelpers.CreateGroupAsync(fixture, token, "Team");

        var error = await PostInvalidCalendarAsync(token, new string('c', 201), groupId);

        Assert.Contains("Name", error.Details.Keys);
    }

    [Fact]
    public async Task A_calendar_name_of_exactly_200_characters_is_accepted()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var groupId = await GroupTestHelpers.CreateGroupAsync(fixture, token, "Team");
        var name = new string('c', 200);

        var calendarId = await CalendarTestHelpers.CreateCalendarAsync(fixture, token, name, groupId);

        var calendar = await CalendarTestHelpers.GetCalendarAsync(fixture, token, calendarId);
        Assert.Equal(name, calendar.Name);
    }

    [Fact]
    [CoversEndpoint("CreateCalendar")]
    public async Task A_group_admin_can_create_a_calendar_owned_by_the_group()
    {
        var (_, ownerToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var groupId = await GroupTestHelpers.CreateGroupAsync(fixture, ownerToken, "Team");

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {ownerToken}");
            _.Post.Json(new { Name = "Team Calendar", TimeZoneId = CalendarTestHelpers.DefaultTimeZone, GroupId = groupId }).ToUrl("/calendars/");
            _.StatusCodeShouldBeOk();
        });

        var body = response.ReadAsJson<CalendarResponseDto>();
        Assert.Equal("Team Calendar", body.Name);
        // Unlike a legacy personally-owned calendar (which seeded the owner into Members),
        // a group-owned calendar starts with no explicit grants at all -- the group's Owner
        // resolves to CalendarRole.Owner through the default CalendarPermissionPolicy instead.
        Assert.Empty(body.Members);
    }

    [Fact]
    public async Task A_plain_group_member_cannot_create_a_calendar_for_the_group()
    {
        var (_, ownerToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var groupId = await GroupTestHelpers.CreateGroupAsync(fixture, ownerToken, "Team");

        var (_, memberToken, memberId) = await fixture.CreateAuthenticatedUserAsync();

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {ownerToken}");
            _.Put.Json(new { Role = GroupRole.Member }).ToUrl($"/groups/{groupId}/members/{memberId}");
            _.StatusCodeShouldBe(204);
        });

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {memberToken}");
            _.Post.Json(new { Name = "Should Fail", TimeZoneId = CalendarTestHelpers.DefaultTimeZone, GroupId = groupId }).ToUrl("/calendars/");
            _.StatusCodeShouldBe(403);
        });
    }

    private async Task<ErrorEnvelope> PostInvalidCalendarAsync(string token, string name, Guid groupId)
    {
        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Post.Json(new { Name = name, TimeZoneId = CalendarTestHelpers.DefaultTimeZone, GroupId = groupId }).ToUrl("/calendars/");
            _.StatusCodeShouldBe(400);
        });

        var error = response.ReadAsJson<ErrorEnvelope>();
        Assert.Equal("validation_error", error.Code);
        return error;
    }
}
