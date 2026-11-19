using Alba;

using buddy.Common;
using buddy.Common.RateLimiting;
using buddy.IntegrationTests.Common;
using buddy.IntegrationTests.Fixtures;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Xunit;

namespace buddy.IntegrationTests.Features.Mcp;

// Slice 3 of docs/backend/analysis/mcp-server.md: the "mcp" policy on POST /mcp, and the global
// limiter's exemption for exactly the endpoints that carry it.
[Collection(BuddyApiCollection.Name)]
public sealed class McpRateLimitTests(BuddyApiFixture fixture) : IAsyncLifetime
{
    private const int McpBurst = 3;
    private const int UserBurst = 2;
    private const int IpBurst = 2;

    private const string Initialize =
        """{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-06-18","capabilities":{},"clientInfo":{"name":"test","version":"1"}}}""";

    private static readonly CapturingLoggerProvider Logs = new();

    private IAlbaHost _host = null!;

    public async Task InitializeAsync()
    {
        Logs.Clear();

        _host = await fixture.SharedHostAsync(
            nameof(McpRateLimitTests),
            new Dictionary<string, string?>
            {
                ["RateLimiting:Mcp:PermitLimit"] = $"{McpBurst}",
                ["RateLimiting:Mcp:Window"] = "01:00:00",
                ["RateLimiting:Authenticated:TokenLimit"] = $"{UserBurst}",
                ["RateLimiting:Authenticated:TokensPerPeriod"] = "1",
                ["RateLimiting:Authenticated:ReplenishmentPeriod"] = "01:00:00",
                ["RateLimiting:Anonymous:TokenLimit"] = $"{IpBurst}",
                ["RateLimiting:Anonymous:TokensPerPeriod"] = "1",
                ["RateLimiting:Anonymous:ReplenishmentPeriod"] = "01:00:00"
            },
            services => services.AddSingleton<ILoggerProvider>(Logs));
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<IScenarioResult> PostMcpAsync(string? token)
    {
        return await _host.Scenario(_ =>
        {
            if (token is not null)
            {
                _.WithRequestHeader("Authorization", $"Bearer {token}");
            }

            _.WithRequestHeader("Accept", "application/json, text/event-stream");
            _.Post.Text(Initialize).ToUrl("/mcp");
            _.ConfigureHttpContext(context => context.Request.ContentType = "application/json");
            _.IgnoreStatusCode();
        });
    }

    private async Task<int> StatusAsync(string method, string url, string token)
    {
        var result = await _host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            if (method == "DELETE")
            {
                _.Delete.Url(url);
            }
            else
            {
                _.Get.Url(url);
            }

            _.IgnoreStatusCode();
        });

        return result.Context.Response.StatusCode;
    }

    [Fact]
    public async Task Mcp_calls_draw_from_the_mcp_policy_and_the_next_is_429_with_a_log_naming_the_endpoint()
    {
        var (user, _, _) = await fixture.CreateAuthenticatedUserAsync();
        var token = await fixture.GetMcpAccessTokenAsync(user);

        // More than the user's global burst (2): only the mcp policy applies to POST /mcp.
        for (var i = 0; i < McpBurst; i++)
        {
            Assert.Equal(200, (await PostMcpAsync(token)).Context.Response.StatusCode);
        }

        var rejected = await PostMcpAsync(token);
        Assert.Equal(429, rejected.Context.Response.StatusCode);
        Assert.True(rejected.Context.Response.Headers.ContainsKey("Retry-After"));
        Assert.Equal(RateLimitingFeature.ErrorCode, rejected.ReadAsJson<ErrorEnvelope>().Code);

        var log = Assert.Single(Logs.Entries, e => e.EventId == 8005);
        Assert.Equal("authenticated", log.Property("CallerKind"));
        Assert.Contains("/mcp", (string)log.Property("Endpoint")!);
    }

    [Fact]
    public async Task A_looping_mcp_client_does_not_drain_the_same_guardians_rest_bucket()
    {
        var (user, restToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var mcpToken = await fixture.GetMcpAccessTokenAsync(user);

        for (var i = 0; i < McpBurst + 2; i++)
        {
            await PostMcpAsync(mcpToken);
        }

        // The same subject's global bucket is still full: its whole burst is available to REST.
        for (var i = 0; i < UserBurst; i++)
        {
            Assert.Equal(200, await StatusAsync("GET", "/users/me", restToken));
        }

        Assert.Equal(429, await StatusAsync("GET", "/users/me", restToken));
    }

    [Fact]
    public async Task Everything_else_under_mcp_keeps_the_global_partitioning()
    {
        var (user, _, _) = await fixture.CreateAuthenticatedUserAsync();
        var token = await fixture.GetMcpAccessTokenAsync(user);

        // GET and DELETE are 405 endpoints without the policy: they draw from the global bucket.
        Assert.Equal(405, await StatusAsync("GET", "/mcp", token));
        Assert.Equal(405, await StatusAsync("DELETE", "/mcp", token));
        Assert.Equal(429, await StatusAsync("GET", "/mcp", token));

        // An unmatched /mcp path is not exempt either (anonymous: the per-IP bucket).
        var statuses = new List<int>();
        for (var i = 0; i < IpBurst + 3; i++)
        {
            var result = await _host.Scenario(_ =>
            {
                _.Get.Url("/mcp/anything");
                _.IgnoreStatusCode();
            });
            statuses.Add(result.Context.Response.StatusCode);
        }

        Assert.Contains(429, statuses);
    }

    [Fact]
    public async Task Unauthenticated_mcp_requests_are_limited_by_the_mcp_policy_not_left_unlimited()
    {
        // The 401 challenge happens after the limiter, so an anonymous POST /mcp is counted by the
        // mcp policy (partition by client IP), not exempted by the global limiter.
        var statuses = new List<int>();
        for (var i = 0; i < McpBurst + 3; i++)
        {
            statuses.Add((await PostMcpAsync(null)).Context.Response.StatusCode);
        }

        Assert.Equal(401, statuses[0]);
        Assert.Equal(429, statuses[^1]);
    }

    [Fact]
    public async Task A_401_on_mcp_logs_nothing_under_the_production_filters()
    {
        // Documented in docs/mcp-setup.md: JwtBearer's failure lines are Information in a
        // Microsoft.AspNetCore category, which appsettings.json filters to Warning, and the code has
        // no application-level 401 log. The 429 line (event 8005, category RateLimitingFeature) is the
        // one rollout signal on /mcp.
        // Startup lines (the cleanup-is-off notice, event 15007) predate the request; only what the 401 logs counts.
        var before = Logs.Entries.Length;

        await PostMcpAsync("garbage");

        var logged = Logs.Entries.Skip(before).ToArray();
        Assert.DoesNotContain(logged, e => e.Level >= LogLevel.Information && e.Category.Contains("JwtBearer"));
        Assert.DoesNotContain(logged, e => e.EventId is 8005 or >= 15000 and < 15100);
    }
}
