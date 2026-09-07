using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

using Microsoft.Extensions.Options;

namespace buddy.Features.Mealplans;

// Raw HTTP against OpenAI's Chat Completions API -- symmetric with AnthropicChatClient, behind the
// same IAiChatClient seam.
public sealed class OpenAiChatClient(HttpClient httpClient, IOptionsMonitor<AiAssistantModelOptions> options) : IAiChatClient
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public async Task<AiChatCompletionResult> SendAsync(AiChatCompletionRequest request, CancellationToken cancellationToken)
    {
        List<OpenAiMessage> messages = [new OpenAiMessage("system", request.SystemPrompt, null, null), .. BuildMessages(request.History)];

        var openAiRequest = new OpenAiRequest(
            options.CurrentValue.OpenAiModel,
            messages,
            request.Tools.Count == 0
                ? null
                : [.. request.Tools.Select(t => new OpenAiToolDefinition("function", new OpenAiFunctionDefinition(t.Name, t.Description, ParseJson(t.JsonSchema))))]);

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "v1/chat/completions")
        {
            Content = JsonContent.Create(openAiRequest, options: SerializerOptions)
        };
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", request.ApiKey);

        using var response = await httpClient.SendAsync(httpRequest, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new AiProviderException(response.StatusCode, body);
        }

        var payload = await response.Content.ReadFromJsonAsync<OpenAiResponse>(SerializerOptions, cancellationToken)
            ?? throw new InvalidOperationException("OpenAI returned an empty response body.");

        var message = payload.Choices.FirstOrDefault()?.Message
            ?? throw new InvalidOperationException("OpenAI returned no choices.");

        AiRequestedToolCall[] toolCalls = message.ToolCalls is null
            ? []
            : [.. message.ToolCalls.Select(c => new AiRequestedToolCall(c.Id, c.Function.Name, c.Function.Arguments))];

        return new AiChatCompletionResult(string.IsNullOrEmpty(message.Content) ? null : message.Content, toolCalls);
    }

    // OpenAI represents a tool round trip as an assistant message carrying tool_calls, followed by
    // one separate "tool" role message per call (unlike Anthropic, which bundles all tool_result
    // blocks into a single user message) -- see the Chat Completions tool-calling contract.
    private static List<OpenAiMessage> BuildMessages(IReadOnlyList<AiChatMessage> history)
    {
        List<OpenAiMessage> messages = [];

        foreach (var turn in history)
        {
            if (turn.Role == AiChatMessageRole.User)
            {
                messages.Add(new OpenAiMessage("user", turn.Text ?? "", null, null));
                continue;
            }

            if (turn.ToolInvocations.Count > 0)
            {
                messages.Add(new OpenAiMessage(
                    "assistant",
                    string.IsNullOrEmpty(turn.Text) ? null : turn.Text,
                    [.. turn.ToolInvocations.Select(t => new OpenAiToolCall(t.ToolCallId, "function", new OpenAiFunctionCall(t.ToolName, t.ArgumentsJson)))],
                    null));

                foreach (var invocation in turn.ToolInvocations)
                {
                    messages.Add(new OpenAiMessage("tool", invocation.ResultJson, null, invocation.ToolCallId));
                }

                continue;
            }

            if (!string.IsNullOrEmpty(turn.Text))
            {
                messages.Add(new OpenAiMessage("assistant", turn.Text, null, null));
            }
        }

        return messages;
    }

    private static JsonElement ParseJson(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}

internal sealed record OpenAiRequest(
    [property: JsonPropertyName("model")] string Model,
    [property: JsonPropertyName("messages")] IReadOnlyList<OpenAiMessage> Messages,
    [property: JsonPropertyName("tools")] IReadOnlyList<OpenAiToolDefinition>? Tools);

internal sealed record OpenAiMessage(
    [property: JsonPropertyName("role")] string Role,
    [property: JsonPropertyName("content")] string? Content,
    [property: JsonPropertyName("tool_calls")] IReadOnlyList<OpenAiToolCall>? ToolCalls,
    [property: JsonPropertyName("tool_call_id")] string? ToolCallId);

internal sealed record OpenAiToolCall(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("function")] OpenAiFunctionCall Function);

internal sealed record OpenAiFunctionCall(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("arguments")] string Arguments);

internal sealed record OpenAiToolDefinition(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("function")] OpenAiFunctionDefinition Function);

internal sealed record OpenAiFunctionDefinition(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("parameters")] JsonElement Parameters);

internal sealed record OpenAiResponse(
    [property: JsonPropertyName("choices")] IReadOnlyList<OpenAiChoice> Choices);

internal sealed record OpenAiChoice(
    [property: JsonPropertyName("message")] OpenAiResponseMessage Message);

internal sealed record OpenAiResponseMessage(
    [property: JsonPropertyName("content")] string? Content,
    [property: JsonPropertyName("tool_calls")] IReadOnlyList<OpenAiToolCall>? ToolCalls);
