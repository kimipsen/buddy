using System.Text.RegularExpressions;

using Alba;

using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Xunit;

namespace buddy.IntegrationTests.Features.Users.VerifyEmail;

[Collection(BuddyApiCollection.Name)]
public sealed partial class VerifyEmailTests(BuddyApiFixture fixture)
{
    [Fact]
    [CoversEndpoint("VerifyCurrentEmail")]
    public async Task Verifies_the_email_when_given_the_token_from_the_verification_email()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var newEmail = $"verify-{Guid.NewGuid():N}@buddy.test";

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Patch.Json(new { Email = newEmail }).ToUrl("/users/me/email");
            _.StatusCodeShouldBeOk();
        });

        var verificationToken = await ReadVerificationTokenAsync(newEmail);

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Post.Json(new { Token = verificationToken }).ToUrl("/users/me/email/verify");
            _.StatusCodeShouldBeOk();
        });

        var body = response.ReadAsJson<EmailResponseEnvelope>();
        Assert.True(body.Email.IsVerified);
    }

    [Fact]
    public async Task Rejects_an_invalid_token()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var newEmail = $"verify-{Guid.NewGuid():N}@buddy.test";

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Patch.Json(new { Email = newEmail }).ToUrl("/users/me/email");
            _.StatusCodeShouldBeOk();
        });

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Post.Json(new { Token = "not-the-real-token" }).ToUrl("/users/me/email/verify");
            _.StatusCodeShouldBe(400);
        });
    }

    private async Task<string> ReadVerificationTokenAsync(string emailAddress)
    {
        var messages = await fixture.GetMailpitMessagesToAsync(emailAddress);
        Assert.NotEmpty(messages);

        var text = await fixture.GetMailpitMessageTextAsync(messages[0].GetProperty("ID").GetString()!);
        var match = TokenPattern().Match(text);

        Assert.True(match.Success, $"Could not find a verification token in email body: {text}");
        return match.Groups[1].Value;
    }

    [GeneratedRegex(@"verify-email/(\S+)")]
    private static partial Regex TokenPattern();

    private sealed record EmailResponseEnvelope(EmailResponse Email);

    private sealed record EmailResponse(string Value, bool IsVerified);
}
