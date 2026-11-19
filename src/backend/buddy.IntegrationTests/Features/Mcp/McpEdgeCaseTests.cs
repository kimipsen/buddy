using Alba;

using buddy.IntegrationTests.Fixtures;

using Xunit;

namespace buddy.IntegrationTests.Features.Mcp;

// Slice 6 of docs/backend/analysis/mcp-server.md: the Testing-section cases not covered by the
// slice 2-5 tests (the rest are in McpReadTests): the flag-off 404 on /mcp and the stateless edge cases.
[Collection(BuddyApiCollection.Name)]
public sealed class McpEdgeCaseTests(BuddyApiFixture fixture)
{
    private static async Task<IScenarioResult> PostAsync(IAlbaHost host, string token, string body, int status = 200) =>
        await host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.WithRequestHeader("Accept", "application/json, text/event-stream");
            _.Post.Text(body).ToUrl("/mcp");
            _.ConfigureHttpContext(context => context.Request.ContentType = "application/json");
            _.StatusCodeShouldBe(status);
        });

    [Fact]
    public async Task With_the_flag_off_post_mcp_is_a_404()
    {
        var off = await fixture.SharedHostAsync("McpEdgeCaseTests.Off", new Dictionary<string, string?> { ["Features:Mcp"] = "false" });
        var (_, token) = await McpTestClient.GuardianAsync(fixture);

        await PostAsync(off, token, """{"jsonrpc":"2.0","id":1,"method":"ping"}""", status: 404);
    }

    [Fact]
    public async Task Ping_is_answered_without_a_session()
    {
        var (_, token) = await McpTestClient.GuardianAsync(fixture);

        var response = await McpTestClient.RpcAsync(fixture.Host, token, "ping");

        Assert.Equal(1, response.GetProperty("id").GetInt32());
        Assert.True(response.TryGetProperty("result", out _));
    }

    [Fact]
    public async Task A_request_with_an_unknown_session_id_header_is_not_a_server_error()
    {
        var (_, token) = await McpTestClient.GuardianAsync(fixture);

        var result = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.WithRequestHeader("Accept", "application/json, text/event-stream");
            _.WithRequestHeader("Mcp-Session-Id", "does-not-exist");
            _.Post.Text("""{"jsonrpc":"2.0","id":1,"method":"ping"}""").ToUrl("/mcp");
            _.ConfigureHttpContext(context => context.Request.ContentType = "application/json");
            _.IgnoreStatusCode();
        });

        Assert.InRange(result.Context.Response.StatusCode, 200, 499);
    }

    [Fact]
    public async Task An_unsupported_protocol_version_header_is_not_a_server_error()
    {
        var (_, token) = await McpTestClient.GuardianAsync(fixture);

        var result = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.WithRequestHeader("Accept", "application/json, text/event-stream");
            _.WithRequestHeader("MCP-Protocol-Version", "1999-01-01");
            _.Post.Text("""{"jsonrpc":"2.0","id":1,"method":"ping"}""").ToUrl("/mcp");
            _.ConfigureHttpContext(context => context.Request.ContentType = "application/json");
            _.IgnoreStatusCode();
        });

        Assert.InRange(result.Context.Response.StatusCode, 200, 499);
    }

    [Fact]
    public async Task A_batch_array_is_not_a_server_error()
    {
        var (_, token) = await McpTestClient.GuardianAsync(fixture);

        var result = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.WithRequestHeader("Accept", "application/json, text/event-stream");
            _.Post.Text("""[{"jsonrpc":"2.0","id":1,"method":"ping"},{"jsonrpc":"2.0","id":2,"method":"ping"}]""").ToUrl("/mcp");
            _.ConfigureHttpContext(context => context.Request.ContentType = "application/json");
            _.IgnoreStatusCode();
        });

        Assert.InRange(result.Context.Response.StatusCode, 200, 499);
    }
}
