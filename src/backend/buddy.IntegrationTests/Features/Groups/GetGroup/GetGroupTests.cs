using Alba;

using buddy.IntegrationTests.Features.Groups;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Xunit;

namespace buddy.IntegrationTests.Features.Groups.GetGroup;

[Collection(BuddyApiCollection.Name)]
public sealed class GetGroupTests(BuddyApiFixture fixture)
{
    [Fact]
    [CoversEndpoint("GetGroup")]
    public async Task The_owner_can_view_the_group_they_created()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var groupId = await GroupTestHelpers.CreateGroupAsync(fixture, token, "Design");

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Get.Url($"/groups/{groupId}");
            _.StatusCodeShouldBeOk();
        });

        var body = response.ReadAsJson<GroupResponseDto>();
        Assert.Equal("Design", body.Name);
    }

    [Fact]
    public async Task A_non_member_gets_not_found_rather_than_forbidden()
    {
        var (_, ownerToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var groupId = await GroupTestHelpers.CreateGroupAsync(fixture, ownerToken, "Private Group");

        var (_, outsiderToken, _) = await fixture.CreateAuthenticatedUserAsync();

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {outsiderToken}");
            _.Get.Url($"/groups/{groupId}");
            _.StatusCodeShouldBe(404);
        });
    }

    [Fact]
    public async Task A_missing_group_id_returns_not_found()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Get.Url($"/groups/{Guid.NewGuid()}");
            _.StatusCodeShouldBe(404);
        });
    }
}
