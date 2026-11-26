using FluentValidation;

namespace buddy.Features.FlashCards;

public sealed class UpdateFlashCardValidator : AbstractValidator<UpdateFlashCard>
{
    public UpdateFlashCardValidator()
    {
        RuleFor(x => x.Question).ValidCardText();
        RuleFor(x => x.Answer).ValidCardText();
    }
}
