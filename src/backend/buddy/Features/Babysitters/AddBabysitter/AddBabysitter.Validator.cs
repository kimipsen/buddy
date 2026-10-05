using FluentValidation;

namespace buddy.Features.Babysitters;

public sealed class AddBabysitterValidator : AbstractValidator<AddBabysitter>
{
    public AddBabysitterValidator()
    {
        this.ValidBabysitterDetails(x => x.Name, x => x.ContactInfo);
    }
}
