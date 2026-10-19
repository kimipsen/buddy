using Alba;

using buddy.Common;
using buddy.Features.Users;
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
    public async Task Stores_both_names_trimmed()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Patch.Json(new { GivenName = "  Updated ", FamilyName = " Person  " }).ToUrl("/users/me/name");
            _.StatusCodeShouldBeOk();
        });

        var body = response.ReadAsJson<NameResponseEnvelope>();

        Assert.Equal("Updated", body.Name.GivenName);
        Assert.Equal("Person", body.Name.FamilyName);
    }

    [Theory]
    [InlineData("givenName", "", "Person")]
    [InlineData("givenName", "   ", "Person")]
    [InlineData("familyName", "Updated", "")]
    [InlineData("familyName", "Updated", "   ")]
    public async Task Rejects_a_blank_name_naming_the_field(string field, string givenName, string familyName)
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();

        var error = await PatchInvalidNameAsync(token, givenName, familyName);

        Assert.Equal(field, Assert.Single(error.Details.Keys));
    }

    [Theory]
    [InlineData("givenName")]
    [InlineData("familyName")]
    public async Task Rejects_a_name_longer_than_200_characters_naming_the_field(string field)
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var tooLong = new string('a', 201);

        var error = await PatchInvalidNameAsync(
            token,
            field == "givenName" ? tooLong : "Updated",
            field == "familyName" ? tooLong : "Person");

        Assert.Equal(field, Assert.Single(error.Details.Keys));
    }

    [Fact]
    public async Task Accepts_names_of_exactly_200_characters_after_trimming()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var givenName = new string('g', 200);
        var familyName = new string('f', 200);

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Patch.Json(new { GivenName = $" {givenName} ", FamilyName = familyName }).ToUrl("/users/me/name");
            _.StatusCodeShouldBeOk();
        });

        var body = response.ReadAsJson<NameResponseEnvelope>();

        Assert.Equal(givenName, body.Name.GivenName);
        Assert.Equal(familyName, body.Name.FamilyName);
    }

    private async Task<ErrorEnvelope> PatchInvalidNameAsync(string token, string givenName, string familyName)
    {
        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Patch.Json(new { GivenName = givenName, FamilyName = familyName }).ToUrl("/users/me/name");
            _.StatusCodeShouldBe(400);
        });

        var error = response.ReadAsJson<ErrorEnvelope>();
        Assert.Equal("validation_error", error.Code);
        return error;
    }

    [Fact]
    public async Task Is_forbidden_when_the_caller_has_no_buddy_user_yet()
    {
        var user = await fixture.CreateUserAsync();
        var token = await fixture.GetAccessTokenAsync(user);

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Patch.Json(new { GivenName = "Nope", FamilyName = "Nobody" }).ToUrl("/users/me/name");
            _.StatusCodeShouldBe(403);
        });

        Assert.Equal(ProvisionedUserMiddleware.ErrorCode, response.ReadAsJson<ErrorEnvelope>().Code);
    }

    private sealed record NameResponseEnvelope(NameResponse Name);

    private sealed record NameResponse(string GivenName, string FamilyName);
}
