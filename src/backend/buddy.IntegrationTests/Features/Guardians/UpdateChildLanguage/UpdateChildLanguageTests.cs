using Alba;

using buddy.IntegrationTests.Features.Guardians;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Xunit;

namespace buddy.IntegrationTests.Features.Guardians.UpdateChildLanguage;

[Collection(BuddyApiCollection.Name)]
public sealed class UpdateChildLanguageTests(BuddyApiFixture fixture)
{
    [Fact]
    [CoversEndpoint("UpdateChildLanguage")]
    public async Task Updates_the_childs_language()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Patch.Json(new { Language = "da" }).ToUrl($"/users/me/children/{child.Id}/language");
            _.StatusCodeShouldBeOk();
        });

        Assert.Equal("da", response.ReadAsJson<ChildSummaryDto>().Language);

        var listResponse = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Get.Url("/users/me/children/");
            _.StatusCodeShouldBeOk();
        });

        Assert.Equal("da", Assert.Single(listResponse.ReadAsJson<ChildSummaryDto[]>()).Language);
    }

    [Fact]
    public async Task Rejects_an_unsupported_language()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Patch.Json(new { Language = "fr" }).ToUrl($"/users/me/children/{child.Id}/language");
            _.StatusCodeShouldBe(400);
        });
    }

    [Fact]
    public async Task Returns_not_found_for_a_child_the_caller_does_not_guard()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var (_, otherGuardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var otherChild = await GuardianTestHelpers.CreateChildAsync(fixture, otherGuardianToken, "Sam");

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Patch.Json(new { Language = "da" }).ToUrl($"/users/me/children/{otherChild.Id}/language");
            _.StatusCodeShouldBe(404);
        });
    }

    [Fact]
    public async Task Requires_authentication()
    {
        await fixture.Host.Scenario(_ =>
        {
            _.Patch.Json(new { Language = "da" }).ToUrl($"/users/me/children/{Guid.NewGuid()}/language");
            _.StatusCodeShouldBe(401);
        });
    }
}
