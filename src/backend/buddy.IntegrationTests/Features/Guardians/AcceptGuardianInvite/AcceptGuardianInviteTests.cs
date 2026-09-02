using Alba;

using buddy.Features.Guardians;
using buddy.IntegrationTests.Features.Guardians;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Xunit;

namespace buddy.IntegrationTests.Features.Guardians.AcceptGuardianInvite;

[Collection(BuddyApiCollection.Name)]
public sealed class AcceptGuardianInviteTests(BuddyApiFixture fixture)
{
    [Fact]
    [CoversEndpoint("AcceptGuardianInvite")]
    public async Task The_invited_user_can_accept_and_becomes_an_active_guardian()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        var (invitee, inviteeToken, _) = await fixture.CreateAuthenticatedUserAsync();

        await GuardianTestHelpers.InviteGuardianAsync(fixture, guardianToken, child.Id, invitee.Email, GuardianKind.Parent);
        var token = await GuardianTestHelpers.ReadGuardianInviteTokenAsync(fixture, invitee.Email);

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {inviteeToken}");
            _.Post.Url($"/guardian-invites/{token}/accept");
            _.StatusCodeShouldBe(204);
        });

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {inviteeToken}");
            _.Get.Url("/users/me/children/");
            _.StatusCodeShouldBeOk();
        });

        var linkedChild = Assert.Single(response.ReadAsJson<ChildSummaryDto[]>(), c => c.Id == child.Id);
        Assert.Equal(GuardianKind.Parent, linkedChild.Kind);
    }

    [Fact]
    public async Task Accepting_an_already_accepted_invite_is_an_idempotent_success()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        var (invitee, inviteeToken, _) = await fixture.CreateAuthenticatedUserAsync();

        await GuardianTestHelpers.InviteGuardianAsync(fixture, guardianToken, child.Id, invitee.Email, GuardianKind.Parent);
        var token = await GuardianTestHelpers.ReadGuardianInviteTokenAsync(fixture, invitee.Email);

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {inviteeToken}");
            _.Post.Url($"/guardian-invites/{token}/accept");
            _.StatusCodeShouldBe(204);
        });

        // A client retry of the exact same accept (e.g. after a dropped response) must not read
        // as failure just because the invite is no longer Pending.
        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {inviteeToken}");
            _.Post.Url($"/guardian-invites/{token}/accept");
            _.StatusCodeShouldBe(204);
        });
    }

    [Fact]
    public async Task A_different_logged_in_user_cannot_accept_someone_elses_invite()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        var (invitee, _, _) = await fixture.CreateAuthenticatedUserAsync();

        await GuardianTestHelpers.InviteGuardianAsync(fixture, guardianToken, child.Id, invitee.Email, GuardianKind.Parent);
        var token = await GuardianTestHelpers.ReadGuardianInviteTokenAsync(fixture, invitee.Email);

        var (_, someoneElseToken, _) = await fixture.CreateAuthenticatedUserAsync();

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {someoneElseToken}");
            _.Post.Url($"/guardian-invites/{token}/accept");
            _.StatusCodeShouldBe(403);
        });
    }

    [Fact]
    public async Task An_unknown_token_is_not_found()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Post.Url("/guardian-invites/not-a-real-token/accept");
            _.StatusCodeShouldBe(404);
        });
    }

    [Fact]
    public async Task A_revoked_invite_cannot_be_accepted()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        var (invitee, inviteeToken, _) = await fixture.CreateAuthenticatedUserAsync();

        var invite = await GuardianTestHelpers.InviteGuardianAsync(fixture, guardianToken, child.Id, invitee.Email, GuardianKind.Parent);
        var token = await GuardianTestHelpers.ReadGuardianInviteTokenAsync(fixture, invitee.Email);

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Delete.Url($"/users/me/children/{child.Id}/guardian-invites/{invite.Id}");
            _.StatusCodeShouldBe(204);
        });

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {inviteeToken}");
            _.Post.Url($"/guardian-invites/{token}/accept");
            _.StatusCodeShouldBe(404);
        });
    }
}
