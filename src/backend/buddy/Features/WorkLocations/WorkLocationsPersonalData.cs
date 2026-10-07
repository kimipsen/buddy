using buddy.Common.Erasure;
using buddy.Features.Users;

namespace buddy.Features.WorkLocations;

// A guardian's work-location schedule is theirs alone, keyed by their UserId: erasing them deletes
// it. See gdpr-data-protection.md.
public sealed class WorkLocationsPersonalDataEraser(IWorkLocationsStore store) : IPersonalDataEraser
{
    public Type Store => typeof(IWorkLocationsStore);

    public Task EraseGuardianAsync(ErasureSubject guardian, CancellationToken cancellationToken) =>
        store.DeleteStreamAsync<WorkLocationScheduleSnapshot>(WorkLocationScheduleId.ForGuardian(guardian.UserId).Value, cancellationToken);

    public Task EraseChildAsync(ErasureSubject child, UserId? heir, CancellationToken cancellationToken) => Task.CompletedTask;
}
