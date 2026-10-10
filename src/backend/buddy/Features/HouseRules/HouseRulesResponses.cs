using buddy.Features.Users;

namespace buddy.Features.HouseRules;

// One rule book as ListRules returns it. Access is the caller's tier, so the client knows whether to
// offer editing. Children are the children whose acknowledgements the book tracks: the child of a
// personal book, or the child members of a household group -- only the caller themself when the
// caller is a child.
public sealed record RuleBookResponse(
    RuleBookScopeKind ScopeKind,
    Guid ScopeId,
    HouseRulesAccessTier Access,
    IReadOnlyList<Guid> Children,
    IReadOnlyList<RuleResponse> Rules)
{
    public static RuleBookResponse From(RuleBookScope scope, HouseRulesAccessTier access, RuleBook? book, IReadOnlyList<UserId> children) =>
        new(
            scope.Kind,
            scope.Id,
            access,
            [.. children.Select(c => c.Value)],
            book is null ? [] : [.. book.Rules.Select(rule => RuleResponse.From(book, rule, children))]);
}

public sealed record RuleResponse(
    Guid Id,
    string Title,
    string Body,
    int Revision,
    int AcknowledgementRevision,
    DateTimeOffset LastEditedAt,
    IReadOnlyList<RuleAcknowledgementResponse> Acknowledgements)
{
    public static RuleResponse From(RuleBook book, Rule rule, IReadOnlyList<UserId> children) =>
        new(
            rule.Id.Value,
            rule.Title,
            rule.Body,
            rule.Revision,
            rule.AcknowledgementRevision,
            rule.LastEditedAt,
            [.. children.Select(child => new RuleAcknowledgementResponse(
                child.Value,
                book.AcknowledgedRevision(rule.Id, child),
                book.IsUpToDate(rule, child)))]);
}

// AcknowledgedRevision is null when the child has never acknowledged the rule (the client shows
// "New"); otherwise IsUpToDate false means "Changed".
public sealed record RuleAcknowledgementResponse(Guid ChildId, int? AcknowledgedRevision, bool IsUpToDate);

// Everything one child is asked to keep: their personal rules, then one section per household group
// they're a member of, ordered by group name (house-rules.md, Question 7).
public sealed record ChildRulesResponse(
    Guid ChildId,
    ChildRuleSectionResponse Personal,
    IReadOnlyList<ChildRuleSectionResponse> Households,
    int PendingAcknowledgements);

// Label is the child's given name for the personal section and the group's name for a household.
public sealed record ChildRuleSectionResponse(
    RuleBookScopeKind ScopeKind,
    Guid ScopeId,
    string Label,
    IReadOnlyList<ChildRuleResponse> Rules)
{
    public static ChildRuleSectionResponse From(RuleBookScope scope, string label, RuleBook? book, UserId childId) =>
        new(
            scope.Kind,
            scope.Id,
            label,
            book is null ? [] : [.. book.Rules.Select(rule => new ChildRuleResponse(
                rule.Id.Value,
                rule.Title,
                rule.Body,
                rule.Revision,
                book.AcknowledgedRevision(rule.Id, childId),
                book.IsUpToDate(rule, childId),
                rule.LastEditedAt))]);
}

public sealed record ChildRuleResponse(
    Guid Id,
    string Title,
    string Body,
    int Revision,
    int? AcknowledgedRevision,
    bool IsUpToDate,
    DateTimeOffset LastEditedAt);
