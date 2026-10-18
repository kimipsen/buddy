using buddy.Common;
using buddy.Common.Validation;
using buddy.Features.Groups;
using buddy.Features.Guardians;

namespace buddy.Features.HouseRules;

public static class DiscardRuleDraftHandler
{
    public static async Task<Result<Unit>> Handle(
        DiscardRuleDraft command,
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

        if (await RuleBooks.ReadAsync(books, command.Scope, cancellationToken) is not { Book: { } book }
            || book.FindRule(command.RuleId) is not { } rule)
        {
            return new Result<Unit>.NotFound();
        }

        // A rule that was never published has nothing to go back to. Discarding it would remove the
        // whole rule, which RemoveRule says plainly (house-rules-publishing.md, Question 3).
        if (!rule.IsPublished)
        {
            return new Result<Unit>.Validation(ValidationProblem.Of(
                "The rule has never been published, so there is no published version to go back to. Remove the rule instead."));
        }

        // Idempotent DELETE: no draft appends nothing.
        if (rule.Draft is { } draft)
        {
            await books.AppendAsync(
                command.Scope.BookId,
                [new RuleDraftDiscarded(command.Scope.BookId, rule.Id, draft.Content, command.UserId, time.GetUtcNow())],
                cancellationToken);
        }

        return new Result<Unit>.Success(Unit.Value);
    }
}
