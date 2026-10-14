using buddy.Features.Users;

namespace buddy.Features.FirstThen;

public sealed record FirstThenCardResponse(string Title, string Icon)
{
    public static FirstThenCardResponse From(FirstThenCard card) => new(card.Title, card.Icon.Value);
}

// The board as both tiers read it. A child with no stream yet gets Revision 0 and State None, not
// 404. LastModifiedByName is filled on guardian routes only: the given name, since the reader is
// another guardian (not the calling name children see).
public sealed record FirstThenBoardResponse(
    int Revision,
    FirstThenState State,
    FirstThenCardResponse? First,
    FirstThenCardResponse? Then,
    int? ThenMinutes,
    DateTimeOffset? UnlockedAt,
    DateTimeOffset? LastModifiedAt,
    string? LastModifiedByName)
{
    public static readonly FirstThenBoardResponse NoBoard = new(0, FirstThenState.None, null, null, null, null, null, null);

    public static FirstThenBoardResponse From(FirstThenBoard? board, string? lastModifiedByName = null) => board is null
        ? NoBoard
        : new FirstThenBoardResponse(
            board.Revision,
            board.State,
            board.State == FirstThenState.None ? null : FirstThenCardResponse.From(board.First),
            board.State == FirstThenState.None ? null : FirstThenCardResponse.From(board.Then),
            board.ThenMinutes,
            board.UnlockedAt,
            board.LastModifiedAt,
            lastModifiedByName);

    // The guardian's view: who last changed the board, by given name.
    public static async Task<FirstThenBoardResponse> ForGuardianAsync(FirstThenBoard? board, IUserEventStore users, CancellationToken cancellationToken)
    {
        if (board is null)
        {
            return NoBoard;
        }

        var modifiedBy = await users.FindSnapshotAsync(board.LastModifiedBy, cancellationToken);

        return From(board, modifiedBy?.Name.GivenName);
    }
}

public sealed record GuardianFirstThenResponse(
    FirstThenBoardResponse Board,
    IReadOnlyList<FirstThenCardResponse> RecentFirsts,
    IReadOnlyList<FirstThenCardResponse> RecentThens);
