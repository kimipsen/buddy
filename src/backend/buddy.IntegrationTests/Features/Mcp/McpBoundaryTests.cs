using System.Reflection;
using System.Text.Json;

using Alba;

using buddy.Features.Mcp;
using buddy.IntegrationTests.Common;
using buddy.IntegrationTests.Features.Guardians;
using buddy.IntegrationTests.Fixtures;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Xunit;

using static buddy.IntegrationTests.Features.NightDuty.NightDutyTestHelpers;

namespace buddy.IntegrationTests.Features.Mcp;

// Slice 6 of docs/backend/analysis/mcp-server.md: range boundaries, malformed input, the golden
// coverage check and the "no argument value in any log line" check.
[Collection(BuddyApiCollection.Name)]
public sealed class McpBoundaryTests(BuddyApiFixture fixture) : IAsyncLifetime
{
    private const string Sentinel = "SENTINEL-7f3a9c-do-not-log";

    private static readonly CapturingLoggerProvider Logs = new();

    private IAlbaHost _host = null!;

    public async Task InitializeAsync()
    {
        Logs.Clear();
        _host = await fixture.SharedHostAsync(
            nameof(McpBoundaryTests),
            new Dictionary<string, string?> { ["Features:Mcp"] = "true" },
            services => services.AddSingleton<ILoggerProvider>(Logs));
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private static string TextOf(JsonElement result) => result.GetProperty("content")[0].GetProperty("text").GetString()!;

    private static string ErrorCodeOf(JsonElement result) => JsonElement.Parse(TextOf(result)).GetProperty("error").GetProperty("code").GetString()!;

    private async Task<IScenarioResult> PostRawAsync(string token, string body) =>
        await _host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.WithRequestHeader("Accept", "application/json, text/event-stream");
            _.Post.Text(body).ToUrl("/mcp");
            _.ConfigureHttpContext(context => context.Request.ContentType = "application/json");
            _.IgnoreStatusCode();
        });

    [Fact]
    public async Task A_range_of_92_days_is_accepted_and_93_is_a_validation_error()
    {
        var (user, restToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, restToken);
        var token = await fixture.GetMcpAccessTokenAsync(user);
        var from = FutureMonday();

        var ok = await McpTestClient.CallToolAsync(_host, token, "list_sleep_diary", new { childId = child.Id, from, to = from.AddDays(92) });
        var tooLong = await McpTestClient.CallToolAsync(_host, token, "list_sleep_diary", new { childId = child.Id, from, to = from.AddDays(93) });

        Assert.False(ok.TryGetProperty("isError", out var isError) && isError.GetBoolean());
        Assert.True(tooLong.GetProperty("isError").GetBoolean());
        Assert.Equal("validation_error", ErrorCodeOf(tooLong));
    }

    [Fact]
    public async Task An_unknown_tool_name_is_an_error_not_a_server_error()
    {
        var (_, token) = await McpTestClient.GuardianAsync(fixture);

        var response = await McpTestClient.RpcAsync(_host, token, "tools/call", new { name = "delete_everything", arguments = new { } });

        Assert.True(response.TryGetProperty("error", out _) || response.GetProperty("result").GetProperty("isError").GetBoolean());
    }

    [Theory]
    [InlineData("""{"jsonrpc":"2.0","id":1,"method":"tools/call","params":{"name":"list_night_duty","arguments":{"childId":42,"from":"x","to":[]}}}""")]
    [InlineData("""{"jsonrpc":"2.0","id":1,"method":"tools/call","params":{"name":"list_night_duty","arguments":{}}}""")]
    [InlineData("""{"jsonrpc":"2.0","id":1,"method":"tools/call","params":{"name":"whoami","arguments":{"unexpected":"value"}}}""")]
    [InlineData("""{"jsonrpc":"2.0","id":1,"method":"tools/call","params":{"name":"list_children","arguments":"not-an-object"}}""")]
    public async Task Malformed_arguments_never_give_a_server_error(string body)
    {
        var (_, token) = await McpTestClient.GuardianAsync(fixture);

        var result = await PostRawAsync(token, body);

        Assert.InRange(result.Context.Response.StatusCode, 200, 499);
    }

    [Fact]
    public async Task A_malformed_json_body_is_not_a_server_error()
    {
        var (_, token) = await McpTestClient.GuardianAsync(fixture);

        var result = await PostRawAsync(token, "{ this is not json");

        Assert.InRange(result.Context.Response.StatusCode, 200, 499);
    }

    [Fact]
    public async Task The_golden_file_has_an_entry_for_every_tool_and_nothing_else()
    {
        var path = Path.Join(AppContext.BaseDirectory, "Features", "Mcp", "Golden", "tools-list.json");
        var golden = JsonDocument.Parse(await File.ReadAllTextAsync(path));
        var goldenNames = golden.RootElement.GetProperty("tools").EnumerateArray().Select(t => t.GetProperty("name").GetString()!).Order(StringComparer.Ordinal).ToList();

        var declared = typeof(ToolNames).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral)
            .Select(f => (string)f.GetRawConstantValue()!)
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.Equal(declared, goldenNames);
    }

    [Fact]
    public async Task No_log_line_contains_a_value_the_caller_sent()
    {
        var (user, restToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, restToken);
        var token = await fixture.GetMcpAccessTokenAsync(user);
        var from = FutureMonday();
        Logs.Clear();

        await McpTestClient.CallToolAsync(_host, token, "list_night_duty", new { childId = child.Id, from = from.AddDays(3), to = from });
        // The SDK itself logs an unknown tool NAME at Error level (category ModelContextProtocol.Server.McpServer);
        // that is a name, not an argument value, and is left as is (see the slice 6 summary).
        await McpTestClient.RpcAsync(_host, token, "tools/call", new { name = "whoami", arguments = new { note = Sentinel } });
        await PostRawAsync(token, """{"jsonrpc":"2.0","id":1,"method":"tools/call","params":{"name":"list_house_rules","arguments":{"childId":"SENTINEL"}}}""".Replace("SENTINEL", Sentinel, StringComparison.Ordinal));
        await McpTestClient.CallToolAsync(_host, token, "list_house_rules", new { childId = child.Id, groupId = Guid.NewGuid() });

        Assert.DoesNotContain(Logs.Entries, e => e.Message.Contains(Sentinel, StringComparison.Ordinal));
        Assert.DoesNotContain(Logs.Entries, e => e.Message.Contains(from.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal) && e.Category.Contains("Mcp", StringComparison.OrdinalIgnoreCase));
    }
}
