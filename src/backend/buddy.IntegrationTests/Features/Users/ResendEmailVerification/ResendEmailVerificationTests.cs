using Alba;

using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Xunit;

namespace buddy.IntegrationTests.Features.Users.ResendEmailVerification;

[Collection(BuddyApiCollection.Name)]
public sealed class ResendEmailVerificationTests(BuddyApiFixture fixture)
{
    [Fact]
    [CoversEndpoint("ResendCurrentUserEmailVerification")]
    public async Task Already_verified_email_returns_no_content_without_sending_anything()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Post.Url("/users/me/email/verify/resend");
            _.StatusCodeShouldBe(204);
        });
    }

    [Fact]
    public async Task A_request_within_the_cooldown_window_is_rejected_as_a_conflict()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var newEmail = $"resend-{Guid.NewGuid():N}@buddy.test";

        // Changing the email requests a verification itself, so the cooldown is already
        // running by the time this test calls the resend endpoint.
        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Patch.Json(new { Email = newEmail }).ToUrl("/users/me/email");
            _.StatusCodeShouldBeOk();
        });

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Post.Url("/users/me/email/verify/resend");
            _.StatusCodeShouldBe(409);
        });
    }
}
