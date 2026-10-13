using FluentValidation;

namespace buddy.Features.Feelings;

public sealed class GetSharedFeelingsValidator : AbstractValidator<GetSharedFeelings>
{
    public GetSharedFeelingsValidator()
    {
        // Only a range given in full can be wrong; a missing end is filled in 14 days from the other.
        When(x => x.From.HasValue && x.To.HasValue, () =>
        {
            RuleFor(x => x.To)
                .Must((query, to) => to >= query.From)
                .WithMessage("'to' must not be before 'from'.");

            RuleFor(x => x.To)
                .Must((query, to) => to!.Value.DayNumber - query.From!.Value.DayNumber <= FeelingsRules.MaxRangeDays)
                .WithMessage($"The requested range cannot exceed {FeelingsRules.MaxRangeDays} days.");
        });
    }
}
