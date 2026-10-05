using Alba;

using buddy.IntegrationTests.Features.Guardians;
using buddy.IntegrationTests.Features.WorkLocations;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Xunit;

namespace buddy.IntegrationTests.Features.Babysitters.ListChildBabysitters;

[Collection(BuddyApiCollection.Name)]
public sealed class ListChildBabysittersTests(BuddyApiFixture fixture)
{
    [Fact]
    [CoversEndpoint("ListChildBabysitters")]
    public async Task Lists_the_active_babysitters_of_every_guardian_of_the_child_sorted_by_name()
    {
        var family = await WorkLocationTestHelpers.CreateCoGuardiansAsync(fixture);
        var cleo = await BabysitterTestHelpers.AddAsync(fixture, family.FirstToken, "Cleo", "111");
        var archived = await BabysitterTestHelpers.AddAsync(fixture, family.FirstToken, "Dora");
        await BabysitterTestHelpers.ArchiveAsync(fixture, family.FirstToken, archived.Id);
        var anna = await BabysitterTestHelpers.AddAsync(fixture, family.SecondToken, "Anna");

        var list = await ListAsync(family.SecondToken, family.Child.Id);

        Assert.Equal(
            [
                new ChildBabysitterDto(family.SecondId, anna.Id, "Anna", ""),
                new ChildBabysitterDto(family.FirstId, cleo.Id, "Cleo", "111")
            ],
            list);
    }

    [Fact]
    public async Task A_revoked_guardians_babysitters_drop_out()
    {
        var family = await WorkLocationTestHelpers.CreateCoGuardiansAsync(fixture);
        await BabysitterTestHelpers.AddAsync(fixture, family.SecondToken, "Anna");

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {family.SecondToken}");
            _.Delete.Url($"/users/me/children/{family.Child.Id}/guardian-link");
            _.StatusCodeShouldBe(204);
        });

        Assert.Empty(await ListAsync(family.FirstToken, family.Child.Id));
    }

    [Fact]
    public async Task The_child_and_a_stranger_get_not_found()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        var childToken = await GuardianTestHelpers.CompleteChildLoginAsync(fixture, child);
        var (_, strangerToken, _) = await fixture.CreateAuthenticatedUserAsync();

        foreach (var token in new[] { childToken, strangerToken })
        {
            await fixture.Host.Scenario(_ =>
            {
                _.WithRequestHeader("Authorization", $"Bearer {token}");
                _.Get.Url($"/babysitters/children/{child.Id}");
                _.StatusCodeShouldBe(404);
            });
        }
    }

    private async Task<ChildBabysitterDto[]> ListAsync(string token, Guid childId) =>
        (await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Get.Url($"/babysitters/children/{childId}");
            _.StatusCodeShouldBeOk();
        })).ReadAsJson<ChildBabysitterDto[]>();
}
