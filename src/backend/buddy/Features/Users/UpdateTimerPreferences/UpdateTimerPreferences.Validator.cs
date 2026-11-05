using FluentValidation;

namespace buddy.Features.Users;

public sealed class UpdateTimerPreferencesValidator : AbstractValidator<UpdateTimerPreferences>
{
    public UpdateTimerPreferencesValidator()
    {
        // JsonStringEnumConverter also accepts integers, so an undefined one would bind.
        RuleFor(x => x.TimerPreferences.Shape).IsInEnum().OverridePropertyName("shape");
        RuleFor(x => x.TimerPreferences.Size).IsInEnum().OverridePropertyName("size");
        RuleFor(x => x.TimerPreferences.Color).IsInEnum().OverridePropertyName("color");
        RuleFor(x => x.TimerPreferences.ChimeSound).IsInEnum().OverridePropertyName("chimeSound");
    }
}
