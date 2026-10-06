using buddy.Common;
using buddy.Common.Validation;
using buddy.Features.Users;

using FluentValidation;

namespace buddy.Features.SleepDiaries;

public static class GetSharedSleepDiaryHandler
{
    public static async Task<Result<SharedSleepDiary>> Handle(
        GetSharedSleepDiary query,
        IValidator<GetSharedSleepDiary> validator,
        ISleepDiaryShareTokenEventStore shareTokens,
        ISleepDiaryEventStore diaries,
        IUserEventStore users,
        ILogger<GetSharedSleepDiary> logger,
        CancellationToken cancellationToken)
    {
        if (await validator.ValidateCommandAsync(query, cancellationToken) is { } problem)
        {
            return new Result<SharedSleepDiary>.Validation(problem);
        }

        var link = await shareTokens.FindByHashAsync(SleepDiaryShareSecret.Hash(query.Token), cancellationToken);

        // Unknown, revoked and expired all look the same to an anonymous reader, by design.
        if (link is null || !SleepDiaryShareLinks.IsLive(link, DateTimeOffset.UtcNow))
        {
            return new Result<SharedSleepDiary>.NotFound();
        }

        var childId = new UserId(link.ChildId);
        var child = await users.FindSnapshotAsync(childId, cancellationToken);

        if (child is null || child.IsDeleted)
        {
            return new Result<SharedSleepDiary>.NotFound();
        }

        // Current data on every request, not a copy taken when the link was made.
        var diary = await diaries.FindSnapshotAsync(SleepDiaryId.ForChild(childId), cancellationToken);

        logger.SharedDiaryViewed(link.ChildId, link.Id);

        return new Result<SharedSleepDiary>.Success(new SharedSleepDiary(
            child.Name,
            query.From,
            query.To,
            link.ExpiresAt,
            SleepDiaryRange.From(diary, query.From, query.To)));
    }
}
