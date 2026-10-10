using System.Collections.Immutable;

using buddy.Features.Users;

namespace buddy.Features.HouseRules;

public union RuleBookEvent(
    RuleBookStarted,
    RuleAdded,
    RuleEdited,
    RuleRemoved,
    RulesReordered,
    RuleAcknowledged
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
    };
}

// Appended together with the first RuleAdded for a scope, never on its own -- the stream is created
// lazily, like SleepDiaryStarted.
public sealed record RuleBookStarted(RuleBookId Id, RuleBookScopeKind ScopeKind, Guid ScopeId, UserId StartedBy, DateTimeOffset OccurredAt);

// Appended at the end of the list; Revision = AcknowledgementRevision = 1.
public sealed record RuleAdded(RuleBookId Id, RuleId RuleId, string Title, string Body, UserId AddedBy, DateTimeOffset OccurredAt);

// Revision is the rule's new revision. RequiresReacknowledgement moves AcknowledgementRevision up
// to it; a minor edit leaves it where it was.
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
