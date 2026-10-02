using buddy.Features.Groups;
using buddy.Features.Users;

namespace buddy.Features.PrintTemplates;

public interface IPrintTemplateEventStore
{
    Task<IReadOnlyCollection<PrintTemplateEvent>> ReadAsync(PrintTemplateId id, CancellationToken cancellationToken);

    Task<PrintTemplate?> FindSnapshotAsync(PrintTemplateId id, CancellationToken cancellationToken);

    // Starts the stream and writes its index row in one session.
    Task CreateAsync(PrintTemplateId id, IReadOnlyCollection<PrintTemplateEvent> events, PrintTemplateIndexDocument index, CancellationToken cancellationToken);

    // index is written in the same session when given -- the rename and delete appends change it.
    Task AppendAsync(PrintTemplateId id, IReadOnlyCollection<PrintTemplateEvent> events, PrintTemplateIndexDocument? index, CancellationToken cancellationToken);

    // Non-deleted templates owned by userId or by any of groupIds, ordered by name.
    Task<IReadOnlyCollection<PrintTemplateIndexDocument>> ListAsync(UserId userId, IReadOnlyCollection<GroupId> groupIds, CancellationToken cancellationToken);
}
