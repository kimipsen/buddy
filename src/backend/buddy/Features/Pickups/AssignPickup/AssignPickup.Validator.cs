using FluentValidation;

namespace buddy.Features.Pickups;

// Structural validation only: does the request carry the fields its own Kind needs. Relationship
// validation (is GuardianId actually a guardian, is SiblingChildId actually a sibling) needs async
// DB-backed lookups and deliberately stays in AssignPickupHandler.ValidateRelationshipAsync,
// running after PickupAuthorization.CheckManage -- see docs/backend/analysis/validation-rules.md
// and the accompanying plan for why that one isn't converted here.
public sealed class AssignPickupValidator : AbstractValidator<AssignPickup>
{
    public AssignPickupValidator()
    {
        RuleFor(x => x.GuardianId)
            .NotNull()
            .WithMessage("A guardian assignee requires guardianId.")
            .When(x => x.Kind == PickupAssigneeKind.Guardian);

        RuleFor(x => x.SiblingChildId)
            .NotNull()
            .WithMessage("A sibling assignee requires siblingChildId.")
            .When(x => x.Kind == PickupAssigneeKind.Sibling);

        RuleFor(x => x.PlaydateHostName)
            .NotEmpty()
            .WithMessage("A playdate assignee requires playdateHostName.")
            .MaximumLength(200)
            .When(x => x.Kind == PickupAssigneeKind.Playdate);

        RuleFor(x => x.PlaydateLocation).MaximumLength(200);
        RuleFor(x => x.PlaydateContactInfo).MaximumLength(2000);
        RuleFor(x => x.Notes).MaximumLength(2000);
    }
}
