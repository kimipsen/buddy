using System.Text.Json;

using Alba;

using buddy.Features.Mealplans;
using buddy.IntegrationTests.Features.Guardians;
using buddy.IntegrationTests.Fixtures;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace buddy.IntegrationTests.Features.Mealplans.Mealie;

// A second host whose IMealieClient is FakeMealieClient, shared by every Mealie test class. It uses
// the same database, Keycloak and Mailpit as fixture.Host, so children and meals created through
// the shared helpers on fixture.Host are visible here.
internal static class MealieTestHost
{
    public const string Token = "mealie-token-1234";

    public static Task<IAlbaHost> GetAsync(BuddyApiFixture fixture) =>
        fixture.SharedHostAsync(
            "Mealie",
            new Dictionary<string, string?>(),
            services => services.Replace(ServiceDescriptor.Singleton<IMealieClient>(FakeMealieClient.Instance)));

    // A guardian with one child, a fresh fake server, and (unless connect is false) a connection.
    public static async Task<MealieFamily> CreateFamilyAsync(BuddyApiFixture fixture, bool connect = true)
    {
        var host = await GetAsync(fixture);
        var (_, token, guardianId) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token, "Emil");
        var server = FakeMealieClient.Instance.NewServer();
        var family = new MealieFamily(host, token, guardianId, child, server);

        if (connect)
        {
            await family.ConnectAsync();
        }

        return family;
    }
}

internal sealed record MealieFamily(IAlbaHost Host, string Token, Guid GuardianId, ChildResponseDto Child, FakeMealieServer Server)
{
    public Guid ChildId => Child.Id;

    public string Path(string relative) => $"/mealplans/children/{ChildId}/{relative}";

    public async Task<JsonElement> ConnectAsync(string? baseUrl = null, string apiToken = MealieTestHost.Token, int expectedStatus = 200)
    {
        var response = await Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {Token}");
            _.Put.Json(new { BaseUrl = baseUrl ?? Server.BaseUrl, ApiToken = apiToken }).ToUrl(Path("mealie"));
            _.StatusCodeShouldBe(expectedStatus);
        });

        return JsonElement.Parse(await response.ReadAsTextAsync());
    }

    public Task<JsonElement> GetAsync(string relative, int expectedStatus = 200, string? token = null) =>
        SendAsync(relative, expectedStatus, token, (s, url) => s.Get.Url(url));

    public Task<JsonElement> PostAsync(string relative, object? body, int expectedStatus = 200, string? token = null, string? idempotencyKey = null) =>
        SendAsync(relative, expectedStatus, token, (s, url) =>
        {
            if (idempotencyKey is not null)
            {
                s.WithRequestHeader("Idempotency-Key", idempotencyKey);
            }

            if (body is null)
            {
                s.Post.Url(url);
            }
            else
            {
                s.Post.Json(body).ToUrl(url);
            }
        });

    public Task<JsonElement> PutAsync(string relative, object body, int expectedStatus = 200, string? token = null) =>
        SendAsync(relative, expectedStatus, token, (s, url) => s.Put.Json(body).ToUrl(url));

    public Task<JsonElement> DeleteAsync(string relative, int expectedStatus = 204, string? token = null) =>
        SendAsync(relative, expectedStatus, token, (s, url) => s.Delete.Url(url));

    private async Task<JsonElement> SendAsync(string relative, int expectedStatus, string? token, Action<Scenario, string> send)
    {
        var response = await Host.Scenario(s =>
        {
            s.WithRequestHeader("Authorization", $"Bearer {token ?? Token}");
            send(s, Path(relative));
            s.StatusCodeShouldBe(expectedStatus);
        });

        var text = await response.ReadAsTextAsync();

        return text.Length == 0 ? default : JsonElement.Parse(text);
    }
}
