using Alba;

using buddy.IntegrationTests.Features.Guardians;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Xunit;

namespace buddy.IntegrationTests.Features.Babysitters.ListMyBabysitters;

[Collection(BuddyApiCollection.Name)]
public sealed class ListMyBabysittersTests(BuddyApiFixture fixture)
{
    [Fact]
    [CoversEndpoint("ListMyBabysitters")]
    public async Task A_guardian_who_never_added_anyone_reads_an_empty_list()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();

        Assert.Empty(await BabysitterTestHelpers.ListMineAsync(fixture, token));
    }

    [Fact]
    public async Task The_list_is_in_the_order_babysitters_were_added()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var bo = await BabysitterTestHelpers.AddAsync(fixture, token, "Bo");
        var anna = await BabysitterTestHelpers.AddAsync(fixture, token, "Anna");

        var list = await BabysitterTestHelpers.ListMineAsync(fixture, token);

        Assert.Equal([bo.Id, anna.Id], list.Select(b => b.Id));
    }

    [Fact]
    public async Task A_child_account_is_forbidden()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        var childToken = await GuardianTestHelpers.CompleteChildLoginAsync(fixture, child);

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {childToken}");
            _.Get.Url("/babysitters/me");
            _.StatusCodeShouldBe(403);
        });
    }
}
