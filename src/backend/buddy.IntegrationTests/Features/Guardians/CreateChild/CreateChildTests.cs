using Alba;

using buddy.Common;
using buddy.Features.Guardians;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Xunit;

namespace buddy.IntegrationTests.Features.Guardians.CreateChild;

[Collection(BuddyApiCollection.Name)]
public sealed class CreateChildTests(BuddyApiFixture fixture)
{
    [Fact]
    [CoversEndpoint("CreateChild")]
    public async Task Creating_a_child_stores_names_and_requested_username_and_links_the_guardian()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();

        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex", "Anderson", "alex.anderson");

        Assert.Equal("Alex", child.Name.GivenName);
        Assert.Equal("Anderson", child.Name.FamilyName);
        Assert.NotEqual(Guid.Empty, child.Id);
        Assert.NotEqual(Guid.Empty, child.GuardianLinkId);
        Assert.Equal(GuardianKind.Guardian, child.Kind);
        Assert.Equal("alex.anderson", child.Username);
        Assert.False(string.IsNullOrWhiteSpace(child.TemporaryPassword));

        var assignedRoles = await fixture.GetAssignedRealmRoleNamesAsync(child.Username);
        Assert.Contains("buddy-child", assignedRoles);
    }

    [Fact]
    public async Task A_child_starts_on_the_guardians_time_zone_and_language()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Patch.Json(new { TimeZoneId = "Europe/Copenhagen" }).ToUrl("/users/me/timezone");
            _.StatusCodeShouldBeOk();
        });

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Patch.Json(new { Language = "da" }).ToUrl("/users/me/language");
            _.StatusCodeShouldBeOk();
        });

        await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");

        var children = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Get.Url("/users/me/children/");
            _.StatusCodeShouldBeOk();
        });

        var child = Assert.Single(children.ReadAsJson<ChildSummaryDto[]>());
        Assert.Equal("Europe/Copenhagen", child.TimeZoneId);
        Assert.Equal("da", child.Language);
    }

    [Fact]
    public async Task Rejects_a_username_that_is_already_in_use()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        const string username = "duplicate-child-username";
        await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, username: username);

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Post.Json(new { GivenName = "Another", FamilyName = "Child", Username = username })
                .ToUrl("/users/me/children/");
            _.StatusCodeShouldBe(409);
        });

        Assert.Equal(CreateChildOutcome.UsernameUnavailableCode, response.ReadAsJson<ErrorEnvelope>().Code);
    }

    [Fact]
    public async Task Rejects_a_blank_given_name()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Post.Json(new { GivenName = "   ", FamilyName = "Child", Username = "blank-given-name" })
                .ToUrl("/users/me/children/");
            _.StatusCodeShouldBe(400);
        });

        var error = response.ReadAsJson<ErrorEnvelope>();
        Assert.Equal("validation_error", error.Code);
        Assert.Contains("GivenName", error.Details.Keys);
    }

    [Theory]
    [InlineData("GivenName", "", "Child", "blank-given")]
    [InlineData("FamilyName", "Child", "", "blank-family")]
    [InlineData("FamilyName", "Child", "   ", "whitespace-family")]
    [InlineData("Username", "Child", "Child", "")]
    [InlineData("Username", "Child", "Child", "   ")]
    public async Task Rejects_a_blank_name_or_username_naming_the_field(string field, string givenName, string familyName, string username)
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();

        var error = await PostInvalidChildAsync(guardianToken, givenName, familyName, username);

        Assert.Equal(field, Assert.Single(error.Details.Keys));
    }

    [Theory]
    [InlineData("GivenName")]
    [InlineData("FamilyName")]
    [InlineData("Username")]
    public async Task Rejects_a_name_or_username_longer_than_200_characters_naming_the_field(string field)
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var tooLong = new string('a', 201);

        var error = await PostInvalidChildAsync(
            guardianToken,
            field == "GivenName" ? tooLong : "Child",
            field == "FamilyName" ? tooLong : "Child",
            field == "Username" ? tooLong : $"too-long-{Guid.CreateVersion7():N}");

        Assert.Equal(field, Assert.Single(error.Details.Keys));
    }

    [Fact]
    public async Task Accepts_names_and_a_username_of_exactly_200_characters()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var givenName = new string('g', 200);
        var familyName = new string('f', 200);
        var username = $"max-{Guid.CreateVersion7():N}".PadRight(200, 'u');

        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, givenName, familyName, username);

        Assert.Equal(givenName, child.Name.GivenName);
        Assert.Equal(familyName, child.Name.FamilyName);
        Assert.Equal(username, child.Username);
    }

    private async Task<ErrorEnvelope> PostInvalidChildAsync(string guardianToken, string givenName, string familyName, string username)
    {
        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Post.Json(new { GivenName = givenName, FamilyName = familyName, Username = username })
                .ToUrl("/users/me/children/");
            _.StatusCodeShouldBe(400);
        });

        var error = response.ReadAsJson<ErrorEnvelope>();
        Assert.Equal("validation_error", error.Code);
        return error;
    }

    [Fact]
    public async Task Requires_authentication()
    {
        await fixture.Host.Scenario(_ =>
        {
            _.Post.Json(new { GivenName = "No", FamilyName = "Auth", Username = "no-auth" }).ToUrl("/users/me/children/");
            _.StatusCodeShouldBe(401);
        });
    }
}
