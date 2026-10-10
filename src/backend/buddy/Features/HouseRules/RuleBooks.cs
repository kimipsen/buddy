using buddy.Features.Groups;
using buddy.Features.Guardians;
using buddy.Features.Users;

namespace buddy.Features.HouseRules;

// Events read for a command (empty for a scope with no book yet) and the book they fold to.
public sealed record LoadedRuleBook(IReadOnlyCollection<RuleBookEvent> Events, RuleBook? Book);

// Loading shared by every slice. RuleBookId.Value is the scope's id whichever kind the scope is, so
// a book whose stored ScopeKind doesn't match the route's kind (a child id that happens to be a
// group's id) is treated as missing -- the callers map null to NotFound (house-rules.md, Question 2).
public static class RuleBooks
{
    public static async Task<LoadedRuleBook?> ReadAsync(IRuleBookEventStore books, RuleBookScope scope, CancellationToken cancellationToken)
    {
        var events = await books.ReadAsync(scope.BookId, cancellationToken);
        var book = RuleBook.Rehydrate(events);

        return book is not null && book.ScopeKind != scope.Kind ? null : new LoadedRuleBook(events, book);
    }

    // Read-only: from the snapshot. Null both for "no book yet" and for a kind mismatch -- either way
    // the scope has no rules.
    public static async Task<RuleBook?> FindSnapshotAsync(IRuleBookEventStore books, RuleBookScope scope, CancellationToken cancellationToken)
    {
        var book = await books.FindSnapshotAsync(scope.BookId, cancellationToken);

        return book is not null && book.ScopeKind == scope.Kind ? book : null;
    }

    // A child sees only their own acknowledgement status; Manage and View see every child's.
    public static async Task<RuleBookResponse> ToResponseAsync(
        RuleBookScope scope,
        HouseRulesAccessTier tier,
        RuleBook? book,
        UserId callerId,
        IGroupEventStore groups,
        IGuardianLinkEventStore guardians,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<UserId> children = tier == HouseRulesAccessTier.Acknowledge
            ? [callerId]
            : await HouseRulesAuthorization.ListChildrenInScopeAsync(scope, groups, guardians, cancellationToken);

        return RuleBookResponse.From(scope, tier, book, children);
    }
}
