using buddy.Common;
using buddy.Common.Observability;
using buddy.Common.Validation;
using buddy.Features.Guardians;

using FluentValidation;

namespace buddy.Features.Feelings;

public static class ListFeelingCheckInsHandler
{
    public static async Task<Result<FeelingCheckInsResponse>> Handle(
        ListFeelingCheckIns query,
        IValidator<ListFeelingCheckIns> validator,
        IFeelingsJournalEventStore journals,
        IGuardianLinkEventStore guardians,
        ILogger<ListFeelingCheckIns> logger,
        CancellationToken cancellationToken)
    {
        if (await validator.ValidateCommandAsync(query, cancellationToken) is { } problem)
        {
            return new Result<FeelingCheckInsResponse>.Validation(problem);
        }

        var access = await FeelingsAuthorization.CheckManage(query.ChildId, query.UserId, guardians, cancellationToken);

        if (access != FeelingsAccess.Allowed)
        {
            return access.ToDeniedResult<FeelingCheckInsResponse>();
        }

        var journal = await journals.FindSnapshotAsync(FeelingsJournalId.ForChild(query.ChildId), cancellationToken);

        logger.CheckInsRead(query.ChildId.Value, query.From, query.To, query.UserId.Value, HealthDataAccessPath.Guardian);

        return new Result<FeelingCheckInsResponse>.Success(FeelingCheckInsResponse.ForRange(journal, query.ChildId, query.From, query.To));
    }
}
