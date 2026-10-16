using buddy.Common;
using buddy.Common.Validation;
using buddy.Features.Guardians;

using FluentValidation;

namespace buddy.Features.VisitSummaries;

public static class UpdateVisitSummaryHandler
{
    public static async Task<Result<VisitSummaryResponse>> Handle(
        UpdateVisitSummary command,
        IValidator<UpdateVisitSummary> validator,
        IVisitSummaryEventStore summaries,
        IGuardianLinkEventStore guardians,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        if (await validator.ValidateCommandAsync(command, cancellationToken) is { } problem)
        {
            return new Result<VisitSummaryResponse>.Validation(problem);
        }

        var access = await VisitSummaryAuthorization.CheckManage(command.ChildId, command.UserId, guardians, cancellationToken);

        if (access != VisitSummaryAccess.Allowed)
        {
            return access.ToDeniedResult<VisitSummaryResponse>();
        }

        var events = (await summaries.ReadAsync(command.SummaryId, cancellationToken)).ToList();

        if (VisitSummary.Rehydrate(events) is not { IsDeleted: false } summary || summary.ChildId != command.ChildId)
        {
            return new Result<VisitSummaryResponse>.NotFound();
        }

        var now = time.GetUtcNow();

        // Saving the form unchanged is a no-op, not a new event.
        if (!summary.Details.HasSameContentAs(command.Details))
        {
            VisitSummaryEvent[] appended = [new VisitSummaryUpdated(summary.Id, summary.Details, command.Details, command.UserId, now)];
            await summaries.AppendAsync(summary.Id, appended, cancellationToken);
            events.AddRange(appended);
        }

        return new Result<VisitSummaryResponse>.Success(VisitSummaryResponse.FromSummary(VisitSummary.Replay(events), now));
    }
}
