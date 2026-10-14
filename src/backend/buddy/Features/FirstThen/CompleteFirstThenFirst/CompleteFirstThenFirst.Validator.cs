using FluentValidation;

namespace buddy.Features.FirstThen;

public sealed class CompleteFirstThenFirstValidator : AbstractValidator<CompleteFirstThenFirst>
{
    public CompleteFirstThenFirstValidator()
    {
        RuleFor(x => x.Revision).GreaterThanOrEqualTo(0);
    }
}
