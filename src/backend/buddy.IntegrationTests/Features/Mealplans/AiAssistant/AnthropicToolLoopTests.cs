using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;

using Alba;

using buddy.Features.Mealplans;
using buddy.IntegrationTests.Features.Guardians;
using buddy.IntegrationTests.Fixtures;

using Microsoft.Extensions.DependencyInjection;

using Xunit;

namespace buddy.IntegrationTests.Features.Mealplans.AiAssistant;

// A tool-calling round trip through the real AnthropicChatClient over a stubbed HTTP handler: the
// stub answers like Anthropic does (text before a tool_use block, then a final reply once it has the
// tool results), so these check the wire shape of the follow-up request the tool loop sends.
[Collection(BuddyApiCollection.Name)]
public sealed class AnthropicToolLoopTests(BuddyApiFixture fixture) : IAsyncLifetime
{
    private static readonly List<string> RequestBodies = [];

    private IAlbaHost _host = null!;

    public async Task InitializeAsync()
    {
        lock (RequestBodies)
        {
            RequestBodies.Clear();
        }

        _host = await fixture.SharedHostAsync(
            nameof(AnthropicToolLoopTests),
            new Dictionary<string, string?>(),
            services => services.AddHttpClient<AnthropicChatClient>().ConfigurePrimaryHttpMessageHandler(() => new FakeAnthropicHandler()));
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task The_request_after_a_tool_call_ends_with_the_tool_results_not_an_assistant_turn()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token, "Alex");
        await MealplanTestHelpers.CreateMealAsync(fixture, token, child.Id);
        var from = DateOnly.FromDateTime(DateTime.UtcNow);
        var basePath = $"/mealplans/children/{child.Id}/ai";

        await SendAsync(token, s => s.Put.Json(new { ApiKey = "sk-ant-test1234" }).ToUrl($"{basePath}/providers/Anthropic/key"));
        await SendAsync(token, s => s.Put.Url($"{basePath}/data-sharing-acknowledgement"));
        await SendAsync(token, s => s.Post
            .Json(new { From = from, To = from.AddDays(2), Slots = new[] { MealSlot.Dinner }, MustIncludeMealIds = Array.Empty<Guid>(), Notes = (string?)null, RatedOnly = false, ServedWithin = AiServedWindow.Any })
            .ToUrl($"{basePath}/sessions"));

        var response = await SendAsync(token, s => s.Post.Json(new { Text = "Plan the dinners." }).ToUrl($"{basePath}/sessions/current/messages"));

        Assert.Equal(FakeAnthropicHandler.FinalReply, response.ReadAsJson<AiSessionViewDto>().Transcript[^1].Text);

        string followUp;
        lock (RequestBodies)
        {
            Assert.Equal(2, RequestBodies.Count);
            followUp = RequestBodies[1];
        }

        var lastMessage = JsonElement.Parse(followUp).GetProperty("messages").EnumerateArray().Last();
        Assert.Equal("user", lastMessage.GetProperty("role").GetString());
        Assert.All(lastMessage.GetProperty("content").EnumerateArray(), block => Assert.Equal("tool_result", block.GetProperty("type").GetString()));
    }

    private Task<IScenarioResult> SendAsync(string token, Action<Scenario> configure) =>
        _host.Scenario(s =>
        {
            s.WithRequestHeader("Authorization", $"Bearer {token}");
            configure(s);
            s.StatusCodeShouldBeOk();
        });

    // Calls get_calendar_conflicts (with a lead-in sentence, as Claude does) until the request
    // carries a tool result, then replies with FinalReply.
    private sealed class FakeAnthropicHandler : HttpMessageHandler
    {
        public const string FinalReply = "Here is your plan.";

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = await request.Content!.ReadAsStringAsync(cancellationToken);
            lock (RequestBodies)
            {
                RequestBodies.Add(body);
            }

            var today = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            object[] content = body.Contains("\"tool_result\"", StringComparison.Ordinal)
                ? [new { type = "text", text = FinalReply }]
                :
                [
                    new { type = "text", text = "Let me check the calendar first." },
                    new { type = "tool_use", id = "toolu_1", name = AiSessionTools.GetCalendarConflicts, input = new { from = today, to = today } },
                ];

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(new { content }), Encoding.UTF8, "application/json")
            };
        }
    }
}
