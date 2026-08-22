using Alba;

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
    public async Task Creating_a_child_returns_a_username_and_one_time_password_and_links_the_guardian()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();

        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");

        Assert.Equal("Alex", child.Name.GivenName);
        Assert.NotEqual(Guid.Empty, child.Id);
        Assert.NotEqual(Guid.Empty, child.GuardianLinkId);
        Assert.Equal(GuardianKind.Guardian, child.Kind);
        Assert.False(string.IsNullOrWhiteSpace(child.Username));
        Assert.False(string.IsNullOrWhiteSpace(child.TemporaryPassword));
    }

    [Fact]
    public async Task Requires_authentication()
    {
        await fixture.Host.Scenario(_ =>
        {
            _.Post.Json(new { Name = "No Auth" }).ToUrl("/users/me/children/");
            _.StatusCodeShouldBe(401);
        });
    }
}
