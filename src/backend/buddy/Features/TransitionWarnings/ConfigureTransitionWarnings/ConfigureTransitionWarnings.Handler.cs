using buddy.Common;
using buddy.Common.Validation;
using buddy.Features.Calendars;
using buddy.Features.Groups;
using buddy.Features.Guardians;

using FluentValidation;

namespace buddy.Features.TransitionWarnings;

public static class ConfigureTransitionWarningsHandler
{
    public static async Task<Result<ChildTransitionWarningsResponse>> Handle(
        ConfigureTransitionWarnings command,
        IValidator<ConfigureTransitionWarnings> validator,
        ITransitionWarningSettingsEventStore settings,
        IGuardianLinkEventStore guardians,
        ICalendarItemEventStore items,
        ICalendarEventStore calendars,
        IGroupEventStore groups,
        CancellationToken cancellationToken)
    {
        if (await validator.ValidateCommandAsync(command, cancellationToken) is { } problem)
        {
            return new Result<ChildTransitionWarningsResponse>.Validation(problem);
        }

        var access = await TransitionWarningsAuthorization.CheckManage(command.ChildId, command.UserId, guardians, cancellationToken);

        if (access != TransitionWarningsAccess.Allowed)
        {
            return access.ToDeniedResult<ChildTransitionWarningsResponse>();
        }

        var (current, exists) = await settings.LoadForWriteAsync(command.ChildId, cancellationToken);
        var after = WarningDefaults.Create(command.Enabled, command.LeadMinutes, command.Kinds, command.QuietHours);

        // An identical save appends nothing. Turning warnings off keeps the lead times, kinds, quiet
        // hours and overrides, so switching back on restores them.
        if (after != current.Defaults)
        {
            var now = DateTimeOffset.UtcNow;
            await settings.SaveAsync(
                current, exists, new TransitionWarningDefaultsConfigured(current.Id, current.Defaults, after, command.UserId, now), now, cancellationToken);
            current = current with { Defaults = after };
        }

        return new Result<ChildTransitionWarningsResponse>.Success(
            await ChildTransitionWarningsView.BuildAsync(current, command.UserId, items, calendars, groups, cancellationToken));
    }
}
