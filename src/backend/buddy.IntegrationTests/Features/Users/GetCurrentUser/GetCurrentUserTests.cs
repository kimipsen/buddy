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

    [Fact]
    public async Task Defaults_a_new_users_language_from_the_accept_language_header()
    {
        var user = await fixture.CreateUserAsync();
        var token = await fixture.GetAccessTokenAsync(user);

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.WithRequestHeader("Accept-Language", "da-DK,da;q=0.9,en;q=0.8");
            _.Get.Url("/users/me");
            _.StatusCodeShouldBeOk();
        });

        var body = response.ReadAsJson<UserProfileResponse>();

        Assert.Equal("da", body.Language);
    }

    [Fact]
    public async Task Defaults_a_new_users_language_to_english_when_the_accept_language_header_has_no_supported_match()
    {
        var user = await fixture.CreateUserAsync();
        var token = await fixture.GetAccessTokenAsync(user);

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.WithRequestHeader("Accept-Language", "fr-FR,fr;q=0.9");
            _.Get.Url("/users/me");
            _.StatusCodeShouldBeOk();
        });

        var body = response.ReadAsJson<UserProfileResponse>();

        Assert.Equal("en", body.Language);
    }

    private sealed record UserProfileResponse(Guid Id, EmailResponse Email, string? UserName, NameResponse Name, string Language);

    private sealed record EmailResponse(string Value, bool IsVerified);

    private sealed record NameResponse(string GivenName, string FamilyName);
}
