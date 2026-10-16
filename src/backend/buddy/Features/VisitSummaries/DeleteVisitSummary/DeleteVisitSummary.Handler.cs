using buddy.Common;
using buddy.Features.Guardians;

namespace buddy.Features.VisitSummaries;

public static class DeleteVisitSummaryHandler
{
    public static async Task<Result<Unit>> Handle(
        DeleteVisitSummary command,
        IVisitSummaryEventStore summaries,
        IGuardianLinkEventStore guardians,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        var access = await VisitSummaryAuthorization.CheckManage(command.ChildId, command.UserId, guardians, cancellationToken);

        if (access != VisitSummaryAccess.Allowed)
        {
            return access.ToDeniedResult<Unit>();
        }

        var summary = VisitSummary.Rehydrate(await summaries.ReadAsync(command.SummaryId, cancellationToken));

        if (summary is null || summary.ChildId != command.ChildId)
        {
            return new Result<Unit>.NotFound();
        }

        // Deleting twice is an idempotent no-op. The event also revokes every link.
        if (!summary.IsDeleted)
        {
            await summaries.AppendAsync(summary.Id, [new VisitSummaryDeleted(summary.Id, command.UserId, time.GetUtcNow())], cancellationToken);
        }

        return new Result<Unit>.Success(Unit.Value);
    }
}
