using FluentValidation;

namespace buddy.Features.Groups;

public sealed class CreateGroupValidator : AbstractValidator<CreateGroup>
{
    public const int NameMaxLength = 200;

    public CreateGroupValidator()
    {
        // The endpoint trims the name first, so a whitespace-only name is empty here and the
        // trimmed name is what gets stored.
        RuleFor(x => x.Name).NotEmpty().MaximumLength(NameMaxLength);
    }
}
