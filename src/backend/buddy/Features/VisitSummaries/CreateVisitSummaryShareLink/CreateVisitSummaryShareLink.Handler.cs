using buddy.Common;
using buddy.Common.Security;
using buddy.Common.Validation;
using buddy.Features.Guardians;

using FluentValidation;

namespace buddy.Features.VisitSummaries;

public static class CreateVisitSummaryShareLinkHandler
{
    public static async Task<Result<IssuedVisitSummaryShareLink>> Handle(
        CreateVisitSummaryShareLink command,
        IValidator<CreateVisitSummaryShareLink> validator,
        IVisitSummaryEventStore summaries,
        IGuardianLinkEventStore guardians,
        TimeProvider time,
        ILogger<CreateVisitSummaryShareLink> logger,
        CancellationToken cancellationToken)
    {
        if (await validator.ValidateCommandAsync(command, cancellationToken) is { } problem)
        {
            return new Result<IssuedVisitSummaryShareLink>.Validation(problem);
        }

        var access = await VisitSummaryAuthorization.CheckManage(command.ChildId, command.UserId, guardians, cancellationToken);

        if (access != VisitSummaryAccess.Allowed)
        {
            return access.ToDeniedResult<IssuedVisitSummaryShareLink>();
        }

        var summary = VisitSummary.Rehydrate(await summaries.ReadAsync(command.SummaryId, cancellationToken));

        if (summary is not { IsDeleted: false } || summary.ChildId != command.ChildId)
        {
            return new Result<IssuedVisitSummaryShareLink>.NotFound();
        }

        var now = time.GetUtcNow();

        if (summary.LiveLinks(now).Count >= VisitSummaryRules.MaxLiveLinks)
        {
            return new Result<IssuedVisitSummaryShareLink>.Validation(ValidationProblem.Of(
                $"A summary can have at most {VisitSummaryRules.MaxLiveLinks} live links. Revoke one first."));
        }

        var (token, hash) = ShareSecret.Generate();
        var linkId = VisitSummaryShareLinkId.New();

        await summaries.AppendAsync(
            summary.Id,
            [new VisitSummaryShareLinkCreated(summary.Id, linkId, hash, command.ExpiresAt, command.UserId, now)],
            cancellationToken);

        logger.ShareLinkCreated(summary.Id.Value, linkId.Value, summary.ChildId.Value, command.UserId.Value, command.ExpiresAt);

        return new Result<IssuedVisitSummaryShareLink>.Success(new IssuedVisitSummaryShareLink(linkId, token, now, command.ExpiresAt));
    }
}
