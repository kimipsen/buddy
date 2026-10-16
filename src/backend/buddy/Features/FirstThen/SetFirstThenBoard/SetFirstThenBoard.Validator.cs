using buddy.Features.Calendars;

using FluentValidation;

namespace buddy.Features.FirstThen;

// A First and a Then with the same title are allowed: "first Lego tidy-up, then Lego" is real.
public sealed class SetFirstThenBoardValidator : AbstractValidator<SetFirstThenBoard>
{
    public const int MaxTitleLength = 60;

    public const int MinThenMinutes = 5;
    public const int MaxThenMinutes = 120;

    public SetFirstThenBoardValidator()
    {
        RuleFor(x => x.Revision).GreaterThanOrEqualTo(0);
        RuleFor(x => x.First).SetValidator(new FirstThenCardValidator("first"));
        RuleFor(x => x.Then).SetValidator(new FirstThenCardValidator("then"));
        RuleFor(x => x.ThenMinutes)
            .InclusiveBetween(MinThenMinutes, MaxThenMinutes)
            .When(x => x.ThenMinutes is not null)
            .WithMessage($"thenMinutes must be between {MinThenMinutes} and {MaxThenMinutes}.");
    }

    private sealed class FirstThenCardValidator : AbstractValidator<FirstThenCard>
    {
        public FirstThenCardValidator(string card)
        {
            RuleFor(c => c.Title).NotEmpty().MaximumLength(MaxTitleLength).OverridePropertyName($"{card}.title");
            // Required: a First-Then board is a visual support, and many children who need one
            // don't read yet.
            RuleFor(c => c.Icon.Value).NotEmpty().ValidIcon().OverridePropertyName($"{card}.icon");
            // A pictogram name may be longer than an emoji (docs/frontend/analysis/pictograms.md).
            RuleFor(c => c.Icon.Value)
                .MaximumLength(Icon.MaxEmojiLength)
                .When(c => !c.Icon.IsPictogram)
                .OverridePropertyName($"{card}.icon");
        }
    }
}
