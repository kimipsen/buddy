using System.Text.Json;
using System.Text.Json.Nodes;

using buddy.Features.Mcp;
using buddy.IntegrationTests.Features.Guardians;
using buddy.IntegrationTests.Fixtures;

using Xunit;

using static buddy.IntegrationTests.Features.NightDuty.NightDutyTestHelpers;

namespace buddy.IntegrationTests.Features.Mcp;

// Slice 5 of docs/backend/analysis/mcp-server.md: the 15 read tools, result mapping and text shaping.
[Collection(BuddyApiCollection.Name)]
public sealed class McpToolTests(BuddyApiFixture fixture)
{
    private static readonly string[] ExpectedTools =
    [
        "get_child_progress", "get_today", "list_calendar_occurrences", "list_calendars", "list_children", "list_groups",
        "list_house_rules", "list_meal_plan", "list_meals", "list_night_duty", "list_pickups", "list_sleep_diary",
        "list_task_templates", "list_work_days", "whoami",
    ];

    private static string SourceGoldenPath([System.Runtime.CompilerServices.CallerFilePath] string testFile = "") =>
        Path.Join(Path.GetDirectoryName(testFile)!, "Golden", "tools-list.json");

    private static string TextOf(JsonElement result) => result.GetProperty("content")[0].GetProperty("text").GetString()!;

    private static string ErrorCodeOf(JsonElement result) => JsonElement.Parse(TextOf(result)).GetProperty("error").GetProperty("code").GetString()!;

    [Fact]
    public async Task Tools_list_has_the_whole_read_catalogue_marked_read_only()
    {
        var (_, token) = await McpTestClient.GuardianAsync(fixture);

        var result = (await McpTestClient.RpcAsync(fixture.Host, token, "tools/list")).GetProperty("result");
        var tools = result.GetProperty("tools").EnumerateArray().ToList();

        Assert.Equal(ExpectedTools, tools.Select(t => t.GetProperty("name").GetString()!).Order(StringComparer.Ordinal));
        Assert.All(tools, t => Assert.True(t.GetProperty("annotations").GetProperty("readOnlyHint").GetBoolean()));
        Assert.All(tools, t => Assert.False(string.IsNullOrWhiteSpace(t.GetProperty("description").GetString())));
    }

    [Fact]
    public async Task Tools_list_matches_the_golden_file()
    {
        var (_, token) = await McpTestClient.GuardianAsync(fixture);
        var result = (await McpTestClient.RpcAsync(fixture.Host, token, "tools/list")).GetProperty("result");

        var tools = result.GetProperty("tools").EnumerateArray()
            .OrderBy(t => t.GetProperty("name").GetString(), StringComparer.Ordinal)
            .Select(t => JsonNode.Parse(t.GetRawText()))
            .ToArray();
        var actual = JsonSerializer.Serialize(new { instructions = McpFeature.ServerInstructions, tools }, new JsonSerializerOptions { WriteIndented = true });

        // MCP_UPDATE_GOLDEN=1 rewrites the committed file (in the source tree) from the running server.
        if (Environment.GetEnvironmentVariable("MCP_UPDATE_GOLDEN") == "1")
        {
            File.WriteAllText(SourceGoldenPath(), actual + "\n");
        }

        var path = Path.Join(AppContext.BaseDirectory, "Features", "Mcp", "Golden", "tools-list.json");
        Assert.True(File.Exists(path), $"No golden file at Features/Mcp/Golden/tools-list.json. If the catalogue change is deliberate, create it with exactly this content:\n{actual}");
        Assert.Equal(File.ReadAllText(path).ReplaceLineEndings("\n").Trim(), actual.ReplaceLineEndings("\n").Trim());
    }

    [Fact]
    public async Task Whoami_returns_the_caller_without_provisioning_anyone()
    {
        var (user, token) = await McpTestClient.GuardianAsync(fixture);

        var result = await McpTestClient.CallToolAsync(fixture.Host, token, "whoami");

        Assert.False(result.TryGetProperty("isError", out var isError) && isError.GetBoolean());
        var structured = result.GetProperty("structuredContent");
        Assert.NotEqual(Guid.Empty, structured.GetProperty("id").GetGuid());
        Assert.Equal("UTC", structured.GetProperty("timeZone").GetString());
        Assert.DoesNotContain(user.Email, TextOf(result), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Get_today_returns_the_local_date_and_zone()
    {
        var (_, token) = await McpTestClient.GuardianAsync(fixture);

        var structured = (await McpTestClient.CallToolAsync(fixture.Host, token, "get_today")).GetProperty("structuredContent");

        Assert.Matches(@"^\d{4}-\d{2}-\d{2}$", structured.GetProperty("date").GetString());
        Assert.Equal("UTC", structured.GetProperty("timeZone").GetString());
    }

    [Fact]
    public async Task List_children_wraps_the_list_as_items()
    {
        var (guardian, restToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, restToken);
        var mcpToken = await fixture.GetMcpAccessTokenAsync(guardian);

        var items = (await McpTestClient.CallToolAsync(fixture.Host, mcpToken, "list_children")).GetProperty("structuredContent").GetProperty("items");

        Assert.Equal(child.Id, Assert.Single(items.EnumerateArray()).GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task List_night_duty_returns_the_nights_of_the_callers_child()
    {
        var (user, restToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, restToken);
        var monday = FutureMonday();
        var mcpToken = await fixture.GetMcpAccessTokenAsync(user);

        var result = await McpTestClient.CallToolAsync(fixture.Host, mcpToken, "list_night_duty", new { childId = child.Id, from = monday, to = monday.AddDays(1) });

        Assert.Equal(2, result.GetProperty("structuredContent").GetProperty("items").GetArrayLength());
    }

    [Fact]
    public async Task A_child_the_caller_is_not_linked_to_is_not_found()
    {
        var (_, otherToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var othersChild = await GuardianTestHelpers.CreateChildAsync(fixture, otherToken);
        var (_, token) = await McpTestClient.GuardianAsync(fixture);
        var monday = FutureMonday();

        var result = await McpTestClient.CallToolAsync(fixture.Host, token, "list_night_duty", new { childId = othersChild.Id, from = monday, to = monday });

        Assert.True(result.GetProperty("isError").GetBoolean());
        Assert.Equal(McpResults.NotFoundCode, ErrorCodeOf(result));
    }

    [Fact]
    public async Task A_validation_failure_is_an_error_result_with_field_errors()
    {
        var (user, restToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, restToken);
        var mcpToken = await fixture.GetMcpAccessTokenAsync(user);
        var monday = FutureMonday();

        var result = await McpTestClient.CallToolAsync(fixture.Host, mcpToken, "list_night_duty", new { childId = child.Id, from = monday.AddDays(3), to = monday });

        Assert.True(result.GetProperty("isError").GetBoolean());
        Assert.Equal("validation_error", ErrorCodeOf(result));
        Assert.NotEmpty(JsonElement.Parse(TextOf(result)).GetProperty("error").GetProperty("details").EnumerateObject());
    }

    [Fact]
    public async Task Naming_both_a_child_and_a_group_is_rejected_before_any_handler_runs()
    {
        var (_, token) = await McpTestClient.GuardianAsync(fixture);
        var monday = FutureMonday();

        var result = await McpTestClient.CallToolAsync(fixture.Host, token, "list_meal_plan", new { childId = Guid.NewGuid(), groupId = Guid.NewGuid(), from = monday, to = monday });

        Assert.True(result.GetProperty("isError").GetBoolean());
        Assert.Equal("validation_error", ErrorCodeOf(result));
    }

    [Fact]
    public async Task A_tool_of_a_feature_that_is_switched_off_is_not_listed_and_cannot_be_called()
    {
        var host = await fixture.SharedHostAsync(
            $"{nameof(McpToolTests)}.NightDutyOff",
            new Dictionary<string, string?> { ["Features:Mcp"] = "true", ["Features:NightDuty"] = "false" });
        var (user, _, _) = await fixture.CreateAuthenticatedUserAsync();
        var token = await fixture.GetMcpAccessTokenAsync(user);

        var listed = (await McpTestClient.RpcAsync(host, token, "tools/list")).GetProperty("result").GetProperty("tools").EnumerateArray()
            .Select(t => t.GetProperty("name").GetString()).ToList();
        var call = await McpTestClient.RpcAsync(host, token, "tools/call", new { name = "list_night_duty", arguments = new { } });

        Assert.DoesNotContain("list_night_duty", listed);
        Assert.Contains("list_children", listed);
        Assert.True(call.TryGetProperty("error", out _) || call.GetProperty("result").GetProperty("isError").GetBoolean());
    }

    [Fact]
    public void A_field_over_its_cap_is_cut_and_flagged()
    {
        var node = JsonNode.Parse($$"""{"items":[{"title":"{{new string('t', 300)}}","notes":"{{new string('n', 600)}}"},{"title":"short"}]}""")!;

        McpTextShaping.Cap(node);

        var first = node["items"]![0]!;
        Assert.Equal(McpTextShaping.TitleLimit, first["title"]!.GetValue<string>().Length);
        Assert.Equal(McpTextShaping.NotesLimit, first["notes"]!.GetValue<string>().Length);
        Assert.True(first["truncated"]!.GetValue<bool>());
        Assert.Null(node["items"]![1]!["truncated"]);
    }

    [Fact]
    public void A_cut_never_splits_a_surrogate_pair()
    {
        var text = new string('a', McpTextShaping.TitleLimit - 1) + "😀";
        var node = JsonNode.Parse(JsonSerializer.Serialize(new { title = text }))!;

        McpTextShaping.Cap(node);

        Assert.Equal(McpTextShaping.TitleLimit - 1, node["title"]!.GetValue<string>().Length);
    }
}
