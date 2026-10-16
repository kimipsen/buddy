using buddy.Common.Validation;
using buddy.Features.Guardians;
using buddy.Features.Users;

using FluentValidation;

namespace buddy.Features.FirstThen;

public static class EndFirstThenBoardHandler
{
    public static async Task<FirstThenWriteOutcome> Handle(
        EndFirstThenBoard command,
        IValidator<EndFirstThenBoard> validator,
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

        // Any state -> None, for both "we're finished" and "never mind". No board is an idempotent
        // no-op.
        return await FirstThenTransitions.ApplyAsync(
            command.UserId,
            command.ChildId,
            command.Revision,
            state => state == FirstThenState.None ? FirstThenTransition.Unchanged : FirstThenTransition.Append,
            (id, revision, now) => new FirstThenBoardEnded(id, revision, command.UserId, now),
            boards,
            guardians,
            users,
            time, cancellationToken);
    }
}
