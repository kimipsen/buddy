using System.Net.Http.Headers;
using System.Text.Json;

using buddy.Features.Mealplans;

using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;

using Xunit;

namespace buddy.IntegrationTests.Features.Mealplans.Mealie;

// Runs the real MealieClient against a real, pinned Mealie (docs/backend/analysis/
// mealie-integration.md, Question 8). Opt-in, because pulling and starting the image is slow:
// `task test:contract:mealie` sets MEALIE_CONTRACT_TESTS=1, and MEALIE_IMAGE picks the version
// (default the newest one Buddy is tested against). Bumping that tag is how a new Mealie release
// gets verified; running it with the minimum version checks the oldest one Buddy accepts.
public sealed class MealieContractTests : IAsyncLifetime
{
    public const string DefaultImage = "ghcr.io/mealie-recipes/mealie:v3.29.0";

    // Mealie's documented first-run admin.
    private const string AdminEmail = "changeme@example.com";
    private const string AdminPassword = "MyPassword";

    private IContainer? _mealie;
    private string _baseUrl = "";
    private string _adminToken = "";
    private string _userToken = "";

    public async Task InitializeAsync()
    {
        if (!MealieContractFactAttribute.Enabled)
        {
            return;
        }

        _mealie = new ContainerBuilder(Environment.GetEnvironmentVariable("MEALIE_IMAGE") is { Length: > 0 } image ? image : DefaultImage)
            .WithEnvironment("ALLOW_SIGNUP", "false")
            .WithPortBinding(9000, true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(r => r.ForPort(9000).ForPath("/api/app/about")))
            .Build();

        await _mealie.StartAsync();
        _baseUrl = $"http://{_mealie.Hostname}:{_mealie.GetMappedPublicPort(9000)}";

        using var http = new HttpClient { BaseAddress = new Uri(_baseUrl) };
        var admin = await LoginAsync(http, AdminEmail, AdminPassword);
        _adminToken = await CreateApiTokenAsync(http, admin);

        await SendAsync(http, admin, HttpMethod.Post, "/api/admin/users", new
        {
            username = "buddysync",
            fullName = "Buddy Sync",
            email = "buddy@example.com",
            password = "BuddySync123!",
            admin = false,
            group = "Home",
            household = "Family"
        });

        var user = await LoginAsync(http, "buddysync", "BuddySync123!");
        _userToken = await CreateApiTokenAsync(http, user);
    }

    public async Task DisposeAsync()
    {
        if (_mealie is not null)
        {
            await _mealie.DisposeAsync();
        }
    }

    [MealieContractFact]
    public async Task Every_call_Buddy_makes_works_against_a_real_Mealie()
    {
        using var http = new HttpClient { BaseAddress = new Uri(_baseUrl) };
        await SendAsync(http, _userToken, HttpMethod.Post, "/api/recipes", new { name = "Lasagne al forno" });
        await SendAsync(http, _userToken, HttpMethod.Patch, "/api/recipes/lasagne-al-forno", new
        {
            description = "Layers",
            recipeIngredient = new[] { new { note = "500 g minced beef", display = "500 g minced beef" } },
            recipeInstructions = new[] { new { text = "Brown the beef." } }
        });
        var recipeId = (await SendAsync(http, _userToken, HttpMethod.Get, "/api/recipes/lasagne-al-forno", null)).GetProperty("id").GetGuid();
        await SendAsync(http, _userToken, HttpMethod.Post, "/api/households/mealplans", new { date = "2031-03-03", entryType = "dinner", recipeId });
        await SendAsync(http, _userToken, HttpMethod.Post, "/api/households/mealplans", new { date = "2031-03-04", entryType = "side", title = "Green salad" });
        var listId = (await SendAsync(http, _userToken, HttpMethod.Post, "/api/households/shopping/lists", new { name = "Weekly shop" })).GetProperty("id").GetGuid();

        var client = CreateClient(allowPrivateNetworks: true);
        var endpoint = new MealieEndpoint(_baseUrl, _userToken);

        var about = Ok(await client.GetAboutAsync(endpoint, CancellationToken.None));
        Assert.True(MealieLiveCheck.IsSupported(about.Version ?? ""));

        var self = Ok(await client.GetSelfAsync(endpoint, CancellationToken.None));
        Assert.False(self.Admin);
        Assert.Equal("buddysync", self.Username);
        Assert.Equal("Family", self.Household);
        Assert.NotEqual(Guid.Empty, self.HouseholdId);
        Assert.Equal("home", self.GroupSlug);

        var search = Ok(await client.SearchRecipesAsync(endpoint, "lasagne", 1, 20, CancellationToken.None));
        Assert.Equal(1, search.Total);
        Assert.Equal(recipeId, Assert.Single(search.Items!).Id);

        var recipe = Ok(await client.GetRecipeAsync(endpoint, recipeId, CancellationToken.None));
        Assert.Equal("lasagne-al-forno", recipe.Slug);
        Assert.Equal("500 g minced beef", Assert.Single(recipe.RecipeIngredient!).Display);
        Assert.Equal("Brown the beef.", Assert.Single(recipe.RecipeInstructions!).Text);

        // No photo uploaded: Mealie answers 404, which Buddy reports as not found.
        var image = await client.GetRecipeImageAsync(endpoint, recipeId, CancellationToken.None);
        Assert.Equal(MealieFailureKind.NotFound, image.FailureOf().Kind);

        var plan = Ok(await client.ListMealPlanAsync(endpoint, new DateOnly(2031, 3, 1), new DateOnly(2031, 3, 7), CancellationToken.None));
        Assert.Equal(2, plan.Count);
        var dinner = plan.Single(e => e.EntryType == "dinner");
        Assert.Equal(recipeId, dinner.RecipeId);
        Assert.Equal("Lasagne al forno", dinner.Recipe?.Name);
        Assert.Equal("Green salad", plan.Single(e => e.EntryType == "side").Title);

        var lists = Ok(await client.ListShoppingListsAsync(endpoint, CancellationToken.None));
        Assert.Contains(lists, l => l.Id == listId && l.Name == "Weekly shop");

        Ok(await client.AddRecipesToShoppingListAsync(endpoint, listId, [new MealieShoppingListRecipeDto(recipeId, 2)], CancellationToken.None));
        var list = await SendAsync(http, _userToken, HttpMethod.Get, $"/api/households/shopping/lists/{listId}", null);
        Assert.NotEmpty(list.GetProperty("listItems").EnumerateArray());

        // Mealie answers an unknown list with a 500, which is why AddMealPlanToMealieShoppingList
        // checks the lists before reporting the failure.
        var missingList = await client.AddRecipesToShoppingListAsync(endpoint, Guid.NewGuid(), [new MealieShoppingListRecipeDto(recipeId, 1)], CancellationToken.None);
        Assert.Equal(MealieFailureKind.Error, missingList.FailureOf().Kind);
    }

    [MealieContractFact]
    public async Task The_live_check_refuses_an_admin_token_and_a_bad_token()
    {
        var client = CreateClient(allowPrivateNetworks: true);

        var (_, admin) = await MealieLiveCheck.RunAsync(client, new MealieEndpoint(_baseUrl, _adminToken), CancellationToken.None);
        var (_, bad) = await MealieLiveCheck.RunAsync(client, new MealieEndpoint(_baseUrl, "not-a-token"), CancellationToken.None);
        var (passed, _) = await MealieLiveCheck.RunAsync(client, new MealieEndpoint(_baseUrl, _userToken), CancellationToken.None);

        Assert.Equal(MealieLiveCheck.AdminRejectedMessage, admin?.Failure.Message);
        Assert.Equal(MealieFailureKind.TokenRejected, bad?.Failure.Kind);
        Assert.NotNull(passed);
    }

    [MealieContractFact]
    public async Task With_private_networks_off_the_container_on_localhost_is_unreachable()
    {
        var client = CreateClient(allowPrivateNetworks: false);

        var about = await client.GetAboutAsync(new MealieEndpoint(_baseUrl, _userToken), CancellationToken.None);

        Assert.Equal(MealieFailureKind.Unreachable, about.FailureOf().Kind);
    }

    // The same handler setup as MealplansFeature registers.
    internal static MealieClient CreateClient(bool allowPrivateNetworks) => new(new HttpClient(new SocketsHttpHandler
    {
        AllowAutoRedirect = false,
        UseCookies = false,
        UseProxy = false,
        ConnectCallback = (context, cancellationToken) => MealieOutboundPolicy.ConnectAsync(context, allowPrivateNetworks, cancellationToken)
    })
    {
        Timeout = TimeSpan.FromSeconds(10),
        MaxResponseContentBufferSize = new MealieOptions().MaxResponseBytes
    });

    private static T Ok<T>(MealieResponse<T> response) => response switch
    {
        MealieResponse<T>.Ok(var value) => value,
        MealieResponse<T>.Failed(var failure) => throw new Xunit.Sdk.XunitException($"Mealie call failed: {failure.Kind} {failure.Message}"),
    };

    private static async Task<string> LoginAsync(HttpClient http, string username, string password)
    {
        using var response = await http.PostAsync("/api/auth/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["username"] = username,
            ["password"] = password
        }));
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("access_token").GetString()!;
    }

    private static async Task<string> CreateApiTokenAsync(HttpClient http, string sessionToken) =>
        (await SendAsync(http, sessionToken, HttpMethod.Post, "/api/users/api-tokens", new { name = "buddy" })).GetProperty("token").GetString()!;

    private static async Task<JsonElement> SendAsync(HttpClient http, string token, HttpMethod method, string path, object? body)
    {
        using var request = new HttpRequestMessage(method, path) { Content = body is null ? null : JsonContent.Create(body) };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var response = await http.SendAsync(request);
        var text = await response.Content.ReadAsStringAsync();

        Assert.True(response.IsSuccessStatusCode, $"{method} {path}: {(int)response.StatusCode} {text}");

        return text.Length == 0 ? default : JsonElement.Parse(text);
    }
}

// A [Fact] that only runs when MEALIE_CONTRACT_TESTS=1 (xunit 2 has no runtime skip).
public sealed class MealieContractFactAttribute : FactAttribute
{
    public static bool Enabled => Environment.GetEnvironmentVariable("MEALIE_CONTRACT_TESTS") == "1";

    public MealieContractFactAttribute()
    {
        if (!Enabled)
        {
            Skip = "Contract test against a real Mealie; run with MEALIE_CONTRACT_TESTS=1 (task test:contract:mealie).";
        }
    }
}
