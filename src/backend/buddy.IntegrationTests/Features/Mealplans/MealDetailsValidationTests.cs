using buddy.Common;
using buddy.IntegrationTests.Features.Guardians;
using buddy.IntegrationTests.Fixtures;

using Xunit;

namespace buddy.IntegrationTests.Features.Mealplans;

// MealRules (name trimming, icon and color) on all four routes that write a meal's details:
// CreateMeal, CreateMealForGroup, UpdateMealDetails and UpdateMealDetailsForGroup.
[Collection(BuddyApiCollection.Name)]
public sealed class MealDetailsValidationTests(BuddyApiFixture fixture)
{
    public static TheoryData<string> Routes => ["CreateMeal", "CreateMealForGroup", "UpdateMealDetails", "UpdateMealDetailsForGroup"];

    [Theory]
    [MemberData(nameof(Routes))]
    public async Task The_name_is_trimmed_before_its_length_check_and_storage(string route)
    {
        var name = new string('a', 200);

        var meal = await SendAsync(route, new { Name = $"  {name} \n", Icon = "🌮", Color = "#ffaa00" }, 200);

        Assert.Equal(name, meal!.Name);
    }

    [Theory]
    [MemberData(nameof(Routes))]
    public async Task A_name_longer_than_200_characters_after_trimming_is_rejected(string route) =>
        await AssertRejectedAsync(route, new { Name = $"  {new string('a', 201)}  ", Icon = "🌮", Color = "#ffaa00" }, "name");

    [Theory]
    [MemberData(nameof(Routes))]
    public async Task A_malformed_pictogram_icon_is_rejected(string route) =>
        await AssertRejectedAsync(route, new { Name = "Tacos", Icon = "pictogram:Not A Slug", Color = "#ffaa00" }, "icon");

    [Theory]
    [MemberData(nameof(Routes))]
    public async Task An_emoji_icon_longer_than_16_characters_is_rejected(string route) =>
        await AssertRejectedAsync(route, new { Name = "Tacos", Icon = new string('x', 17), Color = "#ffaa00" }, "icon");

    [Theory]
    [MemberData(nameof(Routes))]
    public async Task A_pictogram_icon_longer_than_64_characters_is_rejected(string route) =>
        await AssertRejectedAsync(route, new { Name = "Tacos", Icon = "pictogram:" + new string('a', 55), Color = "#ffaa00" }, "icon");

    [Theory]
    [MemberData(nameof(Routes))]
    public async Task A_16_character_emoji_and_a_64_character_pictogram_are_accepted(string route)
    {
        var emoji = await SendAsync(route, new { Name = "Tacos", Icon = new string('x', 16), Color = "#ffaa00" }, 200);
        Assert.Equal(new string('x', 16), emoji!.Icon);

        var pictogram = "pictogram:" + new string('a', 54);
        var withPictogram = await SendAsync(route, new { Name = "Tacos", Icon = pictogram, Color = "#ffaa00" }, 200);
        Assert.Equal(pictogram, withPictogram!.Icon);
    }

    [Theory]
    [MemberData(nameof(Routes))]
    public async Task Icon_and_color_stay_optional(string route)
    {
        var meal = await SendAsync(route, new { Name = "Tacos", Icon = "", Color = "" }, 200);

        Assert.Equal("", meal!.Icon);
        Assert.Equal("", meal.Color);
    }

    [Theory]
    [MemberData(nameof(Routes))]
    public async Task A_color_longer_than_32_characters_after_trimming_is_rejected(string route) =>
        await AssertRejectedAsync(route, new { Name = "Tacos", Icon = "🌮", Color = new string('c', 33) }, "color");

    [Theory]
    [MemberData(nameof(Routes))]
    public async Task A_32_character_color_is_trimmed_and_accepted(string route)
    {
        var color = new string('c', 32);

        var meal = await SendAsync(route, new { Name = "Tacos", Icon = "🌮", Color = $"  {color}  " }, 200);

        Assert.Equal(color, meal!.Color);
    }

    private async Task AssertRejectedAsync(string route, object body, string key)
    {
        var error = await SendAsync<ErrorEnvelope>(route, body, 400);

        Assert.Equal("validation_error", error!.Code);
        Assert.Contains(key, error.Details.Keys);
    }

    private Task<MealDto?> SendAsync(string route, object body, int expectedStatus) => SendAsync<MealDto>(route, body, expectedStatus);

    private async Task<T?> SendAsync<T>(string route, object body, int expectedStatus) where T : class
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        var basePath = route.EndsWith("ForGroup", StringComparison.Ordinal)
            ? $"/mealplans/groups/{await MealplanTestHelpers.ShareWithNewGroupAsync(fixture, guardianToken, child.Id)}"
            : $"/mealplans/children/{child.Id}";
        var isUpdate = route.StartsWith("Update", StringComparison.Ordinal);
        var existing = isUpdate ? await MealplanTestHelpers.CreateMealAsync(fixture, guardianToken, child.Id) : null;

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");

            if (isUpdate)
            {
                _.Patch.Json(body).ToUrl($"{basePath}/meals/{existing!.Id}/details");
            }
            else
            {
                _.Post.Json(body).ToUrl($"{basePath}/meals");
            }

            _.StatusCodeShouldBe(expectedStatus);
        });

        return response.ReadAsJson<T>();
    }
}
