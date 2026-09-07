namespace buddy.Features.Mealplans;

// Builds the response DTO every AI-session endpoint returns -- a chat-displayable transcript
// (replayed from the raw events, mirroring AiSessionHistoryBuilder) plus the draft enriched with
// meal names (an extra read against IMealEventStore, the same join MealPlanExpansion already does
// for the real plan).
public static class AiSessionViewBuilder
{
    public static async Task<AiSessionView> BuildAsync(
        MealplanAiSession session, IReadOnlyCollection<MealplanAiSessionEvent> events, IMealEventStore meals, CancellationToken cancellationToken)
    {
        List<AiSessionTranscriptEntry> transcript = [];

        foreach (var @event in events)
        {
            switch (@event)
            {
                case AiUserMessageSent userMessage:
                    transcript.Add(new AiSessionTranscriptEntry(AiChatMessageRole.User, userMessage.Text, userMessage.OccurredAt));
                    break;
                case AiAssistantMessageRecorded assistantMessage:
                    transcript.Add(new AiSessionTranscriptEntry(AiChatMessageRole.Assistant, assistantMessage.Text, assistantMessage.OccurredAt));
                    break;
            }
        }

        List<AiSessionDraftEntry> draft = [];

        foreach (var entry in session.Draft.OrderBy(kv => kv.Key.Date).ThenBy(kv => kv.Key.Slot))
        {
            var mealEvents = await meals.ReadAsync(entry.Value, cancellationToken);
            var meal = Meal.Rehydrate(mealEvents);
            draft.Add(new AiSessionDraftEntry(entry.Key.Date, entry.Key.Slot, entry.Value, meal?.Name ?? "(deleted meal)"));
        }

        return new AiSessionView(session.Id, session.From, session.To, session.RequestedSlots, session.Status, transcript, draft);
    }
}
