using buddy.Features.Calendars;

using FluentValidation;

namespace buddy.Features.TaskLibrary;

public sealed class UpdateTaskTemplateValidator : AbstractValidator<UpdateTaskTemplate>
{
    public UpdateTaskTemplateValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);

        // Optional (blank is allowed), but capped.
        RuleFor(x => x.Color.Value).MaximumLength(CreateTaskTemplateValidator.MaxColorLength).OverridePropertyName("Color");

        // A template always has its own icon (its subtasks may inherit it), so blank is rejected.
        RuleFor(x => x.Icon.Value).NotEmpty().WithMessage("Icon must not be empty.").OverridePropertyName("Icon");
        RuleFor(x => x.Icon.Value).ValidIcon().OverridePropertyName("Icon");
    }
}
