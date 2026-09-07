using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace buddy.Features.Mealplans;

// Raw HTTP against Anthropic's Messages API (no SDK dependency) -- deliberately symmetric with the
// other providers' adapters, all behind IAiChatClient. The iterative tool-calling loop is not this
// client's concern: SendAsync is a single round trip, one request in, one response out.
public sealed class AnthropicChatClient(HttpClient httpClient) : IAiChatClient
{
    private const string ApiVersion = "2023-06-01";

    // A cost/quality default for a consumer app, not the family's only option -- letting a family
    // pick a model per provider is a frontend/catalog concern for a later phase (see the AI
    // mealplan plan's build order).
    private const string Model = "claude-sonnet-5";

    private const int MaxTokens = 2048;

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public async Task<AiChatCompletionResult> SendAsync(AiChatCompletionRequest request, CancellationToken cancellationToken)
    {
        var anthropicRequest = new AnthropicRequest(
            Model,
            MaxTokens,
            request.SystemPrompt,
            BuildMessages(request.History),
            [.. request.Tools.Select(t => new AnthropicToolDefinition(t.Name, t.Description, ParseJson(t.JsonSchema)))]);

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "v1/messages")
        {
            Content = JsonContent.Create(anthropicRequest, options: SerializerOptions)
        };
        httpRequest.Headers.Add("x-api-key", request.ApiKey);
        httpRequest.Headers.Add("anthropic-version", ApiVersion);

        using var response = await httpClient.SendAsync(httpRequest, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new AiProviderException(response.StatusCode, body);
        }

        var payload = await response.Content.ReadFromJsonAsync<AnthropicResponse>(SerializerOptions, cancellationToken)
            ?? throw new InvalidOperationException("Anthropic returned an empty response body.");

        var text = string.Concat(payload.Content.Where(b => b.Type == "text").Select(b => b.Text));

        AiRequestedToolCall[] toolCalls =
        [
            .. payload.Content
                .Where(b => b.Type == "tool_use")
                .Select(b => new AiRequestedToolCall(b.Id!, b.Name!, b.Input!.Value.GetRawText()))
        ];

        return new AiChatCompletionResult(text.Length == 0 ? null : text, toolCalls);
    }

    // Anthropic requires every tool_use block a turn produced to be answered by tool_result blocks
    // bundled into a single following user message (never split across several) -- see the
    // "parallel tool use" guidance this mirrors.
    private static List<AnthropicMessage> BuildMessages(IReadOnlyList<AiChatMessage> history)
    {
        List<AnthropicMessage> messages = [];

        foreach (var turn in history)
        {
            if (turn.Role == AiChatMessageRole.User)
            {
                messages.Add(new AnthropicMessage("user", [AnthropicContentBlock.OfText(turn.Text ?? "")]));
                continue;
            }

            if (turn.ToolInvocations.Count > 0)
            {
                messages.Add(new AnthropicMessage("assistant",
                    [.. turn.ToolInvocations.Select(t => AnthropicContentBlock.OfToolUse(t.ToolCallId, t.ToolName, ParseJson(t.ArgumentsJson)))]));

                messages.Add(new AnthropicMessage("user",
                    [.. turn.ToolInvocations.Select(t => AnthropicContentBlock.OfToolResult(t.ToolCallId, t.ResultJson, t.IsError))]));
            }

            if (!string.IsNullOrEmpty(turn.Text))
            {
                messages.Add(new AnthropicMessage("assistant", [AnthropicContentBlock.OfText(turn.Text)]));
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

internal sealed record AnthropicRequest(
    [property: JsonPropertyName("model")] string Model,
    [property: JsonPropertyName("max_tokens")] int MaxTokens,
    [property: JsonPropertyName("system")] string System,
    [property: JsonPropertyName("messages")] IReadOnlyList<AnthropicMessage> Messages,
    [property: JsonPropertyName("tools")] IReadOnlyList<AnthropicToolDefinition> Tools);

internal sealed record AnthropicToolDefinition(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("input_schema")] JsonElement InputSchema);

internal sealed record AnthropicMessage(
    [property: JsonPropertyName("role")] string Role,
    [property: JsonPropertyName("content")] IReadOnlyList<AnthropicContentBlock> Content);

// One shape covering all three block kinds Anthropic uses here (text / tool_use / tool_result) --
// an internal wire DTO, not a domain type, so this stays a flexible grab-bag rather than three
// separate records plus a discriminator.
internal sealed record AnthropicContentBlock(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("text")] string? Text = null,
    [property: JsonPropertyName("id")] string? Id = null,
    [property: JsonPropertyName("name")] string? Name = null,
    [property: JsonPropertyName("input")] JsonElement? Input = null,
    [property: JsonPropertyName("tool_use_id")] string? ToolUseId = null,
    [property: JsonPropertyName("content")] string? ToolResultContent = null,
    [property: JsonPropertyName("is_error")] bool? IsError = null)
{
    public static AnthropicContentBlock OfText(string text) => new("text", Text: text);

    public static AnthropicContentBlock OfToolUse(string id, string name, JsonElement input) => new("tool_use", Id: id, Name: name, Input: input);

    public static AnthropicContentBlock OfToolResult(string toolUseId, string resultJson, bool isError) =>
        new("tool_result", ToolUseId: toolUseId, ToolResultContent: resultJson, IsError: isError ? true : null);
}

internal sealed record AnthropicResponse(
    [property: JsonPropertyName("content")] IReadOnlyList<AnthropicContentBlock> Content);
