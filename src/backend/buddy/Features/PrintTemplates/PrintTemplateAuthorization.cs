using System.Diagnostics;

using buddy.Common;
using buddy.Features.Calendars;
using buddy.Features.Groups;
using buddy.Features.Guardians;
using buddy.Features.Users;

namespace buddy.Features.PrintTemplates;

public enum PrintTemplateAccess
{
    Allowed,
    // No relationship -- collapsed to 404, so a caller can't tell a private template from a missing one.
    NotFound,
    // A child account on a create: related to guardians, but printing is a guardian activity.
    Forbidden
}

public static class PrintTemplateAccessExtensions
{
    public static Result<T> ToDeniedResult<T>(this PrintTemplateAccess access) => access switch
    {
        PrintTemplateAccess.Forbidden => new Result<T>.Forbidden(),
        PrintTemplateAccess.NotFound => new Result<T>.NotFound(),
        PrintTemplateAccess.Allowed => throw new UnreachableException("ToDeniedResult called with PrintTemplateAccess.Allowed."),
        _ => throw new UnreachableException($"Unrecognized PrintTemplateAccess value: {access}."),
    };
}

// One tier only, Manage: the owner of a user-owned template, or any non-child member of the owning
// group. There is deliberately no View tier and no child tier (see
// docs/backend/analysis/week-plan-print-templates.md#authorization).
public static class PrintTemplateAuthorization
{
    public static async Task<PrintTemplateAccess> CheckManage(
        PrintTemplate template,
        UserId callerId,
        IGroupEventStore groups,
        IGuardianLinkEventStore guardians,
        CancellationToken cancellationToken)
    {
        if (template.IsDeleted)
        {
            return PrintTemplateAccess.NotFound;
        }

        return template.Owner switch
        {
            PrintTemplateOwner.User(var ownerId) => ownerId == callerId ? PrintTemplateAccess.Allowed : PrintTemplateAccess.NotFound,
            PrintTemplateOwner.Group(var groupId) => await IsGuardianMemberAsync(groupId, callerId, groups, guardians, cancellationToken)
                ? PrintTemplateAccess.Allowed
                : PrintTemplateAccess.NotFound,
        };
    }

    // Children can be group members (AddChildToGroup), so membership alone isn't enough.
    public static async Task<bool> IsGuardianMemberAsync(
        GroupId groupId,
        UserId callerId,
        IGroupEventStore groups,
        IGuardianLinkEventStore guardians,
        CancellationToken cancellationToken)
    {
        var group = await groups.FindSnapshotAsync(groupId, cancellationToken);

        return group is { IsDeleted: false }
            && group.Members.ContainsKey(callerId)
            && !await ChildVisibility.IsChildAsync(callerId, guardians, cancellationToken);
    }
}
