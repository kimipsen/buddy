using buddy.Common;
using buddy.Features.Guardians;

namespace buddy.Features.VisitSummaries;

public static class RevokeVisitSummaryShareLinkHandler
{
    public static async Task<Result<Unit>> Handle(
        RevokeVisitSummaryShareLink command,
        IVisitSummaryEventStore summaries,
        IGuardianLinkEventStore guardians,
        TimeProvider time,
        ILogger<RevokeVisitSummaryShareLink> logger,
        CancellationToken cancellationToken)
    {
        var access = await VisitSummaryAuthorization.CheckManage(command.ChildId, command.UserId, guardians, cancellationToken);

        if (access != VisitSummaryAccess.Allowed)
        {
            return access.ToDeniedResult<Unit>();
        }

        var summary = VisitSummary.Rehydrate(await summaries.ReadAsync(command.SummaryId, cancellationToken));

        // A link of another child's summary is as invisible as one that doesn't exist.
        if (summary is null || summary.ChildId != command.ChildId || summary.FindLink(command.LinkId) is not { } link)
        {
            return new Result<Unit>.NotFound();
        }

        // Revoking twice -- or a link the summary's deletion already revoked -- is an idempotent no-op.
        if (link.IsRevoked)
        {
            return new Result<Unit>.Success(Unit.Value);
        }

        await summaries.AppendAsync(
            summary.Id,
            [new VisitSummaryShareLinkRevoked(summary.Id, link.Id, command.UserId, time.GetUtcNow())],
            cancellationToken);

        logger.ShareLinkRevoked(summary.Id.Value, link.Id.Value, summary.ChildId.Value, command.UserId.Value);

        return new Result<Unit>.Success(Unit.Value);
    }
}
