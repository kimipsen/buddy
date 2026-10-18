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
        TimeProvider time,
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
        // Removal isn't drafted: the rule and any draft of it are gone for everyone at once
        // (house-rules-publishing.md, Question 1).
        if (loaded.Book?.FindRule(command.RuleId) is { } rule)
        {
            await books.AppendAsync(
                command.Scope.BookId,
                [new RuleRemoved(command.Scope.BookId, rule.Id, rule.WorkingContent, command.UserId, time.GetUtcNow())],
                cancellationToken);
        }

        return new Result<Unit>.Success(Unit.Value);
    }
}
