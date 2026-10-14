using FluentValidation;

namespace buddy.Features.Medicines;

// Structural rules only. "Not after the child's today" needs the child's time zone, so it stays in
// the handler. No lower bound on Date: a baseline week before medication is a valid record.
public sealed class LogMedicineEffectNoteValidator : AbstractValidator<LogMedicineEffectNote>
{
    public const int MaxNoteLength = 500;

    public LogMedicineEffectNoteValidator()
    {
        RuleForEach(x => x.Effects).ChildRules(rating =>
        {
            rating.RuleFor(r => r.Area).IsInEnum();
            rating.RuleFor(r => r.Level).IsInEnum();
        });
        RuleFor(x => x.Effects)
            .Must(list => list.Select(r => r.Area).Distinct().Count() == list.Count)
            .WithMessage("Each effect area can be rated once.");

        RuleForEach(x => x.SideEffects).ChildRules(rating =>
        {
            rating.RuleFor(r => r.Kind).IsInEnum();
            rating.RuleFor(r => r.Severity).IsInEnum();
        });
        RuleFor(x => x.SideEffects)
            .Must(list => list.Select(r => r.Kind).Distinct().Count() == list.Count)
            .WithMessage("Each side effect can be rated once.");

        RuleFor(x => x.Note).MaximumLength(MaxNoteLength);

        RuleFor(x => x)
            .Must(x => x.Effects.Count > 0 || x.SideEffects.Count > 0 || x.WoreOffAt is not null || x.Note.Length > 0)
            .WithName("note")
            .WithMessage("Rate at least one effect or side effect, give a wear-off time or write a note.");
    }
}
