using System.Text.RegularExpressions;

using Alba;

using buddy.IntegrationTests.Fixtures;

using Xunit;

namespace buddy.IntegrationTests.Features.Users;

internal static partial class UserTestHelpers
{
    // Changes the caller's email to a fresh, unverified address (PATCH /users/me/email) and
    // returns that address with the token from its verification email. Read the token before
    // anything else is mailed to the address: it is taken from the newest message.
    public static async Task<(string Email, string VerificationToken)> ChangeToUnverifiedEmailAsync(BuddyApiFixture fixture, string token)
    {
        var email = $"unverified-{Guid.NewGuid():N}@buddy.test";

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Patch.Json(new { Email = email }).ToUrl("/users/me/email");
            _.StatusCodeShouldBeOk();
        });

        var messages = await fixture.GetMailpitMessagesToAsync(email);
        Assert.NotEmpty(messages);

        var text = await fixture.GetMailpitMessageTextAsync(messages[0].GetProperty("ID").GetString()!);
        var match = VerificationTokenPattern().Match(text);

        Assert.True(match.Success, $"Could not find a verification token in email body: {text}");
        return (email, match.Groups[1].Value);
    }

    public static async Task VerifyEmailAsync(BuddyApiFixture fixture, string token, string verificationToken)
    {
        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Post.Json(new { Token = verificationToken }).ToUrl("/users/me/email/verify");
            _.StatusCodeShouldBeOk();
        });
    }

    [GeneratedRegex(@"verify-email/(\S+)")]
    private static partial Regex VerificationTokenPattern();
}
