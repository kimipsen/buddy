using System.Text.RegularExpressions;

using Alba;

using buddy.Features.Groups;
using buddy.IntegrationTests.Fixtures;

using Xunit;

namespace buddy.IntegrationTests.Features.Groups;

internal static partial class GroupTestHelpers
{
    public static async Task<Guid> CreateGroupAsync(BuddyApiFixture fixture, string ownerToken, string name)
    {
        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {ownerToken}");
            _.Post.Json(new { Name = name }).ToUrl("/groups/");
            _.StatusCodeShouldBeOk();
        });

        return response.ReadAsJson<GroupResponseDto>().Id;
    }

    public static async Task<GroupResponseDto> GetGroupAsync(BuddyApiFixture fixture, string token, Guid groupId)
    {
        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Get.Url($"/groups/{groupId}");
            _.StatusCodeShouldBeOk();
        });

        return response.ReadAsJson<GroupResponseDto>();
    }

    public static async Task<GroupInviteResponseDto> InviteToGroupAsync(BuddyApiFixture fixture, string ownerToken, Guid groupId, string email, GroupRole role)
    {
        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {ownerToken}");
            _.Post.Json(new { Email = email, Role = role }).ToUrl($"/groups/{groupId}/invites");
            _.StatusCodeShouldBeOk();
        });

        return response.ReadAsJson<GroupInviteResponseDto>();
    }

    // Invite + accept in one call, for tests that just need a second member in the group and don't
    // care about the invite flow itself.
    public static async Task AddMemberAsync(BuddyApiFixture fixture, string ownerToken, Guid groupId, string inviteeToken, string inviteeEmail, GroupRole role)
    {
        await InviteToGroupAsync(fixture, ownerToken, groupId, inviteeEmail, role);
        var token = await ReadInviteTokenAsync(fixture, inviteeEmail);

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {inviteeToken}");
            _.Post.Url($"/invites/{token}/accept");
            _.StatusCodeShouldBe(204);
        });
    }

    public static async Task<string> ReadInviteTokenAsync(BuddyApiFixture fixture, string emailAddress)
    {
        var messages = await fixture.GetMailpitMessagesToAsync(emailAddress);
        Assert.NotEmpty(messages);

        var text = await fixture.GetMailpitMessageTextAsync(messages[0].GetProperty("ID").GetString()!);
        var match = InviteTokenPattern().Match(text);

        Assert.True(match.Success, $"Could not find an invite token in email body: {text}");
        return match.Groups[1].Value;
    }

    // Matches the token off the end of SmtpEmailSender's "http://.../invite/{token}" link -- the
    // email body no longer spells out "invite token is:" (see SendGroupInviteEmailAsync).
    [GeneratedRegex(@"/invite/(\S+)")]
    private static partial Regex InviteTokenPattern();
}
