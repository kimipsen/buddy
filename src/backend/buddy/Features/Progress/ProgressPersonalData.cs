using buddy.Common.Erasure;
using buddy.Features.Users;

namespace buddy.Features.Progress;

// A child's stars, milestones and goal posts are theirs alone, keyed by their UserId: erasing the
// child deletes them. See gdpr-data-protection.md.
public sealed class ProgressPersonalDataEraser(IProgressStore store) : IPersonalDataEraser
{
    public Type Store => typeof(IProgressStore);

    public Task EraseGuardianAsync(ErasureSubject guardian, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task EraseChildAsync(ErasureSubject child, UserId? heir, CancellationToken cancellationToken) =>
        store.DeleteStreamAsync<ChildProgressSnapshot>(ProgressId.ForChild(child.UserId).Value, cancellationToken);
}
