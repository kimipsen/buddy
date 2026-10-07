using Alba;

using buddy.Common;
using buddy.IntegrationTests.Features.Mealplans;
using buddy.IntegrationTests.Features.Privacy;
using buddy.IntegrationTests.Features.SleepDiaries;
using buddy.IntegrationTests.Features.WorkLocations;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Xunit;

namespace buddy.IntegrationTests.Features.Guardians.DeleteChild;

[Collection(BuddyApiCollection.Name)]
public sealed class DeleteChildTests(BuddyApiFixture fixture)
{
    [Fact]
    [CoversEndpoint("DeleteChild")]
    public async Task The_only_guardian_can_delete_a_child_and_all_its_data()
    {
        var n = Guid.NewGuid().ToString("N")[..12];
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token, $"Doomed{n}", $"Child{n}", $"child.{n}");
        await SleepDiaryTestHelpers.LogAsync(fixture, token, child.Id, new DateOnly(2026, 3, 2), SleepDiaryTestHelpers.FullNight($"remark{n}"));

        await DeleteChildAsync(token, child.Id, expectedStatus: 204);

        var children = await ListChildrenAsync(token);
        Assert.DoesNotContain(child.Id.ToString(), children);
        Assert.False(await fixture.KeycloakUserExistsAsync(child.Username));
        Assert.Empty(await PersonalDataScanner.FindAsync(fixture, $"Doomed{n}", $"Child{n}", child.Username, $"remark{n}"));

        // Gone: a second delete finds nothing.
        await DeleteChildAsync(token, child.Id, expectedStatus: 404);
    }

    [Fact]
    public async Task A_child_with_another_guardian_is_not_deleted()
    {
        var family = await WorkLocationTestHelpers.CreateCoGuardiansAsync(fixture);

        var response = await DeleteChildAsync(family.FirstToken, family.Child.Id, expectedStatus: 409);

        Assert.Equal(buddy.Features.Guardians.DeleteChild.HasOtherGuardiansCode, response.ReadAsJson<ErrorEnvelope>().Code);
        Assert.Contains(family.Child.Id.ToString(), await ListChildrenAsync(family.FirstToken));
        Assert.True(await fixture.KeycloakUserExistsAsync(family.Child.Username));
    }

    [Fact]
    public async Task Someone_elses_child_is_not_found()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken);
        var (_, outsiderToken, _) = await fixture.CreateAuthenticatedUserAsync();

        await DeleteChildAsync(outsiderToken, child.Id, expectedStatus: 404);

        Assert.True(await fixture.KeycloakUserExistsAsync(child.Username));
    }

    [Fact]
    public async Task Family_meals_anchored_to_the_child_stay_with_its_sibling()
    {
        var n = Guid.NewGuid().ToString("N")[..12];
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var deleted = await GuardianTestHelpers.CreateChildAsync(fixture, token, "First", "Child");
        var sibling = await GuardianTestHelpers.CreateChildAsync(fixture, token, "Second", "Child");
        await MealplanTestHelpers.CreateMealAsync(fixture, token, deleted.Id, new CreateMealOptions(Name: $"Kept{n}"));

        await DeleteChildAsync(token, deleted.Id, expectedStatus: 204);

        var meals = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Get.Url($"/mealplans/children/{sibling.Id}/meals");
            _.StatusCodeShouldBe(200);
        });
        Assert.Contains($"Kept{n}", await meals.ReadAsTextAsync());
    }

    private Task<IScenarioResult> DeleteChildAsync(string token, Guid childId, int expectedStatus) =>
        fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Delete.Url($"/users/me/children/{childId}");
            _.StatusCodeShouldBe(expectedStatus);
        });

    private async Task<string> ListChildrenAsync(string token)
    {
        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Get.Url("/users/me/children");
            _.StatusCodeShouldBe(200);
        });

        return await response.ReadAsTextAsync();
    }
}
