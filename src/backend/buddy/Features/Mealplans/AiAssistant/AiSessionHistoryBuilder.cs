namespace buddy.Features.Mealplans;

// Replays a session's raw event stream into the provider-agnostic conversation shape a
// IAiChatClient needs to see (see Providers/IAiChatClient.cs) -- the aggregate itself never
// carries this, since it's a replay concern, not decision state (see MealplanAiSession).
public static class AiSessionHistoryBuilder
{
    public static IReadOnlyList<AiChatMessage> Build(IEnumerable<MealplanAiSessionEvent> events)
    {
        List<AiChatMessage> messages = [];
        List<AiToolInvocation>? pendingToolInvocations = null;

        foreach (var @event in events)
        {
            switch (@event)
            {
                case AiUserMessageSent userMessage:
                    messages.Add(new AiChatMessage(AiChatMessageRole.User, userMessage.Text, []));
                    break;

                case AiToolInvocationRecorded tool:
                    (pendingToolInvocations ??= []).Add(new AiToolInvocation(tool.ToolCallId, tool.ToolName, tool.ArgumentsJson, tool.ResultJson, tool.IsError));
                    break;

                // Flushes whatever tool invocations accumulated since the last assistant message --
                // events for one turn are always appended in (tool invocations)*, then this, in
                // that order, so nothing from a later turn can be pending here yet.
                case AiAssistantMessageRecorded assistantMessage:
                    messages.Add(new AiChatMessage(AiChatMessageRole.Assistant, assistantMessage.Text, pendingToolInvocations ?? []));
                    pendingToolInvocations = null;
                    break;
            }
        }

        return messages;
    }
}
