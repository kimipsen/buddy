using System.Diagnostics;

using buddy.Common;
using buddy.Features.Calendars;
using buddy.Features.Groups;
using buddy.Features.Guardians;
using buddy.Features.Users;

namespace buddy.Features.HouseRules;

// Two scopes, three tiers (docs/backend/analysis/house-rules.md, Question 5):
//   child book: an active guardian -> Manage, the child themself -> Acknowledge;
//   group book: a non-child Owner/Admin -> Manage, a child member -> Acknowledge, any other member -> View.
// Anyone else -> None, collapsed to NotFound so a stranger can't tell a private book from a missing one.
public enum HouseRulesAccessTier
{
    None,
    // Read the book, including every child's acknowledgement status.
    View,
    // A child reading their own rules: read, and acknowledge for themself.
    Acknowledge,
    // Add, edit, remove and reorder rules; read; acknowledge on behalf of a child they're a guardian of.
    Manage
}

public enum HouseRulesAccess
{
    Allowed,
    NotFound,
    // The caller can see the book but the action needs a higher tier (a child editing their rules,
    // an adult member adding to a household book).
    Forbidden
}

public static class HouseRulesAccessExtensions
{
    public static Result<T> ToDeniedResult<T>(this HouseRulesAccess access) => access switch
    {
        HouseRulesAccess.Forbidden => new Result<T>.Forbidden(),
        HouseRulesAccess.NotFound => new Result<T>.NotFound(),
        HouseRulesAccess.Allowed => throw new UnreachableException("ToDeniedResult called with HouseRulesAccess.Allowed."),
        _ => throw new UnreachableException($"Unrecognized HouseRulesAccess value: {access}."),
    };
}

public static class HouseRulesAuthorization
{
    public static async Task<HouseRulesAccess> CheckManage(
        RuleBookScope scope,
        UserId callerId,
        IGroupEventStore groups,
        IGuardianLinkEventStore guardians,
        CancellationToken cancellationToken)
    {
        var tier = await ResolveTierAsync(scope, callerId, groups, guardians, cancellationToken);

        return tier switch
        {
            HouseRulesAccessTier.Manage => HouseRulesAccess.Allowed,
            HouseRulesAccessTier.View or HouseRulesAccessTier.Acknowledge => HouseRulesAccess.Forbidden,
            HouseRulesAccessTier.None => HouseRulesAccess.NotFound,
            _ => throw new UnreachableException($"Unrecognized HouseRulesAccessTier value: {tier}."),
        };
    }

    public static async Task<HouseRulesAccessTier> ResolveTierAsync(
        RuleBookScope scope,
        UserId callerId,
        IGroupEventStore groups,
        IGuardianLinkEventStore guardians,
        CancellationToken cancellationToken) => scope.Kind switch
        {
            RuleBookScopeKind.Child => await ResolveChildTierAsync(new UserId(scope.Id), callerId, guardians, cancellationToken),
            RuleBookScopeKind.Group => await ResolveGroupTierAsync(new GroupId(scope.Id), callerId, groups, guardians, cancellationToken),
            _ => throw new UnreachableException($"Unrecognized RuleBookScopeKind value: {scope.Kind}."),
        };

    // The children whose acknowledgements a book tracks: the child of a personal book, or every
    // child member of a household group (in member order, which is stable for the ETag).
    public static async Task<IReadOnlyList<UserId>> ListChildrenInScopeAsync(
        RuleBookScope scope,
        IGroupEventStore groups,
        IGuardianLinkEventStore guardians,
        CancellationToken cancellationToken)
    {
        if (scope.Kind == RuleBookScopeKind.Child)
        {
            return [new UserId(scope.Id)];
        }

        var group = await groups.FindSnapshotAsync(new GroupId(scope.Id), cancellationToken);

        if (group is null || group.IsDeleted)
        {
            return [];
        }

        List<UserId> children = [];

        foreach (var memberId in group.Members.Keys.OrderBy(m => m.Value))
        {
            if (await ChildVisibility.IsChildAsync(memberId, guardians, cancellationToken))
            {
                children.Add(memberId);
            }
        }

        return children;
    }

    private static async Task<HouseRulesAccessTier> ResolveChildTierAsync(
        UserId childId,
        UserId callerId,
        IGuardianLinkEventStore guardians,
        CancellationToken cancellationToken)
    {
        if (callerId == childId)
        {
            return HouseRulesAccessTier.Acknowledge;
        }

        var link = await guardians.FindActiveLinkAsync(childId, callerId, cancellationToken);

        return link is not null ? HouseRulesAccessTier.Manage : HouseRulesAccessTier.None;
    }

    private static async Task<HouseRulesAccessTier> ResolveGroupTierAsync(
        GroupId groupId,
        UserId callerId,
        IGroupEventStore groups,
        IGuardianLinkEventStore guardians,
        CancellationToken cancellationToken)
    {
        var group = await groups.FindSnapshotAsync(groupId, cancellationToken);

        if (group is null || group.IsDeleted || !group.Members.TryGetValue(callerId, out var role))
        {
            return HouseRulesAccessTier.None;
        }

        // Checked before the role, so a child never manages the rules they're asked to keep, even if
        // a future role change made one an Admin (same guard as PrintTemplateAuthorization).
        if (await ChildVisibility.IsChildAsync(callerId, guardians, cancellationToken))
        {
            return HouseRulesAccessTier.Acknowledge;
        }

        return role is GroupRole.Owner or GroupRole.Admin ? HouseRulesAccessTier.Manage : HouseRulesAccessTier.View;
    }
}
