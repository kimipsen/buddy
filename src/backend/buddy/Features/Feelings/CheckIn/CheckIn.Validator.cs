using FluentValidation;

namespace buddy.Features.Feelings;

public sealed class CheckInValidator : AbstractValidator<CheckIn>
{
    public CheckInValidator()
    {
        this.ValidCheckInContent();
    }
}
