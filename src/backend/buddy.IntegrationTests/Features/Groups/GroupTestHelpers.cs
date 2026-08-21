using Alba;

using buddy.IntegrationTests.Fixtures;

namespace buddy.IntegrationTests.Features.Groups;

internal static class GroupTestHelpers
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
}
