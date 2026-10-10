using buddy.Common.Validation;
using buddy.Features.Groups;
using buddy.Features.Guardians;
using buddy.Features.Users;

using FluentValidation;

namespace buddy.Features.HouseRules;

public static class AcknowledgeRuleHandler
{
    public static async Task<AcknowledgeRuleOutcome> Handle(
        AcknowledgeRule command,
        IValidator<AcknowledgeRule> validator,
        IRuleBookEventStore books,
        IGroupEventStore groups,
        IGuardianLinkEventStore guardians,
        CancellationToken cancellationToken)
    {
        if (await validator.ValidateCommandAsync(command, cancellationToken) is { } problem)
        {
            return new AcknowledgeRuleOutcome.Validation(problem);
        }

        var tier = await HouseRulesAuthorization.ResolveTierAsync(command.Scope, command.UserId, groups, guardians, cancellationToken);

        if (await ResolveChildAsync(command, tier, groups, guardians, cancellationToken) is not UserId childId)
        {
            return tier switch
            {
                HouseRulesAccessTier.None => new AcknowledgeRuleOutcome.NotFound(),
                HouseRulesAccessTier.Manage when command.ChildId is null => new AcknowledgeRuleOutcome.Validation(
                    ValidationProblem.Of("childId is required to acknowledge a rule on a child's behalf.")),
                _ => new AcknowledgeRuleOutcome.Forbidden(),
            };
        }

        if (await RuleBooks.ReadAsync(books, command.Scope, cancellationToken) is not { Book: { } book }
            || book.FindRule(command.RuleId) is not { } rule)
        {
            return new AcknowledgeRuleOutcome.NotFound();
        }

        if (command.Revision > rule.Revision)
        {
            return new AcknowledgeRuleOutcome.Validation(
                ValidationProblem.Of($"The rule has no revision {command.Revision}; its current revision is {rule.Revision}."));
        }

        // Stale: the rule was edited in a way that needs re-acknowledging after the revision the child
        // read. Recording it anyway would mean agreeing to text they never saw (house-rules.md,
        // Question 6). A minor edit since then doesn't count: AcknowledgementRevision didn't move.
        if (command.Revision < rule.AcknowledgementRevision)
        {
            return new AcknowledgeRuleOutcome.RevisionChanged(rule.Revision);
        }

        // Idempotent PUT: the same (or an older, still-valid) revision again appends nothing.
        if (book.AcknowledgedRevision(rule.Id, childId) is { } acknowledged && acknowledged >= command.Revision)
        {
            return new AcknowledgeRuleOutcome.Success();
        }

        await books.AppendAsync(
            command.Scope.BookId,
            [new RuleAcknowledged(command.Scope.BookId, rule.Id, childId, command.Revision, command.UserId, DateTimeOffset.UtcNow)],
            cancellationToken);

        return new AcknowledgeRuleOutcome.Success();
    }

    // Whose acknowledgement this is, or null when the caller may not record it:
    //   the child themself (Acknowledge tier), for no one but themself;
    //   a Manage caller on behalf of a child in the book's scope whom they're an active guardian of
    //   -- a household admin can't tick for another family's child.
    private static async Task<UserId?> ResolveChildAsync(
        AcknowledgeRule command,
        HouseRulesAccessTier tier,
        IGroupEventStore groups,
        IGuardianLinkEventStore guardians,
        CancellationToken cancellationToken)
    {
        switch (tier)
        {
            case HouseRulesAccessTier.Acknowledge:
                return command.ChildId is null || command.ChildId == command.UserId ? command.UserId : null;

            case HouseRulesAccessTier.Manage when command.ChildId is { } childId:
                var children = await HouseRulesAuthorization.ListChildrenInScopeAsync(command.Scope, groups, guardians, cancellationToken);
                var isGuardian = await guardians.FindActiveLinkAsync(childId, command.UserId, cancellationToken) is not null;

                return children.Contains(childId) && isGuardian ? childId : null;

            default:
                return null;
        }
    }
}
