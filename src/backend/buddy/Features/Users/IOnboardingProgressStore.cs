namespace buddy.Features.Users;

public interface IOnboardingProgressStore
{
    Task<OnboardingProgress?> FindAsync(UserId userId, CancellationToken cancellationToken);

    // progress.Version is the revision the caller read (0 for none yet). A newer stored revision
    // throws JasperFx.ConcurrencyException, which ConcurrencyConflictMiddleware renders as 409.
    // Returns the saved progress with its new revision.
    Task<OnboardingProgress> SaveAsync(OnboardingProgress progress, CancellationToken cancellationToken);

    Task DeleteAsync(UserId userId, CancellationToken cancellationToken);
}
