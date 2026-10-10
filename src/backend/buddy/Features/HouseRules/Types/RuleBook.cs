using System.Collections.Immutable;

using buddy.Common.Aggregates;
using buddy.Features.Users;

namespace buddy.Features.HouseRules;

// One ordered list of short rules per scope (a child's personal rules or a household group's), plus
// which revision of each rule each child has acknowledged. See docs/backend/analysis/house-rules.md.
public sealed record RuleBook(
    RuleBookId Id,
    RuleBookScopeKind ScopeKind,
    Guid ScopeId,
    ImmutableList<Rule> Rules,
    ImmutableDictionary<(RuleId Rule, UserId Child), int> Acknowledgements,
    UserId LastModifiedBy)
{
    public const int MaxRules = 50;

    public static RuleBook? Rehydrate(IEnumerable<RuleBookEvent> events) => EventReplay.Rehydrate(events, Start, Advance);

    public static RuleBook Replay(IEnumerable<RuleBookEvent> events) => EventReplay.Replay(events, Start, Advance);

    public Rule? FindRule(RuleId ruleId) => Rules.Find(r => r.Id == ruleId);

    public int? AcknowledgedRevision(RuleId ruleId, UserId childId) =>
        Acknowledgements.TryGetValue((ruleId, childId), out var revision) ? revision : null;

    public bool IsUpToDate(Rule rule, UserId childId) =>
        AcknowledgedRevision(rule.Id, childId) is { } revision && revision >= rule.AcknowledgementRevision;

    // Not named Apply/Create/Evolve -- JasperFx's projection source generator scans those names on
    // any projection document type (docs/backend/analysis/event-stream-snapshots.md, Question 4/5).
    public static RuleBook Start(RuleBookEvent @event) => @event switch
    {
        RuleBookStarted started => new RuleBook(
            started.Id,
            started.ScopeKind,
            started.ScopeId,
            [],
            ImmutableDictionary<(RuleId, UserId), int>.Empty,
            started.StartedBy),
        _ => throw EventReplay.NotAStartEvent(nameof(RuleBook), @event.EventType)
    };

    public static RuleBook Advance(RuleBook book, RuleBookEvent @event) => @event switch
    {
        RuleAdded added => book with
        {
            Rules = book.Rules.Add(new Rule(added.RuleId, added.Title, added.Body, 1, 1, added.AddedBy, added.OccurredAt)),
            LastModifiedBy = added.AddedBy
        },
        RuleEdited edited => book with
        {
            Rules = Replace(book.Rules, edited.RuleId, rule => rule with
            {
                Title = edited.After.Title,
                Body = edited.After.Body,
                Revision = edited.Revision,
                AcknowledgementRevision = edited.RequiresReacknowledgement ? edited.Revision : rule.AcknowledgementRevision,
                LastEditedBy = edited.EditedBy,
                LastEditedAt = edited.OccurredAt
            }),
            LastModifiedBy = edited.EditedBy
        },
        RuleRemoved removed => book with
        {
            Rules = book.Rules.RemoveAll(r => r.Id == removed.RuleId),
            Acknowledgements = book.Acknowledgements.RemoveRange(book.Acknowledgements.Keys.Where(k => k.Rule == removed.RuleId)),
            LastModifiedBy = removed.RemovedBy
        },
        RulesReordered reordered => book with
        {
            Rules = Reorder(book.Rules, reordered.After),
            LastModifiedBy = reordered.ModifiedBy
        },
        // An acknowledgement is the child's, not an edit of the book, so LastModifiedBy stays.
        RuleAcknowledged acknowledged => book with
        {
            Acknowledgements = book.Acknowledgements.SetItem((acknowledged.RuleId, acknowledged.ChildId), acknowledged.Revision)
        },
        RuleBookStarted => throw EventReplay.AlreadyStarted(nameof(RuleBook), @event.EventType)
    };

    private static ImmutableList<Rule> Replace(ImmutableList<Rule> rules, RuleId ruleId, Func<Rule, Rule> change)
    {
        var index = rules.FindIndex(r => r.Id == ruleId);

        return index >= 0
            ? rules.SetItem(index, change(rules[index]))
            : throw new InvalidOperationException($"RuleEdited referenced unknown rule {ruleId}.");
    }

    // Every id in `after` must already exist -- a violation is a bug in the handler that appended
    // RulesReordered (ReorderRulesHandler checks the permutation), not user input, so this throws
    // like TaskTemplate.Reorder.
    private static ImmutableList<Rule> Reorder(ImmutableList<Rule> rules, ImmutableList<RuleId> after)
    {
        var byId = rules.ToDictionary(r => r.Id);

        return [.. after.Select(id => byId.TryGetValue(id, out var rule)
            ? rule
            : throw new InvalidOperationException($"RulesReordered referenced unknown rule {id}."))];
    }
}
