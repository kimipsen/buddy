using Alba;

using buddy.IntegrationTests.Fixtures;

using Xunit;

namespace buddy.IntegrationTests.Features.Users;

// Pins what the REST authentication pipeline does today, so the MCP scheme work
// (docs/backend/analysis/mcp-server.md, slice 2) provably leaves it alone. Runs on the shared host
// (Features:Mcp as the fixture sets it) and on an explicit flag-off host; the per-user rate-limit
// partition is pinned by RateLimitingTests, which runs on the same fixture configuration.
[Collection(BuddyApiCollection.Name)]
public sealed class RestAuthenticationRegressionTests(BuddyApiFixture fixture)
{
    // Keycloak-signed in the real world; this one is just a well-formed JWT with a bad signature.
    private const string ForgedExpiredJwt =
        "eyJhbGciOiJSUzI1NiIsInR5cCI6IkpXVCJ9.eyJleHAiOjEsImF1ZCI6ImJ1ZGR5LWFwaSJ9.c2lnbmF0dXJl";

    public static TheoryData<bool> Hosts => new() { false, true };

    private async Task<IAlbaHost> HostAsync(bool flagOff) =>
        flagOff
            ? await fixture.SharedHostAsync($"{nameof(RestAuthenticationRegressionTests)}.Off", new Dictionary<string, string?> { ["Features:Mcp"] = "false" })
            : fixture.Host;

    [Theory]
    [MemberData(nameof(Hosts))]
    public async Task A_valid_token_reaches_an_authenticated_endpoint(bool flagOff)
    {
        var host = await HostAsync(flagOff);
        var user = await fixture.CreateUserAsync();
        var token = await fixture.GetAccessTokenAsync(user);

        await host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Get.Url("/users/me");
            _.StatusCodeShouldBeOk();
        });
    }

    [Theory]
    [MemberData(nameof(Hosts))]
    public async Task A_missing_garbage_or_expired_token_gets_401(bool flagOff)
    {
        var host = await HostAsync(flagOff);

        await host.Scenario(_ =>
        {
            _.Get.Url("/users/me");
            _.StatusCodeShouldBe(401);
        });

        foreach (var bad in new[] { "garbage", ForgedExpiredJwt })
        {
            await host.Scenario(_ =>
            {
                _.WithRequestHeader("Authorization", $"Bearer {bad}");
                _.Get.Url("/users/me");
                _.StatusCodeShouldBe(401);
            });
        }
    }

    [Theory]
    [MemberData(nameof(Hosts))]
    public async Task A_token_for_another_audience_gets_401(bool flagOff)
    {
        var host = await HostAsync(flagOff);
        var user = await fixture.CreateUserAsync();
        var otherAudience = await fixture.GetAccessTokenForClientAsync("buddy-other", user);

        await host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {otherAudience}");
            _.Get.Url("/users/me");
            _.StatusCodeShouldBe(401);
        });
    }

    [Theory]
    [MemberData(nameof(Hosts))]
    public async Task The_REST_401_challenge_is_a_plain_Bearer_without_resource_metadata(bool flagOff)
    {
        var host = await HostAsync(flagOff);

        var anonymous = await host.Scenario(_ =>
        {
            _.Get.Url("/users/me");
            _.StatusCodeShouldBe(401);
        });
        Assert.Equal("Bearer", anonymous.Context.Response.Headers.WWWAuthenticate.ToString());

        var invalid = await host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", "Bearer garbage");
            _.Get.Url("/users/me");
            _.StatusCodeShouldBe(401);
        });
        Assert.StartsWith("Bearer error=\"invalid_token\"", invalid.Context.Response.Headers.WWWAuthenticate.ToString());
        Assert.DoesNotContain("resource_metadata", invalid.Context.Response.Headers.WWWAuthenticate.ToString());
    }

    [Theory]
    [MemberData(nameof(Hosts))]
    public async Task Anonymous_endpoints_stay_anonymous(bool flagOff)
    {
        var host = await HostAsync(flagOff);

        await host.Scenario(_ =>
        {
            _.Get.Url("/features");
            _.StatusCodeShouldBeOk();
        });
        await host.Scenario(_ =>
        {
            _.Get.Url("/health");
            _.StatusCodeShouldBeOk();
        });
    }
}
