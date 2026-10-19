using buddy.Common;
using buddy.Common.RateLimiting;
using buddy.Features.Groups;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Xunit;

namespace buddy.IntegrationTests.Features.Groups.InviteToGroup;

[Collection(BuddyApiCollection.Name)]
public sealed class InviteToGroupTests(BuddyApiFixture fixture)
{
    [Fact]
    [CoversEndpoint("InviteToGroup")]
    public async Task The_owner_can_invite_a_guardian_by_email()
    {
        var (_, ownerToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var groupId = await GroupTestHelpers.CreateGroupAsync(fixture, ownerToken, "Team");
        var (invitee, _, _) = await fixture.CreateAuthenticatedUserAsync();

        var invite = await GroupTestHelpers.InviteToGroupAsync(fixture, ownerToken, groupId, invitee.Email, GroupRole.Member);

        Assert.Equal(invitee.Email.ToLowerInvariant(), invite.Email);
        Assert.Equal(GroupRole.Member, invite.Role);
    }

    [Fact]
    public async Task The_response_carries_the_emailed_link_so_the_inviter_can_share_it_themself()
    {
        var (_, ownerToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var groupId = await GroupTestHelpers.CreateGroupAsync(fixture, ownerToken, "Team");
        var (invitee, inviteeToken, _) = await fixture.CreateAuthenticatedUserAsync();

        var invite = await GroupTestHelpers.InviteToGroupAsync(fixture, ownerToken, groupId, invitee.Email, GroupRole.Member);
        var emailedToken = await GroupTestHelpers.ReadInviteTokenAsync(fixture, invitee.Email);

        Assert.EndsWith($"/invite/{emailedToken}", invite.InviteUrl);

        // The shared link works on its own: accepting with the token taken from it joins the group.
        var sharedToken = invite.InviteUrl[(invite.InviteUrl.LastIndexOf('/') + 1)..];

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {inviteeToken}");
            _.Post.Url($"/invites/{sharedToken}/accept");
            _.StatusCodeShouldBe(204);
        });
    }

    [Fact]
    public async Task Inviting_as_owner_is_rejected()
    {
        var (_, ownerToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var groupId = await GroupTestHelpers.CreateGroupAsync(fixture, ownerToken, "Team");
        var (invitee, _, _) = await fixture.CreateAuthenticatedUserAsync();

        var error = await PostInvalidInviteAsync(ownerToken, groupId, new { Email = invitee.Email, Role = GroupRole.Owner });

        Assert.Equal("role", Assert.Single(error.Details.Keys));
    }

    [Fact]
    public async Task Rejects_an_undefined_role()
    {
        var (_, ownerToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var groupId = await GroupTestHelpers.CreateGroupAsync(fixture, ownerToken, "Team");
        var (invitee, _, _) = await fixture.CreateAuthenticatedUserAsync();

        // The binder accepts an enum as a number, so an out-of-range one reaches the validator.
        var error = await PostInvalidInviteAsync(ownerToken, groupId, new { Email = invitee.Email, Role = 99 });

        Assert.Equal("role", Assert.Single(error.Details.Keys));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-an-email")]
    [InlineData("@buddy.test")]
    [InlineData("someone@")]
    public async Task Rejects_an_empty_or_malformed_email(string email)
    {
        var (_, ownerToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var groupId = await GroupTestHelpers.CreateGroupAsync(fixture, ownerToken, "Team");

        var error = await PostInvalidInviteAsync(ownerToken, groupId, new { Email = email, Role = GroupRole.Member });

        Assert.Equal("email", Assert.Single(error.Details.Keys));
    }

    [Fact]
    public async Task Rejects_an_email_longer_than_254_characters()
    {
        var (_, ownerToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var groupId = await GroupTestHelpers.CreateGroupAsync(fixture, ownerToken, "Team");

        var error = await PostInvalidInviteAsync(ownerToken, groupId, new { Email = EmailOfLength(255), Role = GroupRole.Member });

        Assert.Equal("email", Assert.Single(error.Details.Keys));
    }

    [Fact]
    public async Task Accepts_an_email_of_254_characters_after_trimming_and_lower_casing()
    {
        var (_, ownerToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var groupId = await GroupTestHelpers.CreateGroupAsync(fixture, ownerToken, "Team");
        var email = EmailOfLength(254);

        var invite = await GroupTestHelpers.InviteToGroupAsync(fixture, ownerToken, groupId, $"  {email.ToUpperInvariant()} ", GroupRole.Member);

        Assert.Equal(email, invite.Email);
    }

    [Fact]
    public async Task A_plain_member_cannot_invite_others()
    {
        var (_, ownerToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var groupId = await GroupTestHelpers.CreateGroupAsync(fixture, ownerToken, "Team");

        var (_, plainMemberToken, plainMemberId) = await fixture.CreateAuthenticatedUserAsync();

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {ownerToken}");
            _.Put.Json(new { Role = GroupRole.Member }).ToUrl($"/groups/{groupId}/members/{plainMemberId}");
            _.StatusCodeShouldBe(204);
        });

        var (invitee, _, _) = await fixture.CreateAuthenticatedUserAsync();

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {plainMemberToken}");
            _.Post.Json(new { Email = invitee.Email, Role = GroupRole.Member }).ToUrl($"/groups/{groupId}/invites");
            _.StatusCodeShouldBe(403);
        });
    }

    [Fact]
    public async Task A_non_member_gets_not_found()
    {
        var (_, ownerToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var groupId = await GroupTestHelpers.CreateGroupAsync(fixture, ownerToken, "Team");

        var (_, outsiderToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var (invitee, _, _) = await fixture.CreateAuthenticatedUserAsync();

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {outsiderToken}");
            _.Post.Json(new { Email = invitee.Email, Role = GroupRole.Member }).ToUrl($"/groups/{groupId}/invites");
            _.StatusCodeShouldBe(404);
        });
    }

    [Fact]
    public async Task Re_inviting_the_same_email_immediately_is_rejected_as_a_conflict_by_the_cooldown()
    {
        var (_, ownerToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var groupId = await GroupTestHelpers.CreateGroupAsync(fixture, ownerToken, "Team");
        var (invitee, _, _) = await fixture.CreateAuthenticatedUserAsync();

        await GroupTestHelpers.InviteToGroupAsync(fixture, ownerToken, groupId, invitee.Email, GroupRole.Member);

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {ownerToken}");
            _.Post.Json(new { Email = invitee.Email, Role = GroupRole.Member }).ToUrl($"/groups/{groupId}/invites");
            _.StatusCodeShouldBe(409);
        });

        Assert.Equal(ResendCooldown.ErrorCode, response.ReadAsJson<ErrorEnvelope>().Code);
    }

    private async Task<ErrorEnvelope> PostInvalidInviteAsync(string ownerToken, Guid groupId, object body)
    {
        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {ownerToken}");
            _.Post.Json(body).ToUrl($"/groups/{groupId}/invites");
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
