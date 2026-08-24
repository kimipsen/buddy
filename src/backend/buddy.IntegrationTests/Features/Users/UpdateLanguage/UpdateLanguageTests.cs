using Alba;

using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Xunit;

namespace buddy.IntegrationTests.Features.Users.UpdateLanguage;

[Collection(BuddyApiCollection.Name)]
public sealed class UpdateLanguageTests(BuddyApiFixture fixture)
{
    [Fact]
    [CoversEndpoint("UpdateCurrentLanguage")]
    public async Task Updates_the_calling_users_language()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Patch.Json(new { Language = "da" }).ToUrl("/users/me/language");
            _.StatusCodeShouldBeOk();
        });

        var body = response.ReadAsJson<LanguageResponseEnvelope>();

        Assert.Equal("da", body.Language);
    }

    [Fact]
    public async Task Rejects_an_unsupported_language()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Patch.Json(new { Language = "fr" }).ToUrl("/users/me/language");
            _.StatusCodeShouldBe(400);
        });
    }

    [Fact]
    public async Task Returns_not_found_when_the_caller_has_no_buddy_user_yet()
    {
        var user = await fixture.CreateUserAsync();
        var token = await fixture.GetAccessTokenAsync(user);

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Patch.Json(new { Language = "da" }).ToUrl("/users/me/language");
            _.StatusCodeShouldBe(404);
        });
    }

    private sealed record LanguageResponseEnvelope(string Language);
}
