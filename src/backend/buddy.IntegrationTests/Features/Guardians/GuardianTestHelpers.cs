using Alba;

using buddy.IntegrationTests.Fixtures;

namespace buddy.IntegrationTests.Features.Guardians;

internal static class GuardianTestHelpers
{
    public static async Task<ChildResponseDto> CreateChildAsync(BuddyApiFixture fixture, string guardianToken, string name = "Alex")
    {
        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Post.Json(new { Name = name }).ToUrl("/users/me/children/");
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
}
