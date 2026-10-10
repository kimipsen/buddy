using buddy.Common;
using buddy.Features.Groups;
using buddy.Features.Guardians;
using buddy.Features.Users;

namespace buddy.Features.HouseRules;

public static class GetChildRulesHandler
{
    // The child, or an active guardian of the child. A guardian sees every household the child is in,
    // including a home whose group they don't belong to -- read-only, by design (house-rules.md,
    // Question 7 and Decisions made).
    public static async Task<Result<ChildRulesResponse>> Handle(
        GetChildRules command,
        IRuleBookEventStore books,
        IGroupEventStore groups,
        IGuardianLinkEventStore guardians,
        IUserEventStore users,
        CancellationToken cancellationToken)
    {
        var personalScope = RuleBookScope.ForChild(command.ChildId);
        var tier = await HouseRulesAuthorization.ResolveTierAsync(personalScope, command.UserId, groups, guardians, cancellationToken);

        if (tier == HouseRulesAccessTier.None)
        {
            return new Result<ChildRulesResponse>.NotFound();
        }

        var child = await users.FindSnapshotAsync(command.ChildId, cancellationToken);
        var personal = ChildRuleSectionResponse.From(
            personalScope,
            child?.Name.GivenName ?? "",
            await RuleBooks.FindSnapshotAsync(books, personalScope, cancellationToken),
            command.ChildId);

        List<ChildRuleSectionResponse> households = [];

        foreach (var membership in await groups.ListForUserAsync(command.ChildId, cancellationToken))
        {
            var groupId = new GroupId(membership.GroupId);

            // The membership index can trail a just-deleted group; the snapshot is authoritative.
            if (await groups.FindSnapshotAsync(groupId, cancellationToken) is not { IsDeleted: false } group
                || !group.Members.ContainsKey(command.ChildId))
            {
                continue;
            }

            var scope = RuleBookScope.ForGroup(groupId);
            households.Add(ChildRuleSectionResponse.From(
                scope, group.Name, await RuleBooks.FindSnapshotAsync(books, scope, cancellationToken), command.ChildId));
        }

        households = [.. households.OrderBy(h => h.Label, StringComparer.OrdinalIgnoreCase).ThenBy(h => h.ScopeId)];

        var pending = personal.Rules.Concat(households.SelectMany(h => h.Rules)).Count(r => !r.IsUpToDate);

        return new Result<ChildRulesResponse>.Success(new ChildRulesResponse(command.ChildId.Value, personal, households, pending));
    }
}
