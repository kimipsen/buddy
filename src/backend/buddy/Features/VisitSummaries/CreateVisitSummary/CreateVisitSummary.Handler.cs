using buddy.Common;
using buddy.Common.Validation;
using buddy.Features.Guardians;

using FluentValidation;

namespace buddy.Features.VisitSummaries;

public static class CreateVisitSummaryHandler
{
    public static async Task<Result<VisitSummaryResponse>> Handle(
        CreateVisitSummary command,
        IValidator<CreateVisitSummary> validator,
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

        var id = VisitSummaryId.New();
        var now = time.GetUtcNow();
        VisitSummaryEvent[] events = [new VisitSummaryCreated(id, command.ChildId, command.Details, command.UserId, now)];

        await summaries.CreateAsync(id, events, cancellationToken);

        return new Result<VisitSummaryResponse>.Success(VisitSummaryResponse.FromSummary(VisitSummary.Replay(events), now));
    }
}
