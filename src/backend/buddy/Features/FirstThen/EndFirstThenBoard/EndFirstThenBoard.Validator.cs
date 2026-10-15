using FluentValidation;

namespace buddy.Features.FirstThen;

public sealed class EndFirstThenBoardValidator : AbstractValidator<EndFirstThenBoard>
{
    public EndFirstThenBoardValidator()
    {
        RuleFor(x => x.Revision).GreaterThanOrEqualTo(0);
    }
}
