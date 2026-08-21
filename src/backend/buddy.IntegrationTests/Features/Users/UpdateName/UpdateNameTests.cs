using Alba;

using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Xunit;

namespace buddy.IntegrationTests.Features.Users.UpdateName;

[Collection(BuddyApiCollection.Name)]
public sealed class UpdateNameTests(BuddyApiFixture fixture)
{
    [Fact]
    [CoversEndpoint("UpdateCurrentName")]
    public async Task Updates_the_calling_users_name()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Patch.Json(new { GivenName = "Updated", FamilyName = "Person" }).ToUrl("/users/me/name");
            _.StatusCodeShouldBeOk();
        });

        var body = response.ReadAsJson<NameResponseEnvelope>();

        Assert.Equal("Updated", body.Name.GivenName);
        Assert.Equal("Person", body.Name.FamilyName);
    }

    [Fact]
    public async Task Returns_not_found_when_the_caller_has_no_buddy_user_yet()
    {
        var user = await fixture.CreateUserAsync();
        var token = await fixture.GetAccessTokenAsync(user);

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Patch.Json(new { GivenName = "Nope", FamilyName = "Nobody" }).ToUrl("/users/me/name");
            _.StatusCodeShouldBe(404);
        });
    }

    private sealed record NameResponseEnvelope(NameResponse Name);

    private sealed record NameResponse(string GivenName, string FamilyName);
}
