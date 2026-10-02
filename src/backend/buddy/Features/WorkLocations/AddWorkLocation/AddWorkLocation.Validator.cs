using FluentValidation;

namespace buddy.Features.WorkLocations;

public sealed class AddWorkLocationValidator : AbstractValidator<AddWorkLocation>
{
    public AddWorkLocationValidator()
    {
        this.ValidLocationDetails(x => x.Name, x => x.Icon, x => x.Color);
    }
}
