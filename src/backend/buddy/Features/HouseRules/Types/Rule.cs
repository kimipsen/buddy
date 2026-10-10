using buddy.Features.Users;

namespace buddy.Features.HouseRules;

// The part of a rule a guardian writes; Before/After on RuleEdited and RuleRemoved. Title is plain
// text, Body is raw markdown the backend never parses ("" means title-only).
public sealed record RuleContent(string Title, string Body);

// Revision bumps on every content edit. AcknowledgementRevision is the last Revision that asked the
// children to re-acknowledge: a minor edit (RequiresReacknowledgement = false) leaves it behind, so a
// typo fix doesn't reset what the children have agreed to. See house-rules.md, Question 3.
public sealed record Rule(
    RuleId Id,
    string Title,
    string Body,
    int Revision,
    int AcknowledgementRevision,
    UserId LastEditedBy,
    DateTimeOffset LastEditedAt)
{
    public RuleContent Content => new(Title, Body);
}
