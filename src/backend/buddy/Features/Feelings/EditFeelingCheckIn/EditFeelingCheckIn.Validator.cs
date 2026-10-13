using FluentValidation;

namespace buddy.Features.Feelings;

public sealed class EditFeelingCheckInValidator : AbstractValidator<EditFeelingCheckIn>
{
    public EditFeelingCheckInValidator()
    {
        this.ValidCheckInContent();
    }
}
