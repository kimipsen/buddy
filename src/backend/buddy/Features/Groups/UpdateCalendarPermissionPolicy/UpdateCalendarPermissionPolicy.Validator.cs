using buddy.Features.Calendars;

using FluentValidation;

namespace buddy.Features.Groups;

public sealed class UpdateCalendarPermissionPolicyValidator : AbstractValidator<UpdateCalendarPermissionPolicy>
{
    public UpdateCalendarPermissionPolicyValidator()
    {
        RuleFor(x => x.Policy).DefinedRolesAndTiers<UpdateCalendarPermissionPolicy, CalendarRole>();
    }
}
