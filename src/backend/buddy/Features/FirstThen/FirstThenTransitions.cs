using System.Diagnostics;

using buddy.Features.Guardians;
using buddy.Features.Users;

namespace buddy.Features.FirstThen;

// What a state change does to a board in a given state (first-then-board.md, Question 4).
public enum FirstThenTransition
{
    Append,
    // Already there: succeed without appending, as the idempotent clears elsewhere do.
    Unchanged,
    // Complete/reopen with no current board.
    NoBoard
}

// The shared path of CompleteFirstThenFirst, ReopenFirstThenFirst and EndFirstThenBoard, for both
// route families: authorize, read, check the revision, decide, append if anything changed.
internal static class FirstThenTransitions
{
    public static async Task<FirstThenWriteOutcome> ApplyAsync(
        UserId callerId,
        // Null on the child's /me routes: the board is the caller's own.
        UserId? childId,
        int revision,
        Func<FirstThenState, FirstThenTransition> decide,
        Func<FirstThenBoardId, int, DateTimeOffset, FirstThenEvent> toEvent,
        IFirstThenBoardEventStore boards,
        IGuardianLinkEventStore guardians,
        IUserEventStore users,
        CancellationToken cancellationToken)
    {
        var access = childId is { } managed
            ? await FirstThenAuthorization.CheckManage(managed, callerId, guardians, cancellationToken)
            : await FirstThenAuthorization.CheckOwn(callerId, guardians, cancellationToken);

        if (access != FirstThenAccess.Allowed)
        {
            return access.ToDeniedOutcome();
        }

        var id = FirstThenBoardId.ForChild(childId ?? callerId);
        var board = FirstThenBoard.Rehydrate(await boards.ReadAsync(id, cancellationToken));
        var current = board?.Revision ?? 0;

        // Checked before the state: a tick meant for a board a guardian has since replaced (or ended
        // and replaced) must not land on the new one.
        if (revision != current)
        {
            return new FirstThenWriteOutcome.BoardChanged(current);
        }

        var transition = decide(board?.State ?? FirstThenState.None);

        if (transition == FirstThenTransition.NoBoard)
        {
            return new FirstThenWriteOutcome.NotFound();
        }

        if (transition == FirstThenTransition.Append)
        {
            // decide() only appends from Waiting or Unlocked, which a missing stream never is.
            if (board is null)
            {
                throw new UnreachableException("A FirstThen transition appended to a board that doesn't exist.");
            }

            var @event = toEvent(id, current, DateTimeOffset.UtcNow);
            await boards.AppendAsync(id, [@event], cancellationToken);
            board = FirstThenBoard.Advance(board, @event);
        }

        return new FirstThenWriteOutcome.Success(childId is null
            ? FirstThenBoardResponse.From(board)
            : await FirstThenBoardResponse.ForGuardianAsync(board, users, cancellationToken));
    }
}
