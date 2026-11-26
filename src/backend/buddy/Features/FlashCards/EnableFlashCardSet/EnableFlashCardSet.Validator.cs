using FluentValidation;

namespace buddy.Features.FlashCards;

public sealed class EnableFlashCardSetValidator : AbstractValidator<EnableFlashCardSet>
{
    public EnableFlashCardSetValidator()
    {
        RuleFor(x => x.Tiers)
            .Must(tiers => tiers.Count is >= FlashCardLimits.MinTiers and <= FlashCardLimits.MaxTiers)
            .WithMessage($"Offer between {FlashCardLimits.MinTiers} and {FlashCardLimits.MaxTiers} tiers.");

        RuleForEach(x => x.Tiers).ChildRules(tier =>
        {
            tier.RuleFor(t => t.CardCount).InclusiveBetween(FlashCardLimits.MinTierCards, FlashCardLimits.MaxTierCards);
            tier.RuleFor(t => t.Stars).InclusiveBetween(FlashCardLimits.MinTierStars, FlashCardLimits.MaxTierStars);
        });

        RuleFor(x => x.Tiers)
            .Must(tiers => tiers.Select(t => t.CardCount).Distinct().Count() == tiers.Count)
            .WithMessage("Each tier needs a different card count.");

        RuleFor(x => x.Tiers)
            .Must(tiers => tiers.Where(t => t.Id is not null).Select(t => t.Id).Distinct().Count() == tiers.Count(t => t.Id is not null))
            .WithMessage("A tier id can only be used once.");
    }
}
