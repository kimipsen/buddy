using FluentValidation;

namespace buddy.Features.Users;

public sealed class UpdateOnboardingProgressValidator : AbstractValidator<UpdateOnboardingProgress>
{
    public UpdateOnboardingProgressValidator()
    {
        RuleFor(x => x.Status)
            .Must(status => Enum.IsDefined(status) && status != OnboardingStatus.NotStarted)
            .WithMessage("Status must be Active, Deferred or Completed.");

        RuleFor(x => x.ExpectedVersion).GreaterThanOrEqualTo(0);

        RuleFor(x => x.SetupGroupId)
            .Must(id => id is null || id.Value != Guid.Empty)
            .WithMessage("SetupGroupId must be a group id.");

        // Finishing the guide means a group was set up.
        RuleFor(x => x.SetupGroupId)
            .NotNull()
            .When(x => x.Status == OnboardingStatus.Completed)
            .WithMessage("A completed guide needs a setup group.");
    }
}
