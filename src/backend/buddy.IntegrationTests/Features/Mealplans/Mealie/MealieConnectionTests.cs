using System.Text.Json;

using Alba;

using buddy.Features.Mealplans;
using buddy.IntegrationTests.Features.Guardians;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Xunit;

namespace buddy.IntegrationTests.Features.Mealplans.Mealie;

// docs/backend/analysis/mealie-integration.md, Questions 2, 3, 7 and 8.
[Collection(BuddyApiCollection.Name)]
public sealed class MealieConnectionTests(BuddyApiFixture fixture)
{
    [Fact]
    [CoversEndpoint("GetMealieConnection")]
    public async Task A_family_that_never_connected_reports_not_connected()
    {
        var family = await MealieTestHost.CreateFamilyAsync(fixture, connect: false);

        var view = await family.GetAsync("mealie");

        Assert.Equal(0, view.GetProperty("kind").GetInt32());
    }

    [Fact]
    [CoversEndpoint("ConnectMealie")]
    public async Task Connecting_stores_the_household_and_never_returns_the_token()
    {
        var family = await MealieTestHost.CreateFamilyAsync(fixture, connect: false);

        var connected = await family.ConnectAsync(family.Server.BaseUrl + "/");

        Assert.Equal(1, connected.GetProperty("kind").GetInt32());
        Assert.Equal(family.Server.BaseUrl, connected.GetProperty("baseUrl").GetString());
        Assert.Equal("Holm", connected.GetProperty("householdName").GetString());
        Assert.Equal("buddy-sync", connected.GetProperty("mealieUserName").GetString());
        Assert.Equal("v3.29.0", connected.GetProperty("mealieVersion").GetString());
        Assert.Equal("1234", connected.GetProperty("tokenLast4").GetString());
        Assert.Equal(family.GuardianId, connected.GetProperty("connectedBy").GetGuid());
        Assert.DoesNotContain(MealieTestHost.Token, connected.GetRawText(), StringComparison.Ordinal);

        var view = await family.GetAsync("mealie");
        Assert.Equal(connected.GetRawText(), view.GetRawText());
    }

    [Theory]
    [InlineData("http://mealie.example.com", "https")]
    [InlineData("https://10.0.0.5", "private network")]
    [InlineData("https://192.168.1.20:9000", "private network")]
    [InlineData("https://localhost:9000", "private network")]
    [InlineData("https://mealie", "private network")]
    [InlineData("https://mealie.example.com/mealie", "without a path")]
    [InlineData("not a url", "starting with https")]
    public async Task An_address_the_outbound_policy_refuses_is_rejected_on_baseUrl(string baseUrl, string messagePart)
    {
        var family = await MealieTestHost.CreateFamilyAsync(fixture, connect: false);

        var problem = await family.ConnectAsync(baseUrl, expectedStatus: 400);

        Assert.Contains(messagePart, problem.GetProperty("details").GetProperty("baseUrl")[0].GetString(), StringComparison.Ordinal);
        Assert.Empty(family.Server.Searches);
    }

    [Fact]
    public async Task A_rejected_token_is_reported_on_apiToken_and_nothing_is_stored()
    {
        var family = await MealieTestHost.CreateFamilyAsync(fixture, connect: false);

        var problem = await family.ConnectAsync(apiToken: "wrong-token-9999", expectedStatus: 400);

        Assert.Contains("rejected", problem.GetProperty("details").GetProperty("apiToken")[0].GetString(), StringComparison.Ordinal);
        Assert.Equal(0, (await family.GetAsync("mealie")).GetProperty("kind").GetInt32());
    }

    [Fact]
    public async Task An_admin_token_is_refused()
    {
        var family = await MealieTestHost.CreateFamilyAsync(fixture, connect: false);
        family.Server.Admin = true;

        var problem = await family.ConnectAsync(expectedStatus: 400);

        Assert.Equal(MealieLiveCheck.AdminRejectedMessage, problem.GetProperty("details").GetProperty("apiToken")[0].GetString());
    }

    [Theory]
    [InlineData("v3.19.2")]
    [InlineData("v2.8.0")]
    public async Task A_Mealie_older_than_the_minimum_version_is_refused(string version)
    {
        var family = await MealieTestHost.CreateFamilyAsync(fixture, connect: false);
        family.Server.Version = version;

        var problem = await family.ConnectAsync(expectedStatus: 400);

        Assert.Contains("3.20.0 or newer", problem.GetProperty("details").GetProperty("baseUrl")[0].GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_unreachable_server_is_refused_on_baseUrl()
    {
        var family = await MealieTestHost.CreateFamilyAsync(fixture, connect: false);
        family.Server.Down = true;

        var problem = await family.ConnectAsync(expectedStatus: 400);

        Assert.Contains("couldn't reach", problem.GetProperty("details").GetProperty("baseUrl")[0].GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Reconnecting_with_a_rotated_token_overwrites_the_connection()
    {
        var family = await MealieTestHost.CreateFamilyAsync(fixture);
        family.Server.ValidTokens.Add("rotated-token-5678");

        var reconnected = await family.ConnectAsync(apiToken: "rotated-token-5678");

        Assert.Equal("5678", reconnected.GetProperty("tokenLast4").GetString());
        Assert.Equal("5678", (await family.GetAsync("mealie")).GetProperty("tokenLast4").GetString());
    }

    [Fact]
    public async Task A_sibling_shares_the_family_connection()
    {
        var family = await MealieTestHost.CreateFamilyAsync(fixture);
        var sibling = await GuardianTestHelpers.CreateChildAsync(fixture, family.Token, "Ida");

        var view = await (family with { Child = sibling }).GetAsync("mealie");

        Assert.Equal(family.Server.BaseUrl, view.GetProperty("baseUrl").GetString());
    }

    [Fact]
    [CoversEndpoint("DisconnectMealie")]
    public async Task Disconnecting_is_idempotent_and_hides_the_connection()
    {
        var family = await MealieTestHost.CreateFamilyAsync(fixture);

        await family.DeleteAsync("mealie");
        await family.DeleteAsync("mealie");

        Assert.Equal(0, (await family.GetAsync("mealie")).GetProperty("kind").GetInt32());
        await family.PostAsync("mealie/test-connection", null, expectedStatus: 409);
    }

    [Fact]
    public async Task Disconnecting_a_family_that_never_connected_succeeds()
    {
        var family = await MealieTestHost.CreateFamilyAsync(fixture, connect: false);

        await family.DeleteAsync("mealie");
    }

    [Fact]
    [CoversEndpoint("TestMealieConnection")]
    public async Task Testing_the_connection_counts_recipes_and_shopping_lists()
    {
        var family = await MealieTestHost.CreateFamilyAsync(fixture);
        family.Server.AddRecipe("Lasagne al forno");
        family.Server.AddRecipe("Chicken tacos");

        var result = await family.PostAsync("mealie/test-connection", null);

        Assert.Equal(0, result.GetProperty("kind").GetInt32());
        Assert.Equal(2, result.GetProperty("recipeCount").GetInt32());
        Assert.Equal(1, result.GetProperty("shoppingListCount").GetInt32());
    }

    [Theory]
    [InlineData(MealieFailureKind.TokenRejected, "mealie_token_rejected")]
    [InlineData(MealieFailureKind.Unreachable, "mealie_unreachable")]
    [InlineData(MealieFailureKind.Error, "mealie_error")]
    public async Task A_failing_server_is_a_failed_test_result_not_an_error(MealieFailureKind kind, string code)
    {
        var family = await MealieTestHost.CreateFamilyAsync(fixture);

        switch (kind)
        {
            case MealieFailureKind.TokenRejected:
                family.Server.ValidTokens.Clear();
                break;
            case MealieFailureKind.Unreachable:
                family.Server.Down = true;
                break;
            default:
                family.Server.FailWith = kind;
                break;
        }

        var result = await family.PostAsync("mealie/test-connection", null);

        Assert.Equal(1, result.GetProperty("kind").GetInt32());
        Assert.Equal(code, result.GetProperty("code").GetString());
    }

    [Fact]
    public async Task A_child_is_forbidden_and_an_unrelated_guardian_gets_not_found()
    {
        var family = await MealieTestHost.CreateFamilyAsync(fixture);
        var childToken = await GuardianTestHelpers.CompleteChildLoginAsync(fixture, family.Child);
        var (_, strangerToken, _) = await fixture.CreateAuthenticatedUserAsync();

        await family.GetAsync("mealie", expectedStatus: 403, token: childToken);
        await family.GetAsync("mealie", expectedStatus: 404, token: strangerToken);
        await family.DeleteAsync("mealie", expectedStatus: 404, token: strangerToken);
        await family.PostAsync("mealie/test-connection", null, expectedStatus: 404, token: strangerToken);
        await family.PutAsync("mealie", new { BaseUrl = family.Server.BaseUrl, ApiToken = MealieTestHost.Token }, expectedStatus: 404, token: strangerToken);
    }

    [Fact]
    public async Task An_empty_token_or_address_is_a_validation_error()
    {
        var family = await MealieTestHost.CreateFamilyAsync(fixture, connect: false);

        var problem = await family.PutAsync("mealie", new { BaseUrl = "", ApiToken = "short" }, expectedStatus: 400);

        Assert.True(problem.GetProperty("details").TryGetProperty("baseUrl", out _));
        Assert.True(problem.GetProperty("details").TryGetProperty("apiToken", out _));
    }

    [Fact]
    public async Task The_personal_data_export_lists_the_connection_without_the_token()
    {
        var family = await MealieTestHost.CreateFamilyAsync(fixture);

        var response = await family.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {family.Token}");
            _.Get.Url("/users/me/export");
            _.StatusCodeShouldBeOk();
        });

        var text = await response.ReadAsTextAsync();
        using var export = JsonDocument.Parse(text);
        var connections = export.RootElement.GetProperty("sections").GetProperty("mealplans").GetProperty("mealieConnections");

        Assert.Equal(family.Server.BaseUrl, connections[0].GetProperty("baseUrl").GetString());
        Assert.DoesNotContain(MealieTestHost.Token, text, StringComparison.Ordinal);
    }
}
