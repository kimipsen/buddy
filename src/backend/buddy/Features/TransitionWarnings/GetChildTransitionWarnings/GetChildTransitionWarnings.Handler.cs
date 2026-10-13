using buddy.Common;
using buddy.Features.Calendars;
using buddy.Features.Groups;
using buddy.Features.Guardians;

namespace buddy.Features.TransitionWarnings;

public static class GetChildTransitionWarningsHandler
{
    public static async Task<Result<ChildTransitionWarningsResponse>> Handle(
        GetChildTransitionWarnings query,
        ITransitionWarningSettingsEventStore settings,
        IGuardianLinkEventStore guardians,
        ICalendarItemEventStore items,
        ICalendarEventStore calendars,
        IGroupEventStore groups,
        CancellationToken cancellationToken)
    {
        var access = await TransitionWarningsAuthorization.CheckManage(query.ChildId, query.UserId, guardians, cancellationToken);

        if (access != TransitionWarningsAccess.Allowed)
        {
            return access.ToDeniedResult<ChildTransitionWarningsResponse>();
        }

        var current = await settings.FindSnapshotAsync(TransitionWarningSettingsId.ForChild(query.ChildId), cancellationToken)
            ?? TransitionWarningSettings.ForChild(query.ChildId);

        return new Result<ChildTransitionWarningsResponse>.Success(
            await ChildTransitionWarningsView.BuildAsync(current, query.UserId, items, calendars, groups, cancellationToken));
    }
}
