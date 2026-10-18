using buddy.Features.Users;

namespace buddy.Features.HouseRules;

// The part of a rule a guardian writes; Before/After on RuleEdited, RuleDraftEdited and RuleRemoved.
// Title is plain text, Body is raw markdown the backend never parses ("" means title-only).
public sealed record RuleContent(string Title, string Body);

// Title/Body are the published version, the one the children read. Revision bumps on every publish
// and is 0 until the rule is first published (Title/Body are "" then, and Draft holds the text).
// AcknowledgementRevision is the last Revision that asked the children to re-acknowledge: a minor
// change (RequiresReacknowledgement = false) leaves it behind, so a typo fix doesn't reset what the
// children have agreed to. See house-rules.md, Question 3, and house-rules-publishing.md, Question 1.
//
// Draft is the unpublished change only Manage sees. It defaults to null so rules in snapshots written
// before drafts existed read as published with no draft.
public sealed record Rule(
    RuleId Id,
    string Title,
    string Body,
    int Revision,
    int AcknowledgementRevision,
    UserId LastEditedBy,
    DateTimeOffset LastEditedAt,
    RuleDraft? Draft = null)
{
    public bool IsPublished => Revision > 0;

    public RuleContent Content => new(Title, Body);

    // What a guardian edits: the draft when there is one, otherwise the published text.
    public RuleContent WorkingContent => Draft?.Content ?? Content;
}

// RequiresReacknowledgement is the editor's "small fix" switch, kept with the draft until it is
// published. A first publish ignores it: a rule the children have never seen is always new to them.
public sealed record RuleDraft(RuleContent Content, bool RequiresReacknowledgement, UserId EditedBy, DateTimeOffset EditedAt);
