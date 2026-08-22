using Alba;

using buddy.IntegrationTests.Features.Guardians;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Xunit;

namespace buddy.IntegrationTests.Features.Guardians.ListMyGuardians;

[Collection(BuddyApiCollection.Name)]
public sealed class ListMyGuardiansTests(BuddyApiFixture fixture)
{
    [Fact]
    [CoversEndpoint("ListMyGuardians")]
    public async Task Lists_guardians_linked_to_the_calling_child()
    {
        var (_, guardianToken, guardianId) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        var childToken = await GuardianTestHelpers.CompleteChildLoginAsync(fixture, child);

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {childToken}");
            _.Get.Url("/users/me/guardians/");
            _.StatusCodeShouldBeOk();
        });

        var guardians = response.ReadAsJson<GuardianSummaryDto[]>();
        var listed = Assert.Single(guardians);
        Assert.Equal(guardianId, listed.Id);
        Assert.Equal(child.GuardianLinkId, listed.GuardianLinkId);
    }

    [Fact]
    public async Task Requires_authentication()
    {
        await fixture.Host.Scenario(_ =>
        {
            _.Get.Url("/users/me/guardians/");
            _.StatusCodeShouldBe(401);
        });
    }
}
