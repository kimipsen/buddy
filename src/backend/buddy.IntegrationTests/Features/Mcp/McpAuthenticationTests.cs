using Alba;

using buddy.IntegrationTests.Fixtures;

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

using Xunit;

namespace buddy.IntegrationTests.Features.Mcp;

// Slice 2 of docs/backend/analysis/mcp-server.md: the policy scheme, the buddy-mcp audience (R3), the
// buddy-mcp:read scope policy and the flag-off scheme set.
[Collection(BuddyApiCollection.Name)]
public sealed class McpAuthenticationTests(BuddyApiFixture fixture)
{
    private const string Initialize =
        """{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-06-18","capabilities":{},"clientInfo":{"name":"test","version":"1"}}}""";

    private async Task<int> PostMcpAsync(string path, string? token)
    {
        var result = await fixture.Host.Scenario(_ =>
        {
            if (token is not null)
            {
                _.WithRequestHeader("Authorization", $"Bearer {token}");
            }

            _.WithRequestHeader("Accept", "application/json, text/event-stream");
            _.Post.Text(Initialize).ToUrl(path);
            _.ConfigureHttpContext(context => context.Request.ContentType = "application/json");
            _.IgnoreStatusCode();
        });

        return result.Context.Response.StatusCode;
    }

    private async Task<int> GetRestAsync(string token)
    {
        var result = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Get.Url("/users/me");
            _.IgnoreStatusCode();
        });

        return result.Context.Response.StatusCode;
    }

    [Fact]
    public async Task A_buddy_mcp_token_with_the_read_scope_reaches_mcp()
    {
        var (user, _, _) = await fixture.CreateAuthenticatedUserAsync();
        var token = await fixture.GetMcpAccessTokenAsync(user);

        Assert.Equal(200, await PostMcpAsync("/mcp", token));
    }

    [Fact]
    public async Task GET_and_DELETE_on_mcp_answer_405_when_authenticated()
    {
        var (user, _, _) = await fixture.CreateAuthenticatedUserAsync();
        var token = await fixture.GetMcpAccessTokenAsync(user);

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Get.Url("/mcp");
            _.StatusCodeShouldBe(405);
        });
        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Delete.Url("/mcp");
            _.StatusCodeShouldBe(405);
        });
    }

    [Fact]
    public async Task The_path_match_ignores_case_and_a_trailing_slash_and_stops_at_segments()
    {
        var (user, restToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var mcpToken = await fixture.GetMcpAccessTokenAsync(user);

        // /MCP and /mcp/ are the MCP endpoint: the REST token is turned away there (wrong audience), the MCP one is not.
        foreach (var path in new[] { "/MCP", "/mcp/" })
        {
            Assert.Equal(401, await PostMcpAsync(path, restToken));
            Assert.NotEqual(401, await PostMcpAsync(path, mcpToken));
        }

        // /mcpfoo is a different path, no MCP endpoint and not the MCP scheme: an unknown route for either token.
        Assert.Equal(404, await PostMcpAsync("/mcpfoo", restToken));
        Assert.Equal(404, await PostMcpAsync("/mcpfoo", mcpToken));
    }

    [Fact]
    public async Task A_buddy_api_token_never_authenticates_on_mcp()
    {
        var (_, restToken, _) = await fixture.CreateAuthenticatedUserAsync();

        Assert.Equal(401, await PostMcpAsync("/mcp", restToken));
    }

    [Fact]
    public async Task A_token_for_another_client_is_rejected_on_mcp_and_on_rest()
    {
        var user = await fixture.CreateUserAsync();
        var other = await fixture.GetAccessTokenForClientAsync("buddy-other", user);

        Assert.Equal(401, await PostMcpAsync("/mcp", other));
        Assert.Equal(401, await GetRestAsync(other));
    }

    [Fact]
    public async Task A_buddy_mcp_token_never_authenticates_on_rest()
    {
        var (user, _, _) = await fixture.CreateAuthenticatedUserAsync();
        var mcpToken = await fixture.GetMcpAccessTokenAsync(user);

        Assert.Equal(401, await GetRestAsync(mcpToken));
    }

    [Fact]
    public async Task A_missing_or_garbage_token_gets_401_with_the_resource_metadata_challenge()
    {
        foreach (var token in new string?[] { null, "garbage" })
        {
            var result = await fixture.Host.Scenario(_ =>
            {
                if (token is not null)
                {
                    _.WithRequestHeader("Authorization", $"Bearer {token}");
                }

                _.Post.Json(new { }).ToUrl("/mcp");
                _.StatusCodeShouldBe(401);
            });

            var challenge = result.Context.Response.Headers.WWWAuthenticate.ToString();
            Assert.StartsWith("Bearer", challenge);
            Assert.Contains("resource_metadata=", challenge);
            Assert.Contains("/.well-known/oauth-protected-resource", challenge);
        }
    }

    [Fact]
    public async Task The_right_audience_without_the_read_scope_gets_403_insufficient_scope()
    {
        var (user, _, _) = await fixture.CreateAuthenticatedUserAsync();
        var noScope = await fixture.GetMcpAccessTokenAsync(user, "openid");

        var result = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {noScope}");
            _.WithRequestHeader("Accept", "application/json, text/event-stream");
            _.Post.Text(Initialize).ToUrl("/mcp");
            _.ConfigureHttpContext(context => context.Request.ContentType = "application/json");
            _.StatusCodeShouldBe(403);
        });

        var challenge = result.Context.Response.Headers.WWWAuthenticate.ToString();
        Assert.Contains("error=\"insufficient_scope\"", challenge);
        Assert.Contains("scope=\"buddy-mcp:read\"", challenge);
        Assert.Contains("resource_metadata=", challenge);
    }

    [Fact]
    public async Task The_REST_pipeline_with_mcp_on_still_answers_the_plain_challenge()
    {
        var result = await fixture.Host.Scenario(_ =>
        {
            _.Get.Url("/users/me");
            _.StatusCodeShouldBe(401);
        });

        Assert.Equal("Bearer", result.Context.Response.Headers.WWWAuthenticate.ToString());
    }

    [Fact]
    public async Task With_the_flag_off_the_scheme_set_and_the_metadata_route_are_as_before()
    {
        var off = await fixture.SharedHostAsync("McpAuthenticationTests.Off", new Dictionary<string, string?> { ["Features:Mcp"] = "false" });
        var schemes = await off.Services.GetRequiredService<Microsoft.AspNetCore.Authentication.IAuthenticationSchemeProvider>().GetAllSchemesAsync();

        Assert.Equal(["Bearer"], schemes.Select(scheme => scheme.Name).Order(StringComparer.Ordinal));

        await off.Scenario(_ =>
        {
            _.Get.Url("/.well-known/oauth-protected-resource/mcp");
            _.StatusCodeShouldBe(404);
        });
    }

    [Fact]
    public async Task The_protected_resource_metadata_names_the_issuer_scope_and_resource()
    {
        var result = await fixture.Host.Scenario(_ =>
        {
            _.Get.Url("/.well-known/oauth-protected-resource/mcp");
            _.StatusCodeShouldBeOk();
        });

        var json = result.ReadAsJson<System.Text.Json.JsonElement>();
        Assert.EndsWith("/mcp", json.GetProperty("resource").GetString());
        Assert.Contains("buddy-mcp:read", json.GetProperty("scopes_supported").EnumerateArray().Select(e => e.GetString()));
        Assert.Contains("/realms/buddy-test", json.GetProperty("authorization_servers")[0].GetString());
    }

    [Fact]
    public async Task Starting_with_mcp_on_and_an_empty_audience_fails_and_with_mcp_off_it_starts()
    {
        var empty = new Dictionary<string, string?> { ["Authentication:Keycloak:Audience"] = "" };

        await Assert.ThrowsAsync<OptionsValidationException>(async () =>
        {
            await using var _ = await fixture.CreateHostAsync(new Dictionary<string, string?>(empty) { ["Features:Mcp"] = "true" });
        });

        await using var off = await fixture.CreateHostAsync(new Dictionary<string, string?>(empty) { ["Features:Mcp"] = "false" });
        Assert.NotNull(off.Services);
    }
}
