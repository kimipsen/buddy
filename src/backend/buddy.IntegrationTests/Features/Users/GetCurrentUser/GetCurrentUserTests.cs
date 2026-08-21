using Alba;

using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Xunit;

namespace buddy.IntegrationTests.Features.Users.GetCurrentUser;

[Collection(BuddyApiCollection.Name)]
public sealed class GetCurrentUserTests(BuddyApiFixture fixture)
{
    [Fact]
    [CoversEndpoint("GetCurrentUser")]
    public async Task Returns_the_calling_users_profile_created_from_their_keycloak_claims()
    {
        var token = await fixture.GetAccessTokenAsync("alice", "alice-test-pw");

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Get.Url("/users/me");
            _.StatusCodeShouldBeOk();
        });

        var body = response.ReadAsJson<UserProfileResponse>();

        Assert.Equal("alice", body.UserName);
        Assert.Equal("alice@buddy.test", body.Email.Value);
        Assert.Equal("Alice", body.Name.GivenName);
        Assert.Equal("Anderson", body.Name.FamilyName);
    }

    [Fact]
    public async Task Requires_authentication()
    {
        await fixture.Host.Scenario(_ =>
        {
            _.Get.Url("/users/me");
            _.StatusCodeShouldBe(401);
        });
    }

    private sealed record UserProfileResponse(Guid Id, EmailResponse Email, string? UserName, NameResponse Name);

    private sealed record EmailResponse(string Value, bool IsVerified);

    private sealed record NameResponse(string GivenName, string FamilyName);
}
