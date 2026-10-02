using FluentValidation;

namespace buddy.Features.Pickups;

// Structural validation only. Which fields a kind carries is the PickupAssignee union's job now;
// what is left is a playdate host name being present and the free-text lengths. Relationship
// validation (is GuardianId actually a guardian, is SiblingChildId actually a sibling) needs async
// DB-backed lookups and deliberately stays in AssignPickupHandler.ValidateRelationshipAsync,
// running after PickupAuthorization.CheckManage -- see docs/backend/analysis/validation-rules.md.
public sealed class AssignPickupValidator : AbstractValidator<AssignPickup>
{
    public AssignPickupValidator()
    {
        RuleFor(x => x.Assignee).Custom((assignee, context) =>
        {
            if (assignee is not PickupAssignee.Playdate playdate)
            {
                return;
            }

            if (playdate.HostName.Length == 0)
            {
                context.AddFailure("Assignee.HostName", "A playdate assignee requires hostName.");
            }
            else if (playdate.HostName.Length > 200)
            {
                context.AddFailure("Assignee.HostName", "hostName must be 200 characters or fewer.");
            }

            if (playdate.Location.Length > 200)
            {
                context.AddFailure("Assignee.Location", "location must be 200 characters or fewer.");
            }

            if (playdate.ContactInfo.Length > 2000)
            {
                context.AddFailure("Assignee.ContactInfo", "contactInfo must be 2000 characters or fewer.");
            }
        });

        RuleFor(x => x.Notes).MaximumLength(2000);
    }
}
