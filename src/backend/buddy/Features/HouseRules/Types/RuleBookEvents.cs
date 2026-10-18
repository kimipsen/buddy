using System.Collections.Immutable;

using buddy.Features.Users;

namespace buddy.Features.HouseRules;

public union RuleBookEvent(
    RuleBookStarted,
    RuleAdded,
    RuleEdited,
    RuleRemoved,
    RulesReordered,
    RuleAcknowledged,
    RuleDraftAdded,
    RuleDraftEdited,
    RuleDraftDiscarded,
    RulePublished
)
{
    public static RuleBookEvent FromPayload(object payload) => payload switch
    {
        RuleBookStarted e => e,
        RuleAdded e => e,
        RuleEdited e => e,
        RuleRemoved e => e,
        RulesReordered e => e,
        RuleAcknowledged e => e,
        RuleDraftAdded e => e,
        RuleDraftEdited e => e,
        RuleDraftDiscarded e => e,
        RulePublished e => e,
        _ => throw new ArgumentException($"Unknown rule book event payload: {payload.GetType().Name}", nameof(payload)),
    };

    public string EventType => this switch
    {
        RuleBookStarted => nameof(RuleBookStarted),
        RuleAdded => nameof(RuleAdded),
        RuleEdited => nameof(RuleEdited),
        RuleRemoved => nameof(RuleRemoved),
        RulesReordered => nameof(RulesReordered),
        RuleAcknowledged => nameof(RuleAcknowledged),
        RuleDraftAdded => nameof(RuleDraftAdded),
        RuleDraftEdited => nameof(RuleDraftEdited),
        RuleDraftDiscarded => nameof(RuleDraftDiscarded),
        RulePublished => nameof(RulePublished),
    };
}

// Appended together with the first RuleAdded for a scope, never on its own -- the stream is created
// lazily, like SleepDiaryStarted.
public sealed record RuleBookStarted(RuleBookId Id, RuleBookScopeKind ScopeKind, Guid ScopeId, UserId StartedBy, DateTimeOffset OccurredAt);

// Appended at the end of the list; Revision = AcknowledgementRevision = 1. Written before drafts
// existed (house-rules-publishing.md, Question 2): no handler appends it any more, but existing
// streams still fold it as a rule added and published in one step. AddRule now appends RuleDraftAdded.
public sealed record RuleAdded(RuleBookId Id, RuleId RuleId, string Title, string Body, UserId AddedBy, DateTimeOffset OccurredAt);

// Revision is the rule's new revision. RequiresReacknowledgement moves AcknowledgementRevision up
// to it; a minor edit leaves it where it was. Like RuleAdded, only in streams written before drafts
// existed: EditRule now appends RuleDraftEdited, and RulePublished makes the change visible.
public sealed record RuleEdited(
    RuleBookId Id,
    RuleId RuleId,
    RuleContent Before,
    RuleContent After,
    int Revision,
    bool RequiresReacknowledgement,
    UserId EditedBy,
    DateTimeOffset OccurredAt);

// Gone, not archived: the event history keeps its text. The fold also drops its acknowledgements.
public sealed record RuleRemoved(RuleBookId Id, RuleId RuleId, RuleContent Before, UserId RemovedBy, DateTimeOffset OccurredAt);

// After is always a permutation of the book's current rule ids (checked by the handler, enforced by
// the fold), exactly as SubtasksReordered.
public sealed record RulesReordered(RuleBookId Id, ImmutableList<RuleId> Before, ImmutableList<RuleId> After, UserId ModifiedBy, DateTimeOffset OccurredAt);

// The child agreed to Revision of the rule. RecordedBy is the child themself, or a guardian who went
// through the rule with a child who can't read yet (house-rules.md, Decisions made).
public sealed record RuleAcknowledged(RuleBookId Id, RuleId RuleId, UserId ChildId, int Revision, UserId RecordedBy, DateTimeOffset OccurredAt);

// A new rule at the end of the list that only Manage sees: Revision 0, Draft = Content, until
// RulePublished (house-rules-publishing.md, Question 2).
public sealed record RuleDraftAdded(RuleBookId Id, RuleId RuleId, RuleContent Content, UserId AddedBy, DateTimeOffset OccurredAt);

// The draft becomes After. Before is the working content the guardian started from: the previous
// draft, or the published text when there was none. The children still see the published text.
public sealed record RuleDraftEdited(
    RuleBookId Id,
    RuleId RuleId,
    RuleContent Before,
    RuleContent After,
    bool RequiresReacknowledgement,
    UserId EditedBy,
    DateTimeOffset OccurredAt);

// A published rule goes back to its published text. Never appended for a rule that was never
// published -- that rule is removed instead.
public sealed record RuleDraftDiscarded(RuleBookId Id, RuleId RuleId, RuleContent Discarded, UserId DiscardedBy, DateTimeOffset OccurredAt);

// Content becomes what the children read, at Revision. RequiresReacknowledgement is what the handler
// decided (always true on a first publish), so the fold needs no special case. Publishing every
// draft in a book appends one of these per rule, in one append.
public sealed record RulePublished(
    RuleBookId Id,
    RuleId RuleId,
    RuleContent Content,
    int Revision,
    bool RequiresReacknowledgement,
    UserId PublishedBy,
    DateTimeOffset OccurredAt);
