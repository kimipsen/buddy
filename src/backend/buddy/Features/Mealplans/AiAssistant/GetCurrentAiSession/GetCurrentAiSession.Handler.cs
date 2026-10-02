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
        var userId = query.UserId;

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

        // The transcript AiSessionViewBuilder builds is deliberately not part of MealplanAiSession
        // itself (see MealplanAiSession.Advance) -- it's derived on demand from the raw event stream,
        // so that read still goes through ReadAsync. Only the session's own current-state fields
        // (Draft/Status/...) are cut over to the snapshot.
        var events = await sessions.ReadAsync(sessionId, cancellationToken);
        var session = await sessions.FindSnapshotAsync(sessionId, cancellationToken)
            ?? throw new InvalidOperationException($"No snapshot for sessionId {sessionId}, although its index says the stream exists.");
        var view = await AiSessionViewBuilder.BuildAsync(session, events, meals, cancellationToken);

        return new Result<AiSessionView>.Success(view);
    }
}
