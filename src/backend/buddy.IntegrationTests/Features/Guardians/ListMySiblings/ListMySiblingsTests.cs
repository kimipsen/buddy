using Alba;

using buddy.IntegrationTests.Features.Guardians;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Xunit;

namespace buddy.IntegrationTests.Features.Guardians.ListMySiblings;

[Collection(BuddyApiCollection.Name)]
public sealed class ListMySiblingsTests(BuddyApiFixture fixture)
{
    [Fact]
    [CoversEndpoint("ListMySiblings")]
    public async Task Lists_other_children_sharing_a_guardian_with_the_calling_child()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var alex = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        var bea = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Bea");
        var alexToken = await GuardianTestHelpers.CompleteChildLoginAsync(fixture, alex);

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {alexToken}");
            _.Get.Url("/users/me/siblings/");
            _.StatusCodeShouldBeOk();
        });

        var siblings = response.ReadAsJson<SiblingSummaryDto[]>();
        var listed = Assert.Single(siblings);
        Assert.Equal(bea.Id, listed.Id);
        Assert.Equal(bea.Name.GivenName, listed.Name.GivenName);
    }

    [Fact]
    public async Task Excludes_children_of_an_unrelated_guardian()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var alex = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        var alexToken = await GuardianTestHelpers.CompleteChildLoginAsync(fixture, alex);

        var (_, otherGuardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        await GuardianTestHelpers.CreateChildAsync(fixture, otherGuardianToken, "Cleo");

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {alexToken}");
            _.Get.Url("/users/me/siblings/");
            _.StatusCodeShouldBeOk();
        });

        Assert.Empty(response.ReadAsJson<SiblingSummaryDto[]>());
    }

    [Fact]
    public async Task Requires_authentication()
    {
        await fixture.Host.Scenario(_ =>
        {
            _.Get.Url("/users/me/siblings/");
            _.StatusCodeShouldBe(401);
        });
    }
}
