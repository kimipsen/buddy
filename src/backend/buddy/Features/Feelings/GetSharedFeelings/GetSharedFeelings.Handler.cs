using buddy.Common;
using buddy.Common.Validation;
using buddy.Features.Users;

using FluentValidation;

namespace buddy.Features.Feelings;

public static class GetSharedFeelingsHandler
{
    public static async Task<Result<SharedFeelings>> Handle(
        GetSharedFeelings query,
        IValidator<GetSharedFeelings> validator,
        IFeelingsShareTokenEventStore shareTokens,
        IFeelingsJournalEventStore journals,
        IUserEventStore users,
        TimeProvider time,
        ILogger<GetSharedFeelings> logger,
        CancellationToken cancellationToken)
    {
        if (await validator.ValidateCommandAsync(query, cancellationToken) is { } problem)
        {
            return new Result<SharedFeelings>.Validation(problem);
        }

        var link = await shareTokens.FindByHashAsync(FeelingsShareSecret.Hash(query.Token), cancellationToken);

        // Unknown, revoked and expired all look the same to an anonymous reader, by design.
        if (link is null || !FeelingsShareLinks.IsLive(link, time.GetUtcNow()))
        {
            return new Result<SharedFeelings>.NotFound();
        }

        var childId = new UserId(link.ChildId);
        var child = await users.FindSnapshotAsync(childId, cancellationToken);

        if (child is null || child.IsDeleted)
        {
            return new Result<SharedFeelings>.NotFound();
        }

        var now = ChildLocalClock.Now(child, time);

        var to = query.To ?? query.From?.AddDays(FeelingsRules.SharedViewDefaultDays - 1) ?? now.Date;
        var from = query.From ?? to.AddDays(-(FeelingsRules.SharedViewDefaultDays - 1));

        // Current data on every request, not a copy taken when the link was made.
        var journal = await journals.FindSnapshotAsync(FeelingsJournalId.ForChild(childId), cancellationToken);
        var checkIns = FeelingCheckInsResponse.ForRange(journal, childId, from, to).CheckIns;

        logger.SharedFeelingsViewed(link.ChildId, link.Id);

        return new Result<SharedFeelings>.Success(new SharedFeelings(child.Name, from, to, link.ExpiresAt, checkIns));
    }
}
