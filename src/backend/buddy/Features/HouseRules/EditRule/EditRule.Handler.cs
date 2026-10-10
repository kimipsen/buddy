using buddy.Common;
using buddy.Common.Validation;
using buddy.Features.Groups;
using buddy.Features.Guardians;

using FluentValidation;

namespace buddy.Features.HouseRules;

public static class EditRuleHandler
{
    public static async Task<Result<RuleBookResponse>> Handle(
        EditRule command,
        IValidator<EditRule> validator,
        IRuleBookEventStore books,
        IGroupEventStore groups,
        IGuardianLinkEventStore guardians,
        CancellationToken cancellationToken)
    {
        if (await validator.ValidateCommandAsync(command, cancellationToken) is { } problem)
        {
            return new Result<RuleBookResponse>.Validation(problem);
        }

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

        // Identical content appends nothing, whatever RequireReacknowledgement says -- an unchanged
        // rule has nothing new to agree to, and the PUT stays idempotent.
        if (rule.Content == command.Content)
        {
            return new Result<RuleBookResponse>.Success(await RuleBooks.ToResponseAsync(
                command.Scope, HouseRulesAccessTier.Manage, book, command.UserId, groups, guardians, cancellationToken));
        }

        var edited = new RuleEdited(
            command.Scope.BookId,
            rule.Id,
            rule.Content,
            command.Content,
            rule.Revision + 1,
            command.RequireReacknowledgement,
            command.UserId,
            DateTimeOffset.UtcNow);

        await books.AppendAsync(command.Scope.BookId, [edited], cancellationToken);

        return new Result<RuleBookResponse>.Success(await RuleBooks.ToResponseAsync(
            command.Scope, HouseRulesAccessTier.Manage, RuleBook.Replay([.. loaded.Events, edited]), command.UserId, groups, guardians, cancellationToken));
    }
}
