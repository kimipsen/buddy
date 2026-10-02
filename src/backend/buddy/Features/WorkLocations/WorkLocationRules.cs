using FluentValidation;

namespace buddy.Features.WorkLocations;

// Shared structural rules for a location's name/icon/color (AddWorkLocation, UpdateWorkLocation).
// Limits are recorded in docs/backend/analysis/validation-rules.md.
internal static class WorkLocationRules
{
    public const int MaxNameLength = 40;
    public const int MaxIconLength = 16;
    public const int MaxColorLength = 32;
    public const int MaxActiveLocations = 12;

    // Same cap as ListPickupScheduleHandler.MaxRangeDays, for list and override ranges alike, so one
    // request never writes more than a month of override events.
    public const int MaxRangeDays = 31;

    public static void ValidLocationDetails<T>(this AbstractValidator<T> validator, Func<T, string> name, Func<T, string> icon, Func<T, string> color)
    {
        validator.RuleFor(x => name(x).Trim())
            .NotEmpty()
            .WithMessage("A work location requires a name.")
            .MaximumLength(MaxNameLength)
            .OverridePropertyName("name");

        validator.RuleFor(x => icon(x))
            .NotEmpty()
            .WithMessage("A work location requires an icon.")
            .MaximumLength(MaxIconLength)
            .OverridePropertyName("icon");

        validator.RuleFor(x => color(x))
            .NotEmpty()
            .WithMessage("A work location requires a color.")
            .MaximumLength(MaxColorLength)
            .OverridePropertyName("color");
    }

    // Unique among the guardian's active locations, case-insensitive. An archived location's name
    // can be reused. excluding lets UpdateWorkLocation keep its own name.
    public static bool NameIsTaken(WorkLocationSchedule schedule, string name, WorkLocationId? excluding = null) =>
        schedule.ActiveLocations.Any(l => l.Id != excluding && string.Equals(l.Name, name, StringComparison.OrdinalIgnoreCase));
}
