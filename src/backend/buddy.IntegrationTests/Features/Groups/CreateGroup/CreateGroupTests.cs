using Alba;

using buddy.Features.Calendars;
using buddy.Features.Groups;
using buddy.IntegrationTests.Features.Groups;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Xunit;

namespace buddy.IntegrationTests.Features.Groups.CreateGroup;

[Collection(BuddyApiCollection.Name)]
public sealed class CreateGroupTests(BuddyApiFixture fixture)
{
    [Fact]
    [CoversEndpoint("CreateGroup")]
    public async Task Creating_a_group_makes_the_caller_its_owner_with_the_default_calendar_permission_policy()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Post.Json(new { Name = "Engineering" }).ToUrl("/groups/");
            _.StatusCodeShouldBeOk();
        });

        var body = response.ReadAsJson<GroupResponseDto>();

        Assert.Equal("Engineering", body.Name);
        Assert.Equal(GroupRole.Owner, Assert.Single(body.Members).Role);
        Assert.Equal(CalendarRole.Owner, body.CalendarPermissionPolicy[GroupRole.Owner]);
        Assert.Equal(CalendarRole.Contributor, body.CalendarPermissionPolicy[GroupRole.Admin]);
        Assert.Equal(CalendarRole.Viewer, body.CalendarPermissionPolicy[GroupRole.Member]);
    }

    [Fact]
    public async Task Requires_authentication()
    {
        await fixture.Host.Scenario(_ =>
        {
            _.Post.Json(new { Name = "No Auth" }).ToUrl("/groups/");
            _.StatusCodeShouldBe(401);
        });
    }
}
