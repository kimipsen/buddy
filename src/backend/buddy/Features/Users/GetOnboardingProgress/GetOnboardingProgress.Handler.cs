namespace buddy.Features.Users;

public static class GetOnboardingProgressHandler
{
    public static async Task<OnboardingProgress> Handle(
        GetOnboardingProgress query,
        IOnboardingProgressStore progress,
        CancellationToken cancellationToken) =>
        await progress.FindAsync(query.UserId, cancellationToken) ?? OnboardingProgress.NotStarted(query.UserId);
}
