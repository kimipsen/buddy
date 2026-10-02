using FluentValidation;

namespace buddy.Features.WorkLocations;

public sealed class UpdateWorkLocationValidator : AbstractValidator<UpdateWorkLocation>
{
    public UpdateWorkLocationValidator()
    {
        this.ValidLocationDetails(x => x.Name, x => x.Icon, x => x.Color);
    }
}
