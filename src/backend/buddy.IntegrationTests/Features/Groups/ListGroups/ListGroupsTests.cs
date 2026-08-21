using Alba;

using buddy.Features.Groups;
using buddy.IntegrationTests.Features.Groups;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Xunit;

namespace buddy.IntegrationTests.Features.Groups.ListGroups;

[Collection(BuddyApiCollection.Name)]
public sealed class ListGroupsTests(BuddyApiFixture fixture)
{
    [Fact]
    [CoversEndpoint("ListGroups")]
    public async Task Lists_every_group_the_caller_belongs_to_with_their_role_in_each()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var groupId = await GroupTestHelpers.CreateGroupAsync(fixture, token, "Owned Group");

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Get.Url("/groups/");
            _.StatusCodeShouldBeOk();
        });

        var body = response.ReadAsJson<List<GroupSummaryDto>>();

        var summary = Assert.Single(body, g => g.Id == groupId);
        Assert.Equal("Owned Group", summary.Name);
        Assert.Equal(GroupRole.Owner, summary.Role);
    }

    [Fact]
    public async Task A_user_with_no_groups_gets_an_empty_list()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Get.Url("/groups/");
            _.StatusCodeShouldBeOk();
        });

        Assert.Empty(response.ReadAsJson<List<GroupSummaryDto>>());
    }
}
