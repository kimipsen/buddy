using System.Text.Json;

using Alba;

using buddy.IntegrationTests.Fixtures;

namespace buddy.IntegrationTests.Features.Mcp;

// A JSON-RPC call to POST /mcp (stateless: no initialize needed). The response is JSON or a single
// SSE "data:" event, depending on the SDK; both are read to the JSON-RPC message.
internal static class McpTestClient
{
    public static async Task<JsonElement> RpcAsync(IAlbaHost host, string token, string method, object? parameters = null, int id = 1)
    {
        var body = JsonSerializer.Serialize(new { jsonrpc = "2.0", id, method, @params = parameters ?? new { } });

        var response = await host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.WithRequestHeader("Accept", "application/json, text/event-stream");
            _.Post.Text(body).ToUrl("/mcp");
            _.ConfigureHttpContext(context => context.Request.ContentType = "application/json");
            _.StatusCodeShouldBe(200);
        });

        var text = await response.ReadAsTextAsync();
        var json = text.TrimStart().StartsWith('{')
            ? text
            : string.Join("", text.Split('\n').Where(line => line.StartsWith("data:", StringComparison.Ordinal)).Select(line => line[5..].Trim()));

        return JsonElement.Parse(json);
    }

    public static async Task<JsonElement> CallToolAsync(IAlbaHost host, string token, string tool, object? arguments = null) =>
        (await RpcAsync(host, token, "tools/call", new { name = tool, arguments = arguments ?? new { } })).GetProperty("result");

    public static async Task<(TestUser User, string Token)> GuardianAsync(BuddyApiFixture fixture)
    {
        var (user, _, _) = await fixture.CreateAuthenticatedUserAsync();
        return (user, await fixture.GetMcpAccessTokenAsync(user));
    }
}
