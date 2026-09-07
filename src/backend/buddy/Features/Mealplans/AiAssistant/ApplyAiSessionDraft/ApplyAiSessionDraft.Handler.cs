using buddy.Common;
using buddy.Common.Validation;
using buddy.Features.Guardians;

namespace buddy.Features.Mealplans;

public static class ApplyAiSessionDraftHandler
{
    public static async Task<Result<AiSessionView>> Handle(
        ApplyAiSessionDraft command,
        IAiSessionEventStore sessions,
        IMealPlanEventStore mealPlans,
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
            return new Result<AiSessionView>.Validation(ValidationProblem.Of("This AI session has already been applied or discarded."));
        }

        // Reuses the real MealPlan write path unchanged -- the session's draft never becomes a
        // real assignment any other way, so existing authorization and domain rules are the only
        // path from an AI suggestion to a committed change (see the AI mealplan plan).
        foreach (var entry in session.Draft)
        {
            var assignResult = await AssignMealToSlotHandler.AssignForChildAsync(
                command.ChildId, entry.Key.Date, entry.Key.Slot, entry.Value, "Proposed by the AI assistant.", userId,
                mealPlans, meals, guardians, cancellationToken);

            if (assignResult is not Result<MealPlanEntry>.Success)
            {
                return assignResult.Reraise<MealPlanEntry, AiSessionView>();
            }
        }

        var now = DateTimeOffset.UtcNow;
        await sessions.AppendAsync(sessionId, [new AiSessionApplied(sessionId, userId, now)], cancellationToken);

        MealplanAiSessionEvent[] allEvents = [.. existingEvents, new AiSessionApplied(sessionId, userId, now)];
        var updatedSession = MealplanAiSession.Rehydrate(allEvents)!;
        var view = await AiSessionViewBuilder.BuildAsync(updatedSession, allEvents, meals, cancellationToken);

        return new Result<AiSessionView>.Success(view);
    }
}
