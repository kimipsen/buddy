using System.Collections.Immutable;

namespace buddy.Features.Mealplans;

// Holds only decision-relevant state (the draft and its status) -- the conversation transcript
// itself is never needed to decide anything, so it's derived on demand from the raw event stream
// (see AiSessionHistoryBuilder) rather than carried here.
public sealed record MealplanAiSession(
    MealplanAiSessionId Id,
    DateOnly From,
    DateOnly To,
    ImmutableHashSet<MealSlot> RequestedSlots,
    ImmutableDictionary<(DateOnly Date, MealSlot Slot), MealId> Draft,
    AiSessionStatus Status)
{
    public static MealplanAiSession? Rehydrate(IEnumerable<MealplanAiSessionEvent> events)
    {
        MealplanAiSession? session = null;

        foreach (var @event in events)
        {
            session = @event switch
            {
                AiSessionStarted started => new MealplanAiSession(
                    started.Id,
                    started.From,
                    started.To,
                    [.. started.RequestedSlots],
                    ImmutableDictionary<(DateOnly, MealSlot), MealId>.Empty,
                    AiSessionStatus.Drafting),
                AiDraftAssignmentSet set => session! with
                {
                    Draft = session!.Draft.SetItem((set.Date, set.Slot), set.MealId)
                },
                AiDraftAssignmentCleared cleared => session! with
                {
                    Draft = session!.Draft.Remove((cleared.Date, cleared.Slot))
                },
                AiSessionApplied => session! with { Status = AiSessionStatus.Applied },
                AiSessionDiscarded => session! with { Status = AiSessionStatus.Discarded },
                _ => session
            };
        }

        return session;
    }
}
