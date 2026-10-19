using FluentValidation;

namespace buddy.Features.Calendars;

public sealed class UpdateItemDetailsValidator : AbstractValidator<UpdateItemDetails>
{
    public UpdateItemDetailsValidator()
    {
        // The same cap as CreateItem and ScheduleTaskFromTemplate; an empty title is allowed there too.
        RuleFor(x => x.Title).MaximumLength(200);

        RuleFor(x => x.Icon!.Value).ValidIcon().OverridePropertyName("Icon").When(x => x.Icon is not null);

        // Optional; the endpoint trims it.
        RuleFor(x => x.Color.Value).MaximumLength(Color.MaxLength).OverridePropertyName("Color");
    }
}
