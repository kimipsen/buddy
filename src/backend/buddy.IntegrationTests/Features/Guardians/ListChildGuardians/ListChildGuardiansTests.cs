using Alba;

using buddy.Features.Guardians;
using buddy.IntegrationTests.Features.Guardians;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Xunit;

namespace buddy.IntegrationTests.Features.Guardians.ListChildGuardians;

[Collection(BuddyApiCollection.Name)]
public sealed class ListChildGuardiansTests(BuddyApiFixture fixture)
{
    [Fact]
    [CoversEndpoint("ListChildGuardians")]
    public async Task A_guardian_sees_the_childs_other_active_guardian()
    {
        var (firstGuardian, firstGuardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, firstGuardianToken, "Alex");
        var (secondGuardian, secondGuardianToken, secondGuardianId) = await fixture.CreateAuthenticatedUserAsync();

        await GuardianTestHelpers.InviteGuardianAsync(fixture, firstGuardianToken, child.Id, secondGuardian.Email, GuardianKind.Parent);
        var token = await GuardianTestHelpers.ReadGuardianInviteTokenAsync(fixture, secondGuardian.Email);

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {secondGuardianToken}");
            _.Post.Url($"/guardian-invites/{token}/accept");
            _.StatusCodeShouldBe(204);
        });

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {firstGuardianToken}");
            _.Get.Url($"/users/me/children/{child.Id}/guardians");
            _.StatusCodeShouldBeOk();
        });

        var guardians = response.ReadAsJson<List<GuardianSummaryDto>>();
        Assert.Equal(2, guardians.Count);
        Assert.Contains(guardians, g => g.Id == secondGuardianId);
    }

    [Fact]
    public async Task An_unrelated_user_gets_not_found()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        var (_, unrelatedToken, _) = await fixture.CreateAuthenticatedUserAsync();

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {unrelatedToken}");
            _.Get.Url($"/users/me/children/{child.Id}/guardians");
            _.StatusCodeShouldBe(404);
        });
    }
}
