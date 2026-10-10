using buddy.Common;
using buddy.Features.Groups;
using buddy.Features.Guardians;

namespace buddy.Features.HouseRules;

public static class RemoveRuleHandler
{
    public static async Task<Result<Unit>> Handle(
        RemoveRule command,
        IRuleBookEventStore books,
        IGroupEventStore groups,
        IGuardianLinkEventStore guardians,
        CancellationToken cancellationToken)
    {
        var access = await HouseRulesAuthorization.CheckManage(command.Scope, command.UserId, groups, guardians, cancellationToken);

        if (access != HouseRulesAccess.Allowed)
        {
            return access.ToDeniedResult<Unit>();
        }

        if (await RuleBooks.ReadAsync(books, command.Scope, cancellationToken) is not { } loaded)
        {
            return new Result<Unit>.NotFound();
        }

        // Idempotent DELETE: a rule that's already gone (or a book never started) appends nothing.
        if (loaded.Book?.FindRule(command.RuleId) is { } rule)
        {
            await books.AppendAsync(
                command.Scope.BookId,
                [new RuleRemoved(command.Scope.BookId, rule.Id, rule.Content, command.UserId, DateTimeOffset.UtcNow)],
                cancellationToken);
        }

        return new Result<Unit>.Success(Unit.Value);
    }
}
