using buddy.Common.Erasure;

namespace buddy.Features.Users;

// The setup guide's progress belongs to the user alone: erasure deletes it, the export includes it.
// See docs/backend/analysis/gdpr-data-protection.md.
public sealed class OnboardingPersonalDataEraser(IOnboardingProgressStore progress) : IPersonalDataEraser
{
    public Type Store => typeof(IUsersStore);

    public Task EraseGuardianAsync(ErasureSubject guardian, CancellationToken cancellationToken) =>
        progress.DeleteAsync(guardian.UserId, cancellationToken);

    // A child never runs the guide, but deleting a document that isn't there is harmless.
    public Task EraseChildAsync(ErasureSubject child, UserId? heir, CancellationToken cancellationToken) =>
        progress.DeleteAsync(child.UserId, cancellationToken);
}

public sealed class OnboardingPersonalDataExporter(IOnboardingProgressStore progress) : IPersonalDataExporter
{
    public Type Store => typeof(IUsersStore);

    public string Section => "onboarding";

    public async Task<object?> ExportAsync(ExportSubject subject, CancellationToken cancellationToken) =>
        await progress.FindAsync(subject.UserId, cancellationToken) is { } found ? OnboardingProgressResponse.From(found) : null;
}
