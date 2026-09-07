using buddy.Common;
using buddy.Features.Guardians;

namespace buddy.Features.Mealplans;

public static class DiscardAiSessionHandler
{
    public static async Task<Result<AiSessionView>> Handle(
        DiscardAiSession command,
        IAiSessionEventStore sessions,
        IMealEventStore meals,
        IGuardianLinkEventStore guardians,
        CancellationToken cancellationToken)
    {
        if (command.UserId is not { } userId)
        {
            return new Result<AiSessionView>.NotFound();
        }

        var access = await MealplanAuthorization.CheckManage(command.ChildId, userId, guardians, cancellationToken);

        if (access != MealplanAccess.Allowed)
        {
            return access.ToDeniedResult<AiSessionView>();
        }

        var sessionId = await AiSessionResolution.ResolveCurrentSessionIdAsync(command.ChildId, guardians, sessions, cancellationToken);

        if (sessionId is null)
        {
            return new Result<AiSessionView>.NotFound();
        }

        var existingEvents = await sessions.ReadAsync(sessionId, cancellationToken);
        var session = MealplanAiSession.Rehydrate(existingEvents)!;

        if (session.Status != AiSessionStatus.Drafting)
        {
            // Discarding an already-applied/discarded session is a harmless no-op, same rationale
            // as ClearMealSlot tolerating a double-tap clear.
            var currentView = await AiSessionViewBuilder.BuildAsync(session, existingEvents, meals, cancellationToken);
            return new Result<AiSessionView>.Success(currentView);
        }

        var now = DateTimeOffset.UtcNow;
        await sessions.AppendAsync(sessionId, [new AiSessionDiscarded(sessionId, userId, now)], cancellationToken);

        MealplanAiSessionEvent[] allEvents = [.. existingEvents, new AiSessionDiscarded(sessionId, userId, now)];
        var updatedSession = MealplanAiSession.Rehydrate(allEvents)!;
        var view = await AiSessionViewBuilder.BuildAsync(updatedSession, allEvents, meals, cancellationToken);

        return new Result<AiSessionView>.Success(view);
    }
}
