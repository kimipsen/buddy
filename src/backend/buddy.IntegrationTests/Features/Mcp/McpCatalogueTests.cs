using System.Text.Json;

using buddy.Features.Mcp;
using buddy.IntegrationTests.Fixtures;

using Xunit;

using static buddy.IntegrationTests.Features.NightDuty.NightDutyTestHelpers;

namespace buddy.IntegrationTests.Features.Mcp;

// Slice 6 of docs/backend/analysis/mcp-server.md: partial-flag tools/list and the not-found path of every
// tool that takes an id the caller can be denied.
[Collection(BuddyApiCollection.Name)]
public sealed class McpCatalogueTests(BuddyApiFixture fixture)
{
    private static string TextOf(JsonElement result) => result.GetProperty("content")[0].GetProperty("text").GetString()!;

    [Fact]
    public async Task With_Mealplans_off_only_the_two_meal_tools_are_gone()
    {
        var host = await fixture.SharedHostAsync(
            $"{nameof(McpCatalogueTests)}.MealplansOff",
            new Dictionary<string, string?> { ["Features:Mcp"] = "true", ["Features:Mealplans"] = "false" });
        var (user, _, _) = await fixture.CreateAuthenticatedUserAsync();
        var token = await fixture.GetMcpAccessTokenAsync(user);
        string[] all =
        [
            ToolNames.Whoami, ToolNames.GetToday, ToolNames.ListChildren, ToolNames.ListGroups, ToolNames.ListCalendars,
            ToolNames.ListCalendarOccurrences, ToolNames.ListMealPlan, ToolNames.ListMeals, ToolNames.ListNightDuty,
            ToolNames.ListPickups, ToolNames.ListTaskTemplates, ToolNames.ListHouseRules, ToolNames.ListSleepDiary,
            ToolNames.ListWorkDays, ToolNames.GetChildProgress,
        ];

        var listed = (await McpTestClient.RpcAsync(host, token, "tools/list")).GetProperty("result").GetProperty("tools").EnumerateArray()
            .Select(t => t.GetProperty("name").GetString()!).Order(StringComparer.Ordinal).ToList();

        Assert.Equal(all.Except([ToolNames.ListMealPlan, ToolNames.ListMeals]).Order(StringComparer.Ordinal), listed);
    }

    [Fact]
    public async Task Every_tool_that_takes_a_child_answers_not_found_for_a_child_the_caller_is_not_linked_to()
    {
        var (_, otherToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var othersChild = await Guardians.GuardianTestHelpers.CreateChildAsync(fixture, otherToken);
        var (guardian, _, guardianId) = await fixture.CreateAuthenticatedUserAsync();
        var token = await fixture.GetMcpAccessTokenAsync(guardian);
        var monday = FutureMonday();
        var range = new { from = monday, to = monday.AddDays(1) };
        var calls = new (string Tool, object Arguments)[]
        {
            (ToolNames.ListTaskTemplates, new { childId = othersChild.Id }),
            (ToolNames.ListMeals, new { childId = othersChild.Id, groupId = (Guid?)null }),
            (ToolNames.ListPickups, new { childId = othersChild.Id, range.from, range.to }),
            (ToolNames.ListMealPlan, new { childId = othersChild.Id, groupId = (Guid?)null, range.from, range.to }),
            (ToolNames.ListNightDuty, new { childId = othersChild.Id, range.from, range.to }),
            (ToolNames.ListSleepDiary, new { childId = othersChild.Id, range.from, range.to }),
            (ToolNames.ListHouseRules, new { childId = othersChild.Id, groupId = (Guid?)null }),
            (ToolNames.GetChildProgress, new { childId = othersChild.Id }),
        };

        foreach (var (tool, arguments) in calls)
        {
            var result = await McpTestClient.CallToolAsync(fixture.Host, token, tool, arguments);

            Assert.True(result.TryGetProperty("isError", out var isError) && isError.GetBoolean(), $"{tool} should be an error: {TextOf(result)}");
            Assert.Equal(
                McpResults.NotFoundCode,
                JsonElement.Parse(TextOf(result)).GetProperty("error").GetProperty("code").GetString());
        }

        Assert.NotEqual(Guid.Empty, guardianId);
    }
}
