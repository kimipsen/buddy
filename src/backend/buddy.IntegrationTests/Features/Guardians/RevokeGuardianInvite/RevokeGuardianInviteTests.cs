using Alba;

using buddy.Features.Guardians;
using buddy.IntegrationTests.Features.Guardians;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Xunit;

namespace buddy.IntegrationTests.Features.Guardians.RevokeGuardianInvite;

[Collection(BuddyApiCollection.Name)]
public sealed class RevokeGuardianInviteTests(BuddyApiFixture fixture)
{
    [Fact]
    [CoversEndpoint("RevokeGuardianInvite")]
    public async Task An_active_guardian_can_revoke_a_pending_invite()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        var (invitee, _, _) = await fixture.CreateAuthenticatedUserAsync();

        var invite = await GuardianTestHelpers.InviteGuardianAsync(fixture, guardianToken, child.Id, invitee.Email, GuardianKind.Parent);

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Delete.Url($"/users/me/children/{child.Id}/guardian-invites/{invite.Id}");
            _.StatusCodeShouldBe(204);
        });

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Get.Url($"/users/me/children/{child.Id}/guardian-invites");
            _.StatusCodeShouldBeOk();
        });

        Assert.Empty(response.ReadAsJson<GuardianInviteResponseDto[]>());
    }

    [Fact]
    public async Task Revoking_an_unknown_invite_id_is_idempotent()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Delete.Url($"/users/me/children/{child.Id}/guardian-invites/{Guid.NewGuid()}");
            _.StatusCodeShouldBe(204);
        });
    }
}
