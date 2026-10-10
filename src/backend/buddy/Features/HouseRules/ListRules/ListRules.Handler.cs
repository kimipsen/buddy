using buddy.Common;
using buddy.Features.Groups;
using buddy.Features.Guardians;

namespace buddy.Features.HouseRules;

public static class ListRulesHandler
{
    // Any tier may read. A scope with no rules yet is an empty list, not NotFound.
    public static async Task<Result<RuleBookResponse>> Handle(
        ListRules command,
        IRuleBookEventStore books,
        IGroupEventStore groups,
        IGuardianLinkEventStore guardians,
        CancellationToken cancellationToken)
    {
        var tier = await HouseRulesAuthorization.ResolveTierAsync(command.Scope, command.UserId, groups, guardians, cancellationToken);

        if (tier == HouseRulesAccessTier.None)
        {
            return new Result<RuleBookResponse>.NotFound();
        }

        var book = await RuleBooks.FindSnapshotAsync(books, command.Scope, cancellationToken);

        return new Result<RuleBookResponse>.Success(await RuleBooks.ToResponseAsync(
            command.Scope, tier, book, command.UserId, groups, guardians, cancellationToken));
    }
}
