using Alba;

using buddy.Features.Guardians;
using buddy.IntegrationTests.Features.Guardians;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Xunit;

namespace buddy.IntegrationTests.Features.Guardians.InviteGuardian;

[Collection(BuddyApiCollection.Name)]
public sealed class InviteGuardianTests(BuddyApiFixture fixture)
{
    [Fact]
    [CoversEndpoint("InviteGuardian")]
    public async Task An_active_guardian_can_invite_a_co_guardian_by_email()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        var (invitee, _, _) = await fixture.CreateAuthenticatedUserAsync();

        var invite = await GuardianTestHelpers.InviteGuardianAsync(fixture, guardianToken, child.Id, invitee.Email, GuardianKind.Parent);

        Assert.Equal(invitee.Email.ToLowerInvariant(), invite.Email);
        Assert.Equal(GuardianKind.Parent, invite.Kind);
    }

    [Fact]
    public async Task A_caller_with_no_guardian_link_to_the_child_gets_not_found()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");

        var (_, outsiderToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var (invitee, _, _) = await fixture.CreateAuthenticatedUserAsync();

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {outsiderToken}");
            _.Post.Json(new { Email = invitee.Email, Kind = GuardianKind.Guardian }).ToUrl($"/users/me/children/{child.Id}/guardian-invites");
            _.StatusCodeShouldBe(404);
        });
    }

    [Fact]
    public async Task Re_inviting_the_same_email_immediately_is_rejected_by_the_cooldown()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        var (invitee, _, _) = await fixture.CreateAuthenticatedUserAsync();

        await GuardianTestHelpers.InviteGuardianAsync(fixture, guardianToken, child.Id, invitee.Email, GuardianKind.Parent);

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Post.Json(new { Email = invitee.Email, Kind = GuardianKind.Parent }).ToUrl($"/users/me/children/{child.Id}/guardian-invites");
            _.StatusCodeShouldBe(400);
        });
    }
}
