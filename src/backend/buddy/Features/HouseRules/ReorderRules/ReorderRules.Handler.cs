using System.Collections.Immutable;

using buddy.Common;
using buddy.Common.Validation;
using buddy.Features.Groups;
using buddy.Features.Guardians;

namespace buddy.Features.HouseRules;

public static class ReorderRulesHandler
{
    public static async Task<Result<RuleBookResponse>> Handle(
        ReorderRules command,
        IRuleBookEventStore books,
        IGroupEventStore groups,
        IGuardianLinkEventStore guardians,
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

        ImmutableList<RuleId> before = loaded.Book is { } book ? [.. book.Rules.Select(r => r.Id)] : [];

        // State-dependent, so handler code rather than a validator rule (same as
        // ReorderSubtasksHandler): NewOrder must be exactly a permutation of the current rules, or
        // the RulesReordered fold invariant breaks.
        if (command.NewOrder.Count != before.Count || !before.ToHashSet().SetEquals(command.NewOrder))
        {
            return new Result<RuleBookResponse>.Validation(
                ValidationProblem.Of("NewOrder must contain exactly the book's current rules, each exactly once."));
        }

        if (loaded.Book is null || before.SequenceEqual(command.NewOrder))
        {
            return new Result<RuleBookResponse>.Success(await RuleBooks.ToResponseAsync(
                command.Scope, HouseRulesAccessTier.Manage, loaded.Book, command.UserId, groups, guardians, cancellationToken));
        }

        var reordered = new RulesReordered(command.Scope.BookId, before, command.NewOrder, command.UserId, DateTimeOffset.UtcNow);

        await books.AppendAsync(command.Scope.BookId, [reordered], cancellationToken);

        return new Result<RuleBookResponse>.Success(await RuleBooks.ToResponseAsync(
            command.Scope, HouseRulesAccessTier.Manage, RuleBook.Replay([.. loaded.Events, reordered]), command.UserId, groups, guardians, cancellationToken));
    }
}
