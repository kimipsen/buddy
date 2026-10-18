using buddy.Features.Users;

namespace buddy.Features.HouseRules;

// One rule book as ListRules returns it. Access is the caller's tier, so the client knows whether to
// offer editing. Manage gets every rule with its draft; every other tier only the published rules,
// as if the drafts didn't exist (house-rules-publishing.md, Question 4). Children are the children whose acknowledgements the book tracks: the child of a
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
            book is null ? [] : access == HouseRulesAccessTier.Manage
                ? [.. book.Rules.Select(rule => RuleResponse.From(book, rule, children))]
                : [.. book.PublishedRules.Select(rule => RuleResponse.From(book, rule with { Draft = null }, children))]);
}

// Title/Body/Revision are the published version ("" and 0 until first published). Draft is the
// unpublished change, only ever set for Manage. A never-published rule has no acknowledgements: there
// is nothing for the children to read yet.
public sealed record RuleResponse(
    Guid Id,
    string Title,
    string Body,
    int Revision,
    int AcknowledgementRevision,
    DateTimeOffset LastEditedAt,
    IReadOnlyList<RuleAcknowledgementResponse> Acknowledgements,
    bool IsPublished,
    RuleDraftResponse? Draft)
{
    public static RuleResponse From(RuleBook book, Rule rule, IReadOnlyList<UserId> children) =>
        new(
            rule.Id.Value,
            rule.Title,
            rule.Body,
            rule.Revision,
            rule.AcknowledgementRevision,
            rule.LastEditedAt,
            rule.IsPublished
                ? [.. children.Select(child => new RuleAcknowledgementResponse(
                    child.Value,
                    book.AcknowledgedRevision(rule.Id, child),
                    book.IsUpToDate(rule, child)))]
                : [],
            rule.IsPublished,
            rule.Draft is { } draft ? RuleDraftResponse.From(draft) : null);
}

// RequiresReacknowledgement is the "small fix" switch, inverted: false keeps the children's
// acknowledgements when the draft is published.
public sealed record RuleDraftResponse(string Title, string Body, bool RequiresReacknowledgement, DateTimeOffset EditedAt)
{
    public static RuleDraftResponse From(RuleDraft draft) =>
        new(draft.Content.Title, draft.Content.Body, draft.RequiresReacknowledgement, draft.EditedAt);
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
// Only published rules, with their published text: this is what the child reads.
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
            book is null ? [] : [.. book.PublishedRules.Select(rule => new ChildRuleResponse(
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
