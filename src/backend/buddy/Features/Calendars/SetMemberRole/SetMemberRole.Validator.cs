using FluentValidation;

namespace buddy.Features.Calendars;

public sealed class SetMemberRoleValidator : AbstractValidator<SetMemberRole>
{
    public SetMemberRoleValidator()
    {
        // JsonStringEnumConverter also accepts integers, so an undefined one would bind.
        RuleFor(x => x.Role)
            .IsInEnum()
            // Ownership comes only from the owning group's CalendarPermissionPolicy.
            .NotEqual(CalendarRole.Owner)
            .WithMessage("Ownership cannot be granted through this endpoint.");
    }
}
