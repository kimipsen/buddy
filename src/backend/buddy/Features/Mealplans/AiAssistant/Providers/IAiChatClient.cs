namespace buddy.Features.Mealplans;

// One provider-agnostic round trip: send the conversation so far plus the available tools, get
// back either a final reply or a request to run some tools. The iterative call-execute-call-again
// loop lives in SendAiSessionMessageHandler, not here -- keeping that control provider-agnostic in
// buddy rather than tied to any one provider's SDK helper (see the AI-assisted mealplan plan).
public interface IAiChatClient
{
    Task<AiChatCompletionResult> SendAsync(AiChatCompletionRequest request, CancellationToken cancellationToken);
}

public enum AiChatMessageRole
{
    User,
    Assistant
}

// A provider-agnostic transcript entry. An assistant message that made tool calls carries both the
// calls it made (with their already-executed results) and whatever text preceded/followed them --
// each provider adapter reconstructs its own wire format (Anthropic tool_use/tool_result blocks,
// OpenAI function calls, ...) from this shape (see AnthropicChatClient).
public sealed record AiChatMessage(AiChatMessageRole Role, string? Text, IReadOnlyList<AiToolInvocation> ToolInvocations);

public sealed record AiToolInvocation(string ToolCallId, string ToolName, string ArgumentsJson, string ResultJson, bool IsError);

public sealed record AiToolDefinition(string Name, string Description, string JsonSchema);

public sealed record AiChatCompletionRequest(
    string ApiKey,
    string SystemPrompt,
    IReadOnlyList<AiChatMessage> History,
    IReadOnlyList<AiToolDefinition> Tools);

// One round of the provider responding: either it's done (Text present, no ToolCalls) or it wants
// to call tools (ToolCalls present; Text may still carry a preamble the model said first).
public sealed record AiChatCompletionResult(string? Text, IReadOnlyList<AiRequestedToolCall> ToolCalls);

public sealed record AiRequestedToolCall(string ToolCallId, string ToolName, string ArgumentsJson);
