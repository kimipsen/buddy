using buddy.Common;
using buddy.Common.RateLimiting;
using buddy.Features.Guardians;
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
    public async Task Re_inviting_the_same_email_immediately_is_rejected_as_a_conflict_by_the_cooldown()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        var (invitee, _, _) = await fixture.CreateAuthenticatedUserAsync();

        await GuardianTestHelpers.InviteGuardianAsync(fixture, guardianToken, child.Id, invitee.Email, GuardianKind.Parent);

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Post.Json(new { Email = invitee.Email, Kind = GuardianKind.Parent }).ToUrl($"/users/me/children/{child.Id}/guardian-invites");
            _.StatusCodeShouldBe(409);
        });

        Assert.Equal(ResendCooldown.ErrorCode, response.ReadAsJson<ErrorEnvelope>().Code);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-an-email")]
    [InlineData("@buddy.test")]
    [InlineData("someone@")]
    public async Task Rejects_an_empty_or_malformed_email(string email)
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");

        var error = await PostInvalidInviteAsync(guardianToken, child.Id, new { Email = email, Kind = GuardianKind.Parent });

        Assert.Equal("email", Assert.Single(error.Details.Keys));
    }

    [Fact]
    public async Task Rejects_an_email_longer_than_254_characters()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");

        var error = await PostInvalidInviteAsync(guardianToken, child.Id, new { Email = EmailOfLength(255), Kind = GuardianKind.Parent });

        Assert.Equal("email", Assert.Single(error.Details.Keys));
    }

    [Fact]
    public async Task Accepts_an_email_of_254_characters_after_trimming_and_lower_casing()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        var email = EmailOfLength(254);

        var invite = await GuardianTestHelpers.InviteGuardianAsync(fixture, guardianToken, child.Id, $"  {email.ToUpperInvariant()} ", GuardianKind.Parent);

        Assert.Equal(email, invite.Email);
    }

    [Fact]
    public async Task Rejects_an_undefined_kind()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        var (invitee, _, _) = await fixture.CreateAuthenticatedUserAsync();

        // The binder accepts an enum as a number, so an out-of-range one reaches the validator.
        var error = await PostInvalidInviteAsync(guardianToken, child.Id, new { Email = invitee.Email, Kind = 99 });

        Assert.Equal("kind", Assert.Single(error.Details.Keys));
    }

    private async Task<ErrorEnvelope> PostInvalidInviteAsync(string guardianToken, Guid childId, object body)
    {
        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Post.Json(body).ToUrl($"/users/me/children/{childId}/guardian-invites");
            _.StatusCodeShouldBe(400);
        });

        var error = response.ReadAsJson<ErrorEnvelope>();
        Assert.Equal("validation_error", error.Code);
        return error;
    }

    // A unique, lower-case, well-formed address of exactly `length` characters, padded with
    // DNS-sized domain labels so it stays deliverable to Mailpit.
    private static string EmailOfLength(int length)
    {
        var local = $"len-{Guid.NewGuid():N}@";
        const string tail = ".buddy.test";
        var remaining = length - local.Length - tail.Length;
        var labels = new List<string>();

        while (remaining > 60)
        {
            labels.Add(new string('a', 59));
            remaining -= 60;
        }

        labels.Add(new string('a', remaining));

        var email = local + string.Join(".", labels) + tail;
        Assert.Equal(length, email.Length);
        return email;
    }
}
