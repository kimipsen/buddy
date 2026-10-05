using FluentValidation;

namespace buddy.Features.Babysitters;

public sealed class UpdateBabysitterValidator : AbstractValidator<UpdateBabysitter>
{
    public UpdateBabysitterValidator()
    {
        this.ValidBabysitterDetails(x => x.Name, x => x.ContactInfo);
    }
}
