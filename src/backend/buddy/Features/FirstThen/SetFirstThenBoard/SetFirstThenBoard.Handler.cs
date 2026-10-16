using buddy.Common.Validation;
using buddy.Features.Guardians;
using buddy.Features.Users;

using FluentValidation;

namespace buddy.Features.FirstThen;

public static class SetFirstThenBoardHandler
{
    public static async Task<FirstThenWriteOutcome> Handle(
        SetFirstThenBoard command,
        IValidator<SetFirstThenBoard> validator,
        IFirstThenBoardEventStore boards,
        IGuardianLinkEventStore guardians,
        IUserEventStore users,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        if (await validator.ValidateCommandAsync(command, cancellationToken) is { } problem)
        {
            return new FirstThenWriteOutcome.Validation(problem);
        }

        var access = await FirstThenAuthorization.CheckManage(command.ChildId, command.UserId, guardians, cancellationToken);

        if (access != FirstThenAccess.Allowed)
        {
            return access.ToDeniedOutcome();
        }

        var id = FirstThenBoardId.ForChild(command.ChildId);
        var events = await boards.ReadAsync(id, cancellationToken);
        var board = FirstThenBoard.Rehydrate(events);
        var current = board?.Revision ?? 0;

        // Two guardians setting from the same revision: the second must see the first's board before
        // replacing it.
        if (command.Revision != current)
        {
            return new FirstThenWriteOutcome.BoardChanged(current);
        }

        var now = time.GetUtcNow();

        // Always a full overwrite into Waiting, from any state -- a new board even when the cards are
        // the same, because the First hasn't been done again yet.
        FirstThenEvent set = new FirstThenBoardSet(id, current + 1, command.First, command.Then, command.ThenMinutes, command.UserId, now);

        if (board is null)
        {
            FirstThenEvent started = new FirstThenBoardStarted(id, command.ChildId, now);
            await boards.CreateAsync(id, [started, set], cancellationToken);
            board = FirstThenBoard.Replay([started, set]);
        }
        else
        {
            await boards.AppendAsync(id, [set], cancellationToken);
            board = FirstThenBoard.Advance(board, set);
        }

        return new FirstThenWriteOutcome.Success(await FirstThenBoardResponse.ForGuardianAsync(board, users, cancellationToken));
    }
}
