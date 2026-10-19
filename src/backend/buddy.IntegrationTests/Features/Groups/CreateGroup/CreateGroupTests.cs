using Alba;

using buddy.Common;
using buddy.Features.Calendars;
using buddy.Features.Groups;
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

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Rejects_a_blank_name(string name)
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();

        var error = await PostInvalidGroupAsync(token, name);

        Assert.Equal("name", Assert.Single(error.Details.Keys));
    }

    [Fact]
    public async Task Rejects_a_name_longer_than_200_characters()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();

        var error = await PostInvalidGroupAsync(token, new string('a', 201));

        Assert.Equal("name", Assert.Single(error.Details.Keys));
    }

    [Fact]
    public async Task Stores_a_name_of_200_characters_trimmed()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var name = new string('g', 200);

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Post.Json(new { Name = $"  {name} " }).ToUrl("/groups/");
            _.StatusCodeShouldBeOk();
        });

        Assert.Equal(name, response.ReadAsJson<GroupResponseDto>().Name);
    }

    private async Task<ErrorEnvelope> PostInvalidGroupAsync(string token, string name)
    {
        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Post.Json(new { Name = name }).ToUrl("/groups/");
            _.StatusCodeShouldBe(400);
        });

        var error = response.ReadAsJson<ErrorEnvelope>();
        Assert.Equal("validation_error", error.Code);
        return error;
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
