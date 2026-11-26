using FluentValidation;

namespace buddy.Features.FlashCards;

public sealed class UpdateFlashCardSetValidator : AbstractValidator<UpdateFlashCardSet>
{
    public UpdateFlashCardSetValidator()
    {
        RuleFor(x => x.Name).ValidSetName();
        RuleFor(x => x.Math).ValidMathRule();
    }
}
