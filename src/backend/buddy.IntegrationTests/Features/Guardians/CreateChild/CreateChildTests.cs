using Alba;

using buddy.Common;
using buddy.Features.Guardians;
using buddy.IntegrationTests.Features.Guardians;
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
    public async Task Rejects_a_username_that_is_already_in_use()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        const string username = "duplicate-child-username";
        await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, username: username);

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Post.Json(new { GivenName = "Another", FamilyName = "Child", Username = username })
                .ToUrl("/users/me/children/");
            _.StatusCodeShouldBe(409);
        });
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
