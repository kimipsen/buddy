using Alba;

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
    [CoversEndpoint("CreateCalendar")]
    public async Task Creating_a_personal_calendar_makes_the_caller_its_owner()
    {
        var (_, token, userId) = await fixture.CreateAuthenticatedUserAsync();

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Post.Json(new { Name = "Personal", TimeZoneId = CalendarTestHelpers.DefaultTimeZone }).ToUrl("/calendars/");
            _.StatusCodeShouldBeOk();
        });

        var body = response.ReadAsJson<CalendarResponseDto>();

        Assert.Equal("Personal", body.Name);
        Assert.Equal(CalendarTestHelpers.DefaultTimeZone, body.TimeZoneId);
        var owner = Assert.Single(body.Members);
        Assert.Equal(userId, owner.UserId);
        Assert.Equal(CalendarRole.Owner, owner.Role);
    }

    [Fact]
    public async Task Rejects_an_unrecognized_time_zone()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Post.Json(new { Name = "Bad TZ", TimeZoneId = "Not/A_Real_Zone" }).ToUrl("/calendars/");
            _.StatusCodeShouldBe(400);
        });
    }

    [Fact]
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
}
