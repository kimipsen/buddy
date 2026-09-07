using buddy.Features.Users;

namespace buddy.Features.Mealplans;

public union MealplanAiSessionEvent(
    AiSessionStarted,
    AiUserMessageSent,
    AiToolInvocationRecorded,
    AiDraftAssignmentSet,
    AiDraftAssignmentCleared,
    AiAssistantMessageRecorded,
    AiSessionApplied,
    AiSessionDiscarded
)
{
    public static MealplanAiSessionEvent FromPayload(object payload) => payload switch
    {
        AiSessionStarted e => e,
        AiUserMessageSent e => e,
        AiToolInvocationRecorded e => e,
        AiDraftAssignmentSet e => e,
        AiDraftAssignmentCleared e => e,
        AiAssistantMessageRecorded e => e,
        AiSessionApplied e => e,
        AiSessionDiscarded e => e,
        _ => throw new ArgumentException($"Unknown AI session event payload: {payload.GetType().Name}", nameof(payload)),
    };

    public string EventType => this switch
    {
        AiSessionStarted => nameof(AiSessionStarted),
        AiUserMessageSent => nameof(AiUserMessageSent),
        AiToolInvocationRecorded => nameof(AiToolInvocationRecorded),
        AiDraftAssignmentSet => nameof(AiDraftAssignmentSet),
        AiDraftAssignmentCleared => nameof(AiDraftAssignmentCleared),
        AiAssistantMessageRecorded => nameof(AiAssistantMessageRecorded),
        AiSessionApplied => nameof(AiSessionApplied),
        AiSessionDiscarded => nameof(AiSessionDiscarded),
    };
}

// Starts a brand new stream every time -- unlike MealPlan/AiProviderCredential (one stream ever,
// appended to forever), a session is superseded by the next one a family starts. "Current" is
// resolved by picking the most-recently-started stream across the family (see
// AiSessionResolution.ResolveCurrentSessionIdAsync), not by a single write-once index row, so
// starting a session under any sibling in the family always correctly supersedes the last one
// regardless of which child anchored it.
public sealed record AiSessionStarted(
    MealplanAiSessionId Id,
    UserId ChildId,
    DateOnly From,
    DateOnly To,
    IReadOnlyCollection<MealSlot> RequestedSlots,
    IReadOnlyCollection<MealId> MustIncludeMealIds,
    string? Notes,
    UserId StartedBy,
    DateTimeOffset OccurredAt);

public sealed record AiUserMessageSent(MealplanAiSessionId Id, string Text, UserId SentBy, DateTimeOffset OccurredAt);

// One per tool call the assistant made while producing a single reply -- needed to faithfully
// rebuild the provider's conversation history on the next turn (see AiSessionHistoryBuilder).
// ResultJson is always the server's own validated outcome, never anything the model claimed.
public sealed record AiToolInvocationRecorded(MealplanAiSessionId Id, string ToolCallId, string ToolName, string ArgumentsJson, string ResultJson, bool IsError, DateTimeOffset OccurredAt);

// One slot per event, the same granularity MealAssignedToSlot uses -- these are draft-only
// changes (see MealplanAiSession.Draft), never applied to the real MealPlan until ApplyAiSessionDraft.
public sealed record AiDraftAssignmentSet(MealplanAiSessionId Id, DateOnly Date, MealSlot Slot, MealId MealId, DateTimeOffset OccurredAt);

public sealed record AiDraftAssignmentCleared(MealplanAiSessionId Id, DateOnly Date, MealSlot Slot, DateTimeOffset OccurredAt);

// The assistant's final natural-language reply for one user message, recorded once the
// tool-calling loop for that message has no further tool calls to make.
public sealed record AiAssistantMessageRecorded(MealplanAiSessionId Id, string Text, DateTimeOffset OccurredAt);

public sealed record AiSessionApplied(MealplanAiSessionId Id, UserId AppliedBy, DateTimeOffset OccurredAt);

public sealed record AiSessionDiscarded(MealplanAiSessionId Id, UserId DiscardedBy, DateTimeOffset OccurredAt);
