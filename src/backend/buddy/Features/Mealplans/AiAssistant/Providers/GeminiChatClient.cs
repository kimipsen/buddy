using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

using Microsoft.Extensions.Options;

namespace buddy.Features.Mealplans;

// Raw HTTP against Gemini's generateContent API -- symmetric with AnthropicChatClient/OpenAiChatClient,
// behind the same IAiChatClient seam.
public sealed class GeminiChatClient(HttpClient httpClient, IOptionsMonitor<AiAssistantModelOptions> options) : IAiChatClient
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public async Task<AiChatCompletionResult> SendAsync(AiChatCompletionRequest request, CancellationToken cancellationToken)
    {
        var geminiRequest = new GeminiRequest(
            new GeminiSystemInstruction([new GeminiPart(Text: request.SystemPrompt)]),
            BuildContents(request.History),
            request.Tools.Count == 0
                ? null
                : [new GeminiToolDeclaration([.. request.Tools.Select(t => new GeminiFunctionDeclaration(t.Name, t.Description, ParseJson(t.JsonSchema)))])]);

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"v1beta/models/{options.CurrentValue.GeminiModel}:generateContent")
        {
            Content = JsonContent.Create(geminiRequest, options: SerializerOptions)
        };
        httpRequest.Headers.Add("x-goog-api-key", request.ApiKey);

        using var response = await httpClient.SendAsync(httpRequest, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new AiProviderException(response.StatusCode, body);
        }

        var payload = await response.Content.ReadFromJsonAsync<GeminiResponse>(SerializerOptions, cancellationToken)
            ?? throw new InvalidOperationException("Gemini returned an empty response body.");

        var parts = payload.Candidates.FirstOrDefault()?.Content?.Parts ?? [];

        var text = string.Concat(parts.Where(p => p.Text is not null).Select(p => p.Text));

        // Gemini has no per-call id the way Anthropic/OpenAI do -- function calls are matched back
        // to their responses by name, so the id here is synthesized only to satisfy the shared
        // AiRequestedToolCall/AiToolInvocation shape (see BuildContents, which ignores it and
        // re-matches by name when rebuilding history for the next turn).
        AiRequestedToolCall[] toolCalls =
        [
            .. parts
                .Where(p => p.FunctionCall is not null)
                .Select(p => new AiRequestedToolCall(p.FunctionCall!.Name, p.FunctionCall.Name, p.FunctionCall.Args.GetRawText()))
        ];

        return new AiChatCompletionResult(text.Length == 0 ? null : text, toolCalls);
    }

    // A function-call turn becomes a "model"-role content with functionCall parts, followed by a
    // "user"-role content with the matching functionResponse parts -- Gemini's function-calling
    // contract, distinct from both Anthropic's and OpenAI's shapes.
    private static List<GeminiContent> BuildContents(IReadOnlyList<AiChatMessage> history)
    {
        List<GeminiContent> contents = [];

        foreach (var turn in history)
        {
            if (turn.Role == AiChatMessageRole.User)
            {
                contents.Add(new GeminiContent("user", [new GeminiPart(Text: turn.Text ?? "")]));
                continue;
            }

            if (turn.ToolInvocations.Count > 0)
            {
                contents.Add(new GeminiContent("model",
                    [.. turn.ToolInvocations.Select(t => new GeminiPart(FunctionCall: new GeminiFunctionCall(t.ToolName, ParseJson(t.ArgumentsJson))))]));

                contents.Add(new GeminiContent("user",
                    [.. turn.ToolInvocations.Select(t => new GeminiPart(FunctionResponse: new GeminiFunctionResponse(t.ToolName, ParseJson(t.ResultJson))))]));
            }

            if (!string.IsNullOrEmpty(turn.Text))
            {
                contents.Add(new GeminiContent("model", [new GeminiPart(Text: turn.Text)]));
            }
        }

        return contents;
    }

    private static JsonElement ParseJson(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}

internal sealed record GeminiRequest(
    [property: JsonPropertyName("systemInstruction")] GeminiSystemInstruction SystemInstruction,
    [property: JsonPropertyName("contents")] IReadOnlyList<GeminiContent> Contents,
    [property: JsonPropertyName("tools")] IReadOnlyList<GeminiToolDeclaration>? Tools);

internal sealed record GeminiSystemInstruction(
    [property: JsonPropertyName("parts")] IReadOnlyList<GeminiPart> Parts);

internal sealed record GeminiContent(
    [property: JsonPropertyName("role")] string Role,
    [property: JsonPropertyName("parts")] IReadOnlyList<GeminiPart> Parts);

// One shape covering all three part kinds Gemini uses here (text / functionCall / functionResponse)
// -- an internal wire DTO, not a domain type.
internal sealed record GeminiPart(
    [property: JsonPropertyName("text")] string? Text = null,
    [property: JsonPropertyName("functionCall")] GeminiFunctionCall? FunctionCall = null,
    [property: JsonPropertyName("functionResponse")] GeminiFunctionResponse? FunctionResponse = null);

internal sealed record GeminiFunctionCall(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("args")] JsonElement Args);

internal sealed record GeminiFunctionResponse(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("response")] JsonElement Response);

internal sealed record GeminiToolDeclaration(
    [property: JsonPropertyName("functionDeclarations")] IReadOnlyList<GeminiFunctionDeclaration> FunctionDeclarations);

internal sealed record GeminiFunctionDeclaration(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("parameters")] JsonElement Parameters);

internal sealed record GeminiResponse(
    [property: JsonPropertyName("candidates")] IReadOnlyList<GeminiCandidate> Candidates);

internal sealed record GeminiCandidate(
    [property: JsonPropertyName("content")] GeminiContent? Content);
