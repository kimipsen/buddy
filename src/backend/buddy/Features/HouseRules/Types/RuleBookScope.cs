using buddy.Features.Groups;
using buddy.Features.Users;

namespace buddy.Features.HouseRules;

// A flat kind discriminator rather than a Child(UserId) | Group(GroupId) union: a closed union
// inside a persisted event doesn't round-trip through System.Text.Json on Marten replay
// (pickup-schedules.md, Question 3). RuleBookStarted carries Kind + ScopeId the same way.
public enum RuleBookScopeKind
{
    Child,
    Group
}

// The scope a command addresses -- built by the endpoint from its route (children/{childId} or
// groups/{groupId}). Never persisted as a whole.
public sealed record RuleBookScope(RuleBookScopeKind Kind, Guid Id)
{
    public static RuleBookScope ForChild(UserId childId) => new(RuleBookScopeKind.Child, childId.Value);

    public static RuleBookScope ForGroup(GroupId groupId) => new(RuleBookScopeKind.Group, groupId.Value);

    public RuleBookId BookId => new(Id);
}
