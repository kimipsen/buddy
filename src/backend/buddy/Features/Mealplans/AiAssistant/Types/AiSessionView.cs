namespace buddy.Features.Mealplans;

// The response shape for every AI-session endpoint: enough of the raw event stream replayed into
// something a chat UI can render directly, plus the draft enriched with meal names (mirroring how
// MealPlanEntry enriches a plan assignment) so the frontend doesn't need a second round trip.
public sealed record AiSessionView(
    MealplanAiSessionId Id,
    DateOnly From,
    DateOnly To,
    IReadOnlyCollection<MealSlot> RequestedSlots,
    AiSessionStatus Status,
    IReadOnlyList<AiSessionTranscriptEntry> Transcript,
    IReadOnlyList<AiSessionDraftEntry> Draft);

public sealed record AiSessionTranscriptEntry(AiChatMessageRole Role, string Text, DateTimeOffset OccurredAt);

public sealed record AiSessionDraftEntry(DateOnly Date, MealSlot Slot, MealId MealId, string MealName);
