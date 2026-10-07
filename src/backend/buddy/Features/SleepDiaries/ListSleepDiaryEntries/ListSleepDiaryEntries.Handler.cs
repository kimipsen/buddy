using buddy.Common;
using buddy.Common.Observability;
using buddy.Common.Validation;
using buddy.Features.Guardians;

using FluentValidation;

namespace buddy.Features.SleepDiaries;

public static class ListSleepDiaryEntriesHandler
{
    // Wider than the 31 days of the schedule-expansion lists: there's nothing to expand here (one
    // snapshot read, then a filter), and a clinician typically reviews a few 14-day rounds at once.
    public const int MaxRangeDays = 92;

    public static async Task<Result<SleepDiaryRange>> Handle(
        ListSleepDiaryEntries query,
        IValidator<ListSleepDiaryEntries> validator,
        ISleepDiaryEventStore diaries,
        IGuardianLinkEventStore guardians,
        ILogger<ListSleepDiaryEntries> logger,
        CancellationToken cancellationToken)
    {
        if (await validator.ValidateCommandAsync(query, cancellationToken) is { } problem)
        {
            return new Result<SleepDiaryRange>.Validation(problem);
        }

        var access = await SleepDiaryAuthorization.CheckManage(query.ChildId, query.UserId, guardians, cancellationToken);

        if (access != SleepDiaryAccess.Allowed)
        {
            return access.ToDeniedResult<SleepDiaryRange>();
        }

        var diary = await diaries.FindSnapshotAsync(SleepDiaryId.ForChild(query.ChildId), cancellationToken);

        logger.SleepDiaryEntriesRead(query.ChildId.Value, query.From, query.To, query.UserId.Value, HealthDataAccessPath.Guardian);

        return new Result<SleepDiaryRange>.Success(SleepDiaryRange.From(diary, query.From, query.To));
    }
}
