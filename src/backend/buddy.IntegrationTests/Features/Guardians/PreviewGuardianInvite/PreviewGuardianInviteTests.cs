using Alba;

using buddy.Features.Guardians;
using buddy.IntegrationTests.Features.Guardians;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Xunit;

namespace buddy.IntegrationTests.Features.Guardians.PreviewGuardianInvite;

[Collection(BuddyApiCollection.Name)]
public sealed class PreviewGuardianInviteTests(BuddyApiFixture fixture)
{
    [Fact]
    [CoversEndpoint("PreviewGuardianInvite")]
    public async Task Anyone_can_preview_an_invite_without_logging_in()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        var (invitee, _, _) = await fixture.CreateAuthenticatedUserAsync();

        await GuardianTestHelpers.InviteGuardianAsync(fixture, guardianToken, child.Id, invitee.Email, GuardianKind.Guardian);
        var token = await GuardianTestHelpers.ReadGuardianInviteTokenAsync(fixture, invitee.Email);

        var response = await fixture.Host.Scenario(_ =>
        {
            _.Get.Url($"/guardian-invites/{token}/preview");
            _.StatusCodeShouldBeOk();
        });

        var preview = response.ReadAsJson<GuardianInvitePreviewResponseDto>();
        Assert.Equal("Alex", preview.ChildGivenName);
        Assert.Equal(GuardianKind.Guardian, preview.Kind);
    }

    [Fact]
    public async Task An_unknown_token_is_not_found()
    {
        await fixture.Host.Scenario(_ =>
        {
            _.Get.Url("/guardian-invites/not-a-real-token/preview");
            _.StatusCodeShouldBe(404);
        });
    }
}
