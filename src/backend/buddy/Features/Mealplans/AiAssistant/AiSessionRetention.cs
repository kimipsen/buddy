using buddy.Common.Erasure;

namespace buddy.Features.Mealplans;

// GDPR Question 6.2 (docs/backend/analysis/gdpr-data-protection.md): 30 days after a session's
// last activity -- applying or discarding it, or the last message of one that was never closed --
// its conversation is masked with the store's masking rules (MealplansPersonalDataEraser): the
// notes, both sides of the chat and the tool calls' arguments and results. A session still drafting
// is closed first (AiSessionExpired). The draft and the meal plan it produced stay.
public sealed class AiSessionRetention(IMealplansStore store, IAiSessionEventStore sessions, ILogger<AiSessionRetention> logger)
{
    public static readonly TimeSpan RetentionPeriod = TimeSpan.FromDays(30);

    // Returns how many sessions it erased. Idempotent: an erased session is never a candidate again.
    public async Task<int> EraseExpiredAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var inactiveSince = now - RetentionPeriod;
        var erased = 0;

        foreach (var index in await sessions.ListRetentionCandidatesAsync(inactiveSince, cancellationToken))
        {
            // The stream decides, not the index row: a row from before retention existed has no
            // LastActivityAt and was picked by its StartedAt.
            var events = await sessions.ReadAsync(new MealplanAiSessionId(index.Id), cancellationToken);

            if (events.Count == 0)
            {
                continue;
            }

            var lastActivityAt = events.Max(e => e.OccurredAt);

            if (lastActivityAt > inactiveSince)
            {
                await sessions.UpdateIndexAsync(index with { LastActivityAt = lastActivityAt }, cancellationToken);
                continue;
            }

            // A session nobody closed is closed first, so its erased history can't be continued.
            if (MealplanAiSession.Replay(events).Status == AiSessionStatus.Drafting)
            {
                var sessionId = new MealplanAiSessionId(index.Id);
                await sessions.AppendAsync(sessionId, [new AiSessionExpired(sessionId, now)], cancellationToken);
                lastActivityAt = now;
            }

            await store.MaskStreamAsync<MealplanAiSessionSnapshot>(index.Id, cancellationToken);
            await sessions.UpdateIndexAsync(index with { LastActivityAt = lastActivityAt, ContentErasedAt = now }, cancellationToken);

            logger.AiSessionContentErased(index.Id, (int)RetentionPeriod.TotalDays);
            erased++;
        }

        return erased;
    }
}
