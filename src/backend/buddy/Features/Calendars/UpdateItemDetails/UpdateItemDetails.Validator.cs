using FluentValidation;

namespace buddy.Features.Calendars;

public sealed class UpdateItemDetailsValidator : AbstractValidator<UpdateItemDetails>
{
    public UpdateItemDetailsValidator()
    {
        RuleFor(x => x.Icon!.Value).ValidIcon().OverridePropertyName("Icon").When(x => x.Icon is not null);
    }
}
