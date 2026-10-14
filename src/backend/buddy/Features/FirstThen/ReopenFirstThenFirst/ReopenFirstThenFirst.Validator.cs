using FluentValidation;

namespace buddy.Features.FirstThen;

public sealed class ReopenFirstThenFirstValidator : AbstractValidator<ReopenFirstThenFirst>
{
    public ReopenFirstThenFirstValidator()
    {
        RuleFor(x => x.Revision).GreaterThanOrEqualTo(0);
    }
}
