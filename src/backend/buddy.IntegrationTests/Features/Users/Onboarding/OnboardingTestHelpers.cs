using Alba;

using buddy.Features.Users;
using buddy.IntegrationTests.Fixtures;

namespace buddy.IntegrationTests.Features.Users.Onboarding;

internal sealed record OnboardingProgressDto(OnboardingStatus Status, Guid? SetupGroupId, bool InvitationsSkipped, int Version);

internal static class OnboardingTestHelpers
{
    public static async Task<OnboardingProgressDto> GetAsync(BuddyApiFixture fixture, string token)
    {
        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Get.Url("/users/me/onboarding");
            _.StatusCodeShouldBeOk();
        });

        return response.ReadAsJson<OnboardingProgressDto>();
    }

    public static async Task<OnboardingProgressDto> PutAsync(
        BuddyApiFixture fixture, string token, OnboardingStatus status, Guid? setupGroupId, bool invitationsSkipped, int version) =>
        (await PutRawAsync(fixture, token, status, setupGroupId, invitationsSkipped, version, expectedStatus: 200))
            .ReadAsJson<OnboardingProgressDto>();

    public static Task<IScenarioResult> PutRawAsync(
        BuddyApiFixture fixture, string token, OnboardingStatus status, Guid? setupGroupId, bool invitationsSkipped, int version, int expectedStatus) =>
        fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Put.Json(new { Status = status, SetupGroupId = setupGroupId, InvitationsSkipped = invitationsSkipped, Version = version })
                .ToUrl("/users/me/onboarding");
            _.StatusCodeShouldBe(expectedStatus);
        });
}
