using buddy.IntegrationTests.Features.WorkLocations;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Xunit;

namespace buddy.IntegrationTests.Features.Guardians.ResetChildPassword;

[Collection(BuddyApiCollection.Name)]
public sealed class ResetChildPasswordTests(BuddyApiFixture fixture)
{
    // Keycloak's direct-grant errors: a wrong password vs. the right one-time password, whose
    // UPDATE_PASSWORD required action blocks a direct grant until the child sets their own.
    private const string WrongPassword = "Invalid user credentials";
    private const string OneTimePassword = "Account is not fully set up";

    [Fact]
    [CoversEndpoint("ResetChildPassword")]
    public async Task A_guardian_gets_a_new_one_time_password_and_the_old_one_stops_working()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token);
        var oldPassword = $"child-pw-{child.Id:N}";
        await GuardianTestHelpers.CompleteChildLoginAsync(fixture, child);

        var reset = await ResetAsync(token, child.Id, expectedStatus: 200);

        Assert.Equal(child.Username, reset.Username);
        Assert.False(string.IsNullOrWhiteSpace(reset.TemporaryPassword));
        Assert.NotEqual(child.TemporaryPassword, reset.TemporaryPassword);
        Assert.Equal(WrongPassword, await fixture.GetPasswordGrantErrorAsync(child.Username, oldPassword));
        Assert.Equal(OneTimePassword, await fixture.GetPasswordGrantErrorAsync(child.Username, reset.TemporaryPassword));
    }

    [Fact]
    public async Task Each_reset_replaces_the_previous_one_time_password()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token);

        var first = await ResetAsync(token, child.Id, expectedStatus: 200);
        var second = await ResetAsync(token, child.Id, expectedStatus: 200);

        Assert.NotEqual(first.TemporaryPassword, second.TemporaryPassword);
        Assert.Equal(WrongPassword, await fixture.GetPasswordGrantErrorAsync(child.Username, child.TemporaryPassword));
        Assert.Equal(WrongPassword, await fixture.GetPasswordGrantErrorAsync(child.Username, first.TemporaryPassword));
        Assert.Equal(OneTimePassword, await fixture.GetPasswordGrantErrorAsync(child.Username, second.TemporaryPassword));
    }

    [Fact]
    public async Task A_co_guardian_can_reset_the_password_too()
    {
        var family = await WorkLocationTestHelpers.CreateCoGuardiansAsync(fixture);

        var reset = await ResetAsync(family.SecondToken, family.Child.Id, expectedStatus: 200);

        Assert.Equal(OneTimePassword, await fixture.GetPasswordGrantErrorAsync(family.Child.Username, reset.TemporaryPassword));
    }

    [Fact]
    public async Task Someone_elses_child_is_not_found_and_keeps_its_password()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken);
        var (_, outsiderToken, _) = await fixture.CreateAuthenticatedUserAsync();

        await ResetAsync(outsiderToken, child.Id, expectedStatus: 404);

        Assert.Equal(OneTimePassword, await fixture.GetPasswordGrantErrorAsync(child.Username, child.TemporaryPassword));
    }

    [Fact]
    public async Task A_child_cannot_reset_its_own_password()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken);
        var childToken = await GuardianTestHelpers.CompleteChildLoginAsync(fixture, child);

        await ResetAsync(childToken, child.Id, expectedStatus: 404);
    }

    [Fact]
    public async Task An_unknown_child_is_not_found()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();

        await ResetAsync(token, Guid.CreateVersion7(), expectedStatus: 404);
    }

    private async Task<ChildPasswordResetDto> ResetAsync(string token, Guid childId, int expectedStatus)
    {
        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Post.Url($"/users/me/children/{childId}/password-reset");
            _.StatusCodeShouldBe(expectedStatus);
        });

        return expectedStatus == 200 ? response.ReadAsJson<ChildPasswordResetDto>() : null!;
    }
}
