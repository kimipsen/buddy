using FluentValidation;

namespace buddy.Features.FlashCards;

public sealed class CreateFlashCardSetValidator : AbstractValidator<CreateFlashCardSet>
{
    public CreateFlashCardSetValidator()
    {
        RuleFor(x => x.Name).ValidSetName();
        RuleFor(x => x.Kind).IsInEnum();
        // A built-in set is catalogue content: a stored set of that kind would have no catalogue entry.
        RuleFor(x => x.Kind).NotEqual(FlashCardSetKind.BuiltIn).WithMessage("A built-in set cannot be created.");

        // A Math set is defined by its rule alone; a Text set has no rule and gets cards added later.
        RuleFor(x => x.Math).NotNull().WithMessage("A math set needs a math rule.").When(x => x.Kind == FlashCardSetKind.Math);
        RuleFor(x => x.Math).Null().WithMessage("A text set cannot have a math rule.").When(x => x.Kind == FlashCardSetKind.Text);
        RuleFor(x => x.Math).ValidMathRule();
    }
}
