using buddy.Common;
using buddy.Features.Groups;
using buddy.Features.Guardians;

namespace buddy.Features.HouseRules;

public static class PublishRulesHandler
{
    // Publishes every draft in the book in one append, so the children never see half a release
    // (house-rules-publishing.md, Question 2). A scope with no book yet, or no drafts, appends nothing.
    public static async Task<Result<RuleBookResponse>> Handle(
        PublishRules command,
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

        if (await RuleBooks.ReadAsync(books, command.Scope, cancellationToken) is not { } loaded)
        {
            return new Result<RuleBookResponse>.NotFound();
        }

        var now = time.GetUtcNow();
        RuleBookEvent[] published = loaded.Book is { } book
            ? [.. book.Rules.SelectMany(rule => rule.Draft is { } draft
                ? [RuleBooks.Publish(command.Scope.BookId, rule, draft, command.UserId, now)]
                : Array.Empty<RulePublished>())]
            : [];

        if (published.Length == 0)
        {
            return new Result<RuleBookResponse>.Success(await RuleBooks.ToResponseAsync(
                command.Scope, HouseRulesAccessTier.Manage, loaded.Book, command.UserId, groups, guardians, cancellationToken));
        }

        await books.AppendAsync(command.Scope.BookId, published, cancellationToken);

        return new Result<RuleBookResponse>.Success(await RuleBooks.ToResponseAsync(
            command.Scope, HouseRulesAccessTier.Manage, RuleBook.Replay([.. loaded.Events, .. published]), command.UserId, groups, guardians, cancellationToken));
    }
}
