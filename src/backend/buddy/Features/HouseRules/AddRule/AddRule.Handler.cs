using buddy.Common;
using buddy.Common.Validation;
using buddy.Features.Groups;
using buddy.Features.Guardians;

using FluentValidation;

namespace buddy.Features.HouseRules;

public static class AddRuleHandler
{
    public static async Task<Result<RuleBookResponse>> Handle(
        AddRule command,
        IValidator<AddRule> validator,
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

        if (await RuleBooks.ReadAsync(books, command.Scope, cancellationToken) is not { } loaded)
        {
            return new Result<RuleBookResponse>.NotFound();
        }

        if (loaded.Book is { } existing && existing.Rules.Count >= RuleBook.MaxRules)
        {
            return new Result<RuleBookResponse>.Validation(
                ValidationProblem.Of($"A rule book can hold at most {RuleBook.MaxRules} rules."));
        }

        var id = command.Scope.BookId;
        var now = DateTimeOffset.UtcNow;
        var added = new RuleAdded(id, RuleId.New(), command.Title, command.Body, command.UserId, now);
        RuleBookEvent[] appended;

        if (loaded.Book is null)
        {
            appended = [new RuleBookStarted(id, command.Scope.Kind, command.Scope.Id, command.UserId, now), added];
            await books.CreateAsync(id, appended, cancellationToken);
        }
        else
        {
            appended = [added];
            await books.AppendAsync(id, appended, cancellationToken);
        }

        var book = RuleBook.Replay([.. loaded.Events, .. appended]);

        return new Result<RuleBookResponse>.Success(await RuleBooks.ToResponseAsync(
            command.Scope, HouseRulesAccessTier.Manage, book, command.UserId, groups, guardians, cancellationToken));
    }
}
