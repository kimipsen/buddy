using System.Text.Json;

using buddy.Common;
using buddy.Features.Mcp;
using buddy.Features.Users;
using buddy.IntegrationTests.Features.Calendars;
using buddy.IntegrationTests.Features.Guardians;
using buddy.IntegrationTests.Features.SleepDiaries;
using buddy.IntegrationTests.Fixtures;

using Alba;

using Xunit;

using static buddy.IntegrationTests.Features.NightDuty.NightDutyTestHelpers;

namespace buddy.IntegrationTests.Features.Mcp;

// Slice 6 of docs/backend/analysis/mcp-server.md: the Testing-section cases the earlier slices
// did not cover (revoked link, child token on a direct tool call, shaping end to end, every read tool).
[Collection(BuddyApiCollection.Name)]
public sealed class McpReadTests(BuddyApiFixture fixture)
{
    private static string TextOf(JsonElement result) => result.GetProperty("content")[0].GetProperty("text").GetString()!;

    private static string ErrorCodeOf(JsonElement result) => JsonElement.Parse(TextOf(result)).GetProperty("error").GetProperty("code").GetString()!;

    [Fact]
    public async Task After_the_guardian_link_is_revoked_the_next_call_is_not_found()
    {
        var (user, restToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, restToken);
        var mcpToken = await fixture.GetMcpAccessTokenAsync(user);
        var monday = FutureMonday();
        var arguments = new { childId = child.Id, from = monday, to = monday };

        var before = await McpTestClient.CallToolAsync(fixture.Host, mcpToken, "list_night_duty", arguments);
        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {restToken}");
            _.Delete.Url($"/users/me/children/{child.Id}/guardian-link");
            _.StatusCodeShouldBe(204);
        });
        var after = await McpTestClient.CallToolAsync(fixture.Host, mcpToken, "list_night_duty", arguments);

        Assert.False(before.TryGetProperty("isError", out var wasError) && wasError.GetBoolean());
        Assert.True(after.GetProperty("isError").GetBoolean());
        Assert.Equal(McpResults.NotFoundCode, ErrorCodeOf(after));
    }

    [Fact]
    public async Task A_child_token_on_a_direct_tool_call_gets_403_and_no_tool_runs()
    {
        var child = await fixture.CreateUserAsync(groups: [KeycloakGroups.Child]);
        var token = await fixture.GetMcpAccessTokenAsync(child);
        var monday = FutureMonday();
        var body = JsonSerializer.Serialize(new
        {
            jsonrpc = "2.0",
            id = 1,
            method = "tools/call",
            @params = new { name = "list_night_duty", arguments = new { childId = Guid.NewGuid(), from = monday, to = monday } },
        });

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.WithRequestHeader("Accept", "application/json, text/event-stream");
            _.Post.Text(body).ToUrl("/mcp");
            _.ConfigureHttpContext(context => context.Request.ContentType = "application/json");
            _.StatusCodeShouldBe(403);
        });

        Assert.Equal(McpGuardianMiddleware.ErrorCode, response.ReadAsJson<ErrorEnvelope>().Code);
    }

    [Fact]
    public async Task A_text_field_over_its_cap_comes_back_truncated_through_a_tool()
    {
        var (user, restToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, restToken);
        await SleepDiaryTestHelpers.UpdateNotesAsync(fixture, restToken, child.Id, new string('n', McpTextShaping.NotesLimit + 100));
        var mcpToken = await fixture.GetMcpAccessTokenAsync(user);
        var monday = FutureMonday();

        var structured = (await McpTestClient.CallToolAsync(fixture.Host, mcpToken, "list_sleep_diary", new { childId = child.Id, from = monday, to = monday }))
            .GetProperty("structuredContent");

        Assert.Equal(McpTextShaping.NotesLimit, structured.GetProperty("sleepHygieneNotes").GetString()!.Length);
        Assert.True(structured.GetProperty("truncated").GetBoolean());
    }

    [Fact]
    public async Task A_calendar_the_caller_owns_is_listed_and_its_occurrences_are_readable()
    {
        var (user, restToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var calendarId = await CalendarTestHelpers.CreateCalendarAsync(fixture, restToken, "Family");
        var day = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1);
        await CalendarTestHelpers.CreateEventAsync(fixture, restToken, calendarId, "Dentist", day);
        var mcpToken = await fixture.GetMcpAccessTokenAsync(user);

        var calendars = (await McpTestClient.CallToolAsync(fixture.Host, mcpToken, "list_calendars")).GetProperty("structuredContent").GetProperty("items");
        var occurrences = (await McpTestClient.CallToolAsync(fixture.Host, mcpToken, "list_calendar_occurrences", new { calendarId, from = day, to = day }))
            .GetProperty("structuredContent").GetProperty("items");

        Assert.Contains(calendars.EnumerateArray(), c => c.GetProperty("id").GetGuid() == calendarId);
        Assert.Equal("Dentist", Assert.Single(occurrences.EnumerateArray()).GetProperty("title").GetString());
    }

    [Fact]
    public async Task Every_read_tool_answers_a_guardian_with_a_child_without_an_error()
    {
        var (user, restToken, userId) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, restToken);
        var mcpToken = await fixture.GetMcpAccessTokenAsync(user);
        var monday = FutureMonday();
        var range = new { from = monday, to = monday.AddDays(1) };
        var calls = new (string Tool, object Arguments)[]
        {
            // The schema lists childId and groupId as both required (nullable), so a client sends the unused one as null.
            ("whoami", new { }),
            ("get_today", new { }),
            ("list_children", new { }),
            ("list_groups", new { }),
            ("list_calendars", new { }),
            ("list_task_templates", new { childId = child.Id }),
            ("list_meals", new { childId = child.Id, groupId = (Guid?)null }),
            ("list_pickups", new { childId = child.Id, range.from, range.to }),
            ("list_meal_plan", new { childId = child.Id, groupId = (Guid?)null, range.from, range.to }),
            ("list_night_duty", new { childId = child.Id, range.from, range.to }),
            ("list_sleep_diary", new { childId = child.Id, range.from, range.to }),
            ("list_house_rules", new { childId = child.Id, groupId = (Guid?)null }),
            ("get_child_progress", new { childId = child.Id }),
            ("list_work_days", new { guardianId = userId, range.from, range.to }),
        };

        foreach (var (tool, arguments) in calls)
        {
            var result = await McpTestClient.CallToolAsync(fixture.Host, mcpToken, tool, arguments);

            Assert.False(result.TryGetProperty("isError", out var isError) && isError.GetBoolean(), $"{tool}: {TextOf(result)}");
        }
    }
}
