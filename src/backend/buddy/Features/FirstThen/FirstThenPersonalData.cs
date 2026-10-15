using buddy.Common.Erasure;
using buddy.Features.Users;

namespace buddy.Features.FirstThen;

// A child's board stream is the child's alone: erasing the child deletes it. An erased guardian's
// SetBy/CompletedBy/ReopenedBy/EndedBy stay as the user id, which is a pseudonym once the user is
// erased, as for sleep diary entries (docs/backend/analysis/first-then-board.md, Privacy).
public sealed class FirstThenPersonalDataEraser(IFirstThenStore store) : IPersonalDataEraser
{
    public Type Store => typeof(IFirstThenStore);

    public Task EraseGuardianAsync(ErasureSubject guardian, CancellationToken cancellationToken) => Task.CompletedTask;

    public async Task EraseChildAsync(ErasureSubject child, UserId? heir, CancellationToken cancellationToken) =>
        await store.DeleteStreamAsync<FirstThenBoardSnapshot>(FirstThenBoardId.ForChild(child.UserId).Value, cancellationToken);
}
