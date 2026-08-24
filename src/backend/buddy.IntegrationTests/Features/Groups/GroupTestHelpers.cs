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

    public static async Task<string> ReadInviteTokenAsync(BuddyApiFixture fixture, string emailAddress)
    {
        var messages = await fixture.GetMailpitMessagesToAsync(emailAddress);
        Assert.NotEmpty(messages);

        var text = await fixture.GetMailpitMessageTextAsync(messages[0].GetProperty("ID").GetString()!);
        var match = InviteTokenPattern().Match(text);

        Assert.True(match.Success, $"Could not find an invite token in email body: {text}");
        return match.Groups[1].Value;
    }

    [GeneratedRegex(@"invite token is:\s*(\S+)")]
    private static partial Regex InviteTokenPattern();
}
