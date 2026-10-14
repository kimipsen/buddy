using buddy.Common.Validation;

using FluentValidation;

namespace buddy.Features.Medicines;

public sealed class ListMedicineEffectNotesValidator : AbstractValidator<ListMedicineEffectNotes>
{
    public ListMedicineEffectNotesValidator()
    {
        this.ValidDateRange(x => x.From, x => x.To, ListMedicineEffectNotesHandler.MaxRangeDays);
    }
}
