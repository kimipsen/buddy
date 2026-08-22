using Alba;

using buddy.IntegrationTests.Features.Guardians;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Xunit;

namespace buddy.IntegrationTests.Features.Guardians.RevokeGuardianLink;

[Collection(BuddyApiCollection.Name)]
public sealed class RevokeGuardianLinkTests(BuddyApiFixture fixture)
{
    [Fact]
    [CoversEndpoint("RevokeGuardianLink")]
    public async Task Revoking_the_link_removes_the_child_from_the_guardians_list()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Delete.Url($"/users/me/children/{child.Id}/guardian-link");
            _.StatusCodeShouldBe(204);
        });

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Get.Url("/users/me/children/");
            _.StatusCodeShouldBeOk();
        });

        Assert.Empty(response.ReadAsJson<ChildSummaryDto[]>());
    }

    [Fact]
    public async Task Revoking_a_link_that_does_not_exist_returns_not_found()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var (_, _, unrelatedUserId) = await fixture.CreateAuthenticatedUserAsync();

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Delete.Url($"/users/me/children/{unrelatedUserId}/guardian-link");
            _.StatusCodeShouldBe(404);
        });
    }

    [Fact]
    public async Task Requires_authentication()
    {
        await fixture.Host.Scenario(_ =>
        {
            _.Delete.Url($"/users/me/children/{Guid.NewGuid()}/guardian-link");
            _.StatusCodeShouldBe(401);
        });
    }
}
