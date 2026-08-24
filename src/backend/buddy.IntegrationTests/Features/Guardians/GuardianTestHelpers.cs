using System.Text.RegularExpressions;

using Alba;

using buddy.Features.Guardians;
using buddy.IntegrationTests.Fixtures;

using Xunit;

namespace buddy.IntegrationTests.Features.Guardians;

internal static partial class GuardianTestHelpers
{
    public static async Task<ChildResponseDto> CreateChildAsync(BuddyApiFixture fixture, string guardianToken, string givenName = "Alex", string familyName = "Anderson", string? username = null)
    {
        username ??= $"child.{Guid.CreateVersion7():N}";
        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Post.Json(new { GivenName = givenName, FamilyName = familyName, Username = username }).ToUrl("/users/me/children/");
            _.StatusCodeShouldBeOk();
        });

        return response.ReadAsJson<ChildResponseDto>();
    }

    // A freshly-provisioned child has a temporary password with a pending required action, which
    // blocks direct-grant login -- this completes onboarding (as the child's own device would on
    // first login) and returns a real access token for them.
    public static async Task<string> CompleteChildLoginAsync(BuddyApiFixture fixture, ChildResponseDto child, CancellationToken cancellationToken = default)
    {
        var permanentPassword = $"child-pw-{child.Id:N}";
        await fixture.SetPermanentPasswordAsync(child.Username, permanentPassword, cancellationToken);

        return await fixture.GetAccessTokenAsync(child.Username, permanentPassword);
    }

    public static async Task<GuardianInviteResponseDto> InviteGuardianAsync(BuddyApiFixture fixture, string guardianToken, Guid childId, string email, GuardianKind kind)
    {
        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Post.Json(new { Email = email, Kind = kind }).ToUrl($"/users/me/children/{childId}/guardian-invites");
            _.StatusCodeShouldBeOk();
        });

        return response.ReadAsJson<GuardianInviteResponseDto>();
    }

    public static async Task<string> ReadGuardianInviteTokenAsync(BuddyApiFixture fixture, string emailAddress)
    {
        var messages = await fixture.GetMailpitMessagesToAsync(emailAddress);
        Assert.NotEmpty(messages);

        var text = await fixture.GetMailpitMessageTextAsync(messages[0].GetProperty("ID").GetString()!);
        var match = InviteTokenPattern().Match(text);

        Assert.True(match.Success, $"Could not find a guardian invite token in email body: {text}");
        return match.Groups[1].Value;
    }

    [GeneratedRegex(@"/guardian-invite/(\S+)")]
    private static partial Regex InviteTokenPattern();
}
