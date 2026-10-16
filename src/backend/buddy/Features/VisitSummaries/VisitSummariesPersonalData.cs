using buddy.Common.Erasure;
using buddy.Features.Guardians;
using buddy.Features.Users;

namespace buddy.Features.VisitSummaries;

// A child's visit summaries are the child's: erasing the child deletes them with their link
// documents. A guardian who is erased loses the links they created on summaries that stay with a
// co-guardian -- whoever they handed a link to shouldn't keep reading after they've gone -- while
// the summaries themselves stay (the sleep diary's rule). See gdpr-data-protection.md.
public sealed class VisitSummariesPersonalDataEraser(
    IVisitSummariesStore store,
    IVisitSummaryEventStore summaries,
    IGuardianLinkEventStore guardians,
    TimeProvider time) : IPersonalDataEraser
{
    public Type Store => typeof(IVisitSummariesStore);

    public async Task EraseGuardianAsync(ErasureSubject guardian, CancellationToken cancellationToken)
    {
        var links = await guardians.ListForGuardianAsync(guardian.UserId, cancellationToken);
        var revokedLinks = await guardians.ListRevokedForGuardianAsync(guardian.UserId, cancellationToken);

        foreach (var childId in links.Concat(revokedLinks).Select(l => new UserId(l.ChildId)).Distinct())
        {
            foreach (var summary in await summaries.ListSnapshotsForChildAsync(childId, cancellationToken))
            {
                var id = summary.Id;
                var now = time.GetUtcNow();
                VisitSummaryEvent[] revocations =
                [
                    .. summary.ShareLinks
                        .Where(l => !l.IsRevoked && l.CreatedBy == guardian.UserId)
                        .Select(l => (VisitSummaryEvent)new VisitSummaryShareLinkRevoked(id, l.Id, guardian.UserId, now))
                ];

                await summaries.AppendAsync(id, revocations, cancellationToken);
            }
        }
    }

    public async Task EraseChildAsync(ErasureSubject child, UserId? heir, CancellationToken cancellationToken)
    {
        foreach (var document in await summaries.ListForChildAsync(child.UserId, cancellationToken))
        {
            await store.DeleteStreamAsync<VisitSummarySnapshot>(document.Id, cancellationToken, session =>
            {
                session.Delete(document);
                session.DeleteWhere<VisitSummaryShareLinkDocument>(d => d.SummaryId == document.Id);
            });
        }
    }
}
