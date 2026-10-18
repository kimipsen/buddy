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
        TimeProvider time,
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

        // EditRule saves a draft; the children keep reading the published text until PublishRule
        // (house-rules-publishing.md, Question 3).
        if (DraftChange(rule, command, time.GetUtcNow()) is not { } change)
        {
            return new Result<RuleBookResponse>.Success(await RuleBooks.ToResponseAsync(
                command.Scope, HouseRulesAccessTier.Manage, book, command.UserId, groups, guardians, cancellationToken));
        }

        await books.AppendAsync(command.Scope.BookId, [change], cancellationToken);

        return new Result<RuleBookResponse>.Success(await RuleBooks.ToResponseAsync(
            command.Scope, HouseRulesAccessTier.Manage, RuleBook.Replay([.. loaded.Events, change]), command.UserId, groups, guardians, cancellationToken));
    }

    // Null when nothing changed: the same content and switch as the draft, or the published text on a
    // published rule with no draft -- the PUT stays idempotent. Restoring the published text drops the
    // draft rather than keeping one that would bump the revision of an unchanged rule.
    private static RuleBookEvent? DraftChange(Rule rule, EditRule command, DateTimeOffset now)
    {
        if (rule.IsPublished && command.Content == rule.Content)
        {
            if (rule.Draft is not { } draft)
            {
                return null;
            }

            return new RuleDraftDiscarded(command.Scope.BookId, rule.Id, draft.Content, command.UserId, now);
        }

        if (rule.Draft is { } current
            && current.Content == command.Content
            && current.RequiresReacknowledgement == command.RequireReacknowledgement)
        {
            return null;
        }

        return new RuleDraftEdited(
            command.Scope.BookId,
            rule.Id,
            rule.WorkingContent,
            command.Content,
            command.RequireReacknowledgement,
            command.UserId,
            now);
    }
}
