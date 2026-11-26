using FluentValidation;

namespace buddy.Features.FlashCards;

public sealed class AddFlashCardValidator : AbstractValidator<AddFlashCard>
{
    public AddFlashCardValidator()
    {
        RuleFor(x => x.Question).ValidCardText();
        RuleFor(x => x.Answer).ValidCardText();
    }
}
