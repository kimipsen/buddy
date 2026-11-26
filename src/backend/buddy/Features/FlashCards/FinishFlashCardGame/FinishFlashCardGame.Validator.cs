using FluentValidation;

namespace buddy.Features.FlashCards;

public sealed class FinishFlashCardGameValidator : AbstractValidator<FinishFlashCardGame>
{
    public const int CardKeyMaxLength = 100;

    public FinishFlashCardGameValidator()
    {
        RuleFor(x => x.GameId).NotEqual(Guid.Empty);
        RuleFor(x => x.SetId.Value).NotEqual(Guid.Empty).OverridePropertyName("setId");
        RuleFor(x => x.TierId.Value).NotEqual(Guid.Empty).OverridePropertyName("tierId");

        RuleFor(x => x.Results)
            .Must(r => r.Count is >= 1 and <= FlashCardLimits.MaxTierCards)
            .WithMessage($"Send between 1 and {FlashCardLimits.MaxTierCards} results.");

        // Slots 0..N-1, each exactly once.
        RuleFor(x => x.Results)
            .Must(r => r.Select(x => x.Slot).Order().SequenceEqual(Enumerable.Range(0, r.Count)))
            .WithMessage("Every slot from 0 to the last one must appear exactly once.");

        RuleForEach(x => x.Results).ChildRules(result =>
        {
            result.RuleFor(r => r.CardKey).NotEmpty().MaximumLength(CardKeyMaxLength);
            result.RuleFor(r => r.Question).MaximumLength(FlashCardLimits.TextMaxLength).NotEmpty().When(r => r.Question is not null);
            result.RuleFor(r => r.Answer).MaximumLength(FlashCardLimits.TextMaxLength).NotEmpty().When(r => r.Answer is not null);
        });
    }
}
