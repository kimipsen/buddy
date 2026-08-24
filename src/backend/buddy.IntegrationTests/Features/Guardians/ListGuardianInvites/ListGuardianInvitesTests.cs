using Alba;

using buddy.Features.Guardians;
using buddy.IntegrationTests.Features.Guardians;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Xunit;

namespace buddy.IntegrationTests.Features.Guardians.ListGuardianInvites;

[Collection(BuddyApiCollection.Name)]
public sealed class ListGuardianInvitesTests(BuddyApiFixture fixture)
{
    [Fact]
    [CoversEndpoint("ListGuardianInvites")]
    public async Task An_active_guardian_can_list_pending_invites()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        var (invitee, _, _) = await fixture.CreateAuthenticatedUserAsync();

        await GuardianTestHelpers.InviteGuardianAsync(fixture, guardianToken, child.Id, invitee.Email, GuardianKind.Guardian);

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Get.Url($"/users/me/children/{child.Id}/guardian-invites");
            _.StatusCodeShouldBeOk();
        });

        var pending = Assert.Single(response.ReadAsJson<GuardianInviteResponseDto[]>());
        Assert.Equal(invitee.Email.ToLowerInvariant(), pending.Email);
    }

    [Fact]
    public async Task A_caller_with_no_guardian_link_to_the_child_gets_not_found()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");

        var (_, outsiderToken, _) = await fixture.CreateAuthenticatedUserAsync();

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {outsiderToken}");
            _.Get.Url($"/users/me/children/{child.Id}/guardian-invites");
            _.StatusCodeShouldBe(404);
        });
    }
}
