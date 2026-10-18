using buddy.Common;
using buddy.Features.Groups;
using buddy.Features.Guardians;

namespace buddy.Features.HouseRules;

public static class PublishRuleHandler
{
    public static async Task<Result<RuleBookResponse>> Handle(
        PublishRule command,
        IRuleBookEventStore books,
        IGroupEventStore groups,
        IGuardianLinkEventStore guardians,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        var access = await HouseRulesAuthorization.CheckManage(command.Scope, command.UserId, groups, guardians, cancellationToken);

        if (access != HouseRulesAccess.Allowed)
        {
            return access.ToDeniedResult<RuleBookResponse>();
        }

        if (await RuleBooks.ReadAsync(books, command.Scope, cancellationToken) is not { Book: { } book } loaded
            || book.FindRule(command.RuleId) is not { } rule)
        {
            return new Result<RuleBookResponse>.NotFound();
        }

        // Idempotent: a rule with no draft is already what the children read, so nothing is appended.
        if (rule.Draft is not { } draft)
        {
            return new Result<RuleBookResponse>.Success(await RuleBooks.ToResponseAsync(
                command.Scope, HouseRulesAccessTier.Manage, book, command.UserId, groups, guardians, cancellationToken));
        }

        var published = RuleBooks.Publish(command.Scope.BookId, rule, draft, command.UserId, time.GetUtcNow());

        await books.AppendAsync(command.Scope.BookId, [published], cancellationToken);

        return new Result<RuleBookResponse>.Success(await RuleBooks.ToResponseAsync(
            command.Scope, HouseRulesAccessTier.Manage, RuleBook.Replay([.. loaded.Events, published]), command.UserId, groups, guardians, cancellationToken));
    }
}
