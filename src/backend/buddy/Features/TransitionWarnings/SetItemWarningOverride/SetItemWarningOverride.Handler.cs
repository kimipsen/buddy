using buddy.Common.Validation;
using buddy.Features.Calendars;
using buddy.Features.Groups;
using buddy.Features.Guardians;

using FluentValidation;

namespace buddy.Features.TransitionWarnings;

public static class SetItemWarningOverrideHandler
{
    public static async Task<SetItemWarningOverrideOutcome> Handle(
        SetItemWarningOverride command,
        IValidator<SetItemWarningOverride> validator,
        ITransitionWarningSettingsEventStore settings,
        IGuardianLinkEventStore guardians,
        ICalendarItemEventStore items,
        ICalendarEventStore calendars,
        IGroupEventStore groups,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        if (await validator.ValidateCommandAsync(command, cancellationToken) is { } problem)
        {
            return new SetItemWarningOverrideOutcome.Validation(problem);
        }

        var access = await TransitionWarningsAuthorization.CheckManage(command.ChildId, command.UserId, guardians, cancellationToken);

        if (access != TransitionWarningsAccess.Allowed)
        {
            return SetItemWarningOverrideOutcome.Denied(access);
        }

        // A guardian can't store overrides for items they can't see: missing, deleted and private
        // all answer NotFound (Question 4).
        if (await ChildTransitionWarningsView.FindVisibleItemAsync(command.ItemId, command.UserId, items, calendars, groups, cancellationToken) is null)
        {
            return new SetItemWarningOverrideOutcome.NotFound();
        }

        var (current, exists) = await settings.LoadForWriteAsync(command.ChildId, cancellationToken);
        var after = ItemWarningOverride.FromLeadMinutes(command.LeadMinutes);
        var before = current.ItemOverrides.GetValueOrDefault(command.ItemId);

        if (after != before)
        {
            if (before is null && await CountLiveOverridesAsync(current, items, cancellationToken) >= LeadTimeRules.MaxOverrides)
            {
                return new SetItemWarningOverrideOutcome.TooManyOverrides();
            }

            var now = time.GetUtcNow();
            await settings.SaveAsync(
                current, exists, new ItemWarningOverrideSet(current.Id, command.ItemId, before, after, command.UserId, now), now, cancellationToken);
            current = current with { ItemOverrides = current.ItemOverrides.SetItem(command.ItemId, after) };
        }

        return new SetItemWarningOverrideOutcome.Success(
            await ChildTransitionWarningsView.BuildAsync(current, command.UserId, items, calendars, groups, cancellationToken));
    }

    // Exceptions for deleted items don't count towards the cap: the guardian's list leaves them out,
    // so they could never be removed to make room. Only read at the cap, so at most 100 loads.
    private static async Task<int> CountLiveOverridesAsync(TransitionWarningSettings settings, ICalendarItemEventStore items, CancellationToken cancellationToken)
    {
        if (settings.ItemOverrides.Count < LeadTimeRules.MaxOverrides)
        {
            return settings.ItemOverrides.Count;
        }

        var live = 0;
        foreach (var itemId in settings.ItemOverrides.Keys)
        {
            if (await items.FindSnapshotAsync(itemId, cancellationToken) is { IsDeleted: false })
            {
                live++;
            }
        }

        return live;
    }
}
