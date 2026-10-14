using buddy.Common.Erasure;

namespace buddy.Features.FirstThen;

// The "firstThen" section: each child's current board and recent cards. Past boards stay in the
// event stream and aren't exported one by one, matching the sleep diary's export of current state.
public sealed class FirstThenPersonalDataExporter(IFirstThenBoardEventStore boards) : IPersonalDataExporter
{
    public Type Store => typeof(IFirstThenStore);

    public string Section => "firstThen";

    public async Task<object?> ExportAsync(ExportSubject subject, CancellationToken cancellationToken)
    {
        List<ExportedFirstThenBoard> children = [];

        foreach (var childId in subject.Children)
        {
            var board = await boards.FindSnapshotAsync(FirstThenBoardId.ForChild(childId), cancellationToken);

            children.Add(new ExportedFirstThenBoard(
                childId.Value,
                FirstThenBoardResponse.From(board),
                [.. (board?.RecentFirsts ?? []).Select(FirstThenCardResponse.From)],
                [.. (board?.RecentThens ?? []).Select(FirstThenCardResponse.From)]));
        }

        return children;
    }
}

public sealed record ExportedFirstThenBoard(
    Guid ChildId,
    FirstThenBoardResponse Board,
    IReadOnlyList<FirstThenCardResponse> RecentFirsts,
    IReadOnlyList<FirstThenCardResponse> RecentThens);
