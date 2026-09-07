using buddy.Common;
using buddy.Features.Guardians;

namespace buddy.Features.Mealplans;

public static class GetCurrentAiSessionHandler
{
    public static async Task<Result<AiSessionView>> Handle(
        GetCurrentAiSession query,
        IAiSessionEventStore sessions,
        IMealEventStore meals,
        IGuardianLinkEventStore guardians,
        CancellationToken cancellationToken)
    {
        if (query.UserId is not { } userId)
        {
            return new Result<AiSessionView>.NotFound();
        }

        var access = await MealplanAuthorization.CheckManage(query.ChildId, userId, guardians, cancellationToken);

        if (access != MealplanAccess.Allowed)
        {
            return access.ToDeniedResult<AiSessionView>();
        }

        var sessionId = await AiSessionResolution.ResolveCurrentSessionIdAsync(query.ChildId, guardians, sessions, cancellationToken);

        if (sessionId is null)
        {
            return new Result<AiSessionView>.NotFound();
        }

        var events = await sessions.ReadAsync(sessionId, cancellationToken);
        var session = MealplanAiSession.Rehydrate(events)!;
        var view = await AiSessionViewBuilder.BuildAsync(session, events, meals, cancellationToken);

        return new Result<AiSessionView>.Success(view);
    }
}
