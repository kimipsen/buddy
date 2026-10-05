using FluentValidation;

namespace buddy.Features.Babysitters;

// Shared structural rules for a babysitter's details (AddBabysitter, UpdateBabysitter). Limits are
// recorded in docs/backend/analysis/babysitters.md, Validation limits.
internal static class BabysitterRules
{
    public const int MaxNameLength = 80;
    public const int MaxContactInfoLength = 200;
    public const int MaxActiveBabysitters = 20;

    public static void ValidBabysitterDetails<T>(this AbstractValidator<T> validator, Func<T, string> name, Func<T, string> contactInfo)
    {
        validator.RuleFor(x => name(x).Trim())
            .NotEmpty()
            .WithMessage("A babysitter requires a name.")
            .MaximumLength(MaxNameLength)
            .OverridePropertyName("name");

        validator.RuleFor(x => contactInfo(x).Trim())
            .MaximumLength(MaxContactInfoLength)
            .OverridePropertyName("contactInfo");
    }

    // Unique among the guardian's active babysitters, case-insensitive. An archived babysitter's name
    // can be reused. excluding lets UpdateBabysitter keep its own name.
    public static bool NameIsTaken(BabysitterList list, string name, BabysitterId? excluding = null) =>
        list.ActiveBabysitters.Any(b => b.Id != excluding && string.Equals(b.Name, name, StringComparison.OrdinalIgnoreCase));
}
