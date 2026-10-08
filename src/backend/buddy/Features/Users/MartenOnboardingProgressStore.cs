using JasperFx;

namespace buddy.Features.Users;

public sealed class MartenOnboardingProgressStore(IUsersStore store) : IOnboardingProgressStore
{
    public async Task<OnboardingProgress?> FindAsync(UserId userId, CancellationToken cancellationToken)
    {
        await using var session = store.QuerySession();

        return (await session.LoadAsync<OnboardingProgressDocument>(userId.Value, cancellationToken))?.ToProgress();
    }

    public async Task<OnboardingProgress> SaveAsync(OnboardingProgress progress, CancellationToken cancellationToken)
    {
        var document = OnboardingProgressDocument.From(progress);

        await using var session = store.LightweightSession();

        if (progress.Version == 0)
        {
            // Two first writes racing: the insert's primary key decides, the loser gets the 409.
            session.Insert(document);
        }
        else
        {
            // Rejected when the stored revision is already at or past the new one, i.e. someone else
            // wrote after this caller read.
            session.UpdateRevision(document, progress.Version + 1);
        }

        try
        {
            await session.SaveChangesAsync(cancellationToken);
        }
        catch (DocumentAlreadyExistsException exception)
        {
            throw new ConcurrencyException("Onboarding progress was created by another request.", exception);
        }

        return document.ToProgress();
    }

    public async Task DeleteAsync(UserId userId, CancellationToken cancellationToken)
    {
        await using var session = store.LightweightSession();
        session.Delete<OnboardingProgressDocument>(userId.Value);
        await session.SaveChangesAsync(cancellationToken);
    }
}
