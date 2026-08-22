using Alba;

using buddy.IntegrationTests.Features.Guardians;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Xunit;

namespace buddy.IntegrationTests.Features.Guardians.ListMyChildren;

[Collection(BuddyApiCollection.Name)]
public sealed class ListMyChildrenTests(BuddyApiFixture fixture)
{
    [Fact]
    [CoversEndpoint("ListMyChildren")]
    public async Task Lists_children_linked_to_the_calling_guardian()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Get.Url("/users/me/children/");
            _.StatusCodeShouldBeOk();
        });

        var children = response.ReadAsJson<ChildSummaryDto[]>();
        var listed = Assert.Single(children);
        Assert.Equal(child.Id, listed.Id);
        Assert.Equal(child.GuardianLinkId, listed.GuardianLinkId);
    }

    [Fact]
    public async Task Requires_authentication()
    {
        await fixture.Host.Scenario(_ =>
        {
            _.Get.Url("/users/me/children/");
            _.StatusCodeShouldBe(401);
        });
    }
}
