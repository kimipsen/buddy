using buddy.Common.Validation;
using buddy.Features.Guardians;
using buddy.Features.Users;

using FluentValidation;

namespace buddy.Features.FirstThen;

public static class ReopenFirstThenFirstHandler
{
    public static async Task<FirstThenWriteOutcome> Handle(
        ReopenFirstThenFirst command,
        IValidator<ReopenFirstThenFirst> validator,
        IFirstThenBoardEventStore boards,
        IGuardianLinkEventStore guardians,
        IUserEventStore users,
        CancellationToken cancellationToken)
    {
        if (await validator.ValidateCommandAsync(command, cancellationToken) is { } problem)
        {
            return new FirstThenWriteOutcome.Validation(problem);
        }

        // Unlocked -> Waiting, for the mis-tap. Already Waiting is an idempotent no-op; no board is 404.
        return await FirstThenTransitions.ApplyAsync(
            command.UserId,
            command.ChildId,
            command.Revision,
            state => state switch
            {
                FirstThenState.Unlocked => FirstThenTransition.Append,
                FirstThenState.Waiting => FirstThenTransition.Unchanged,
                FirstThenState.None => FirstThenTransition.NoBoard,
                _ => throw new ArgumentOutOfRangeException(nameof(state), state, null),
            },
            (id, revision, now) => new FirstThenFirstReopened(id, revision, command.UserId, now),
            boards,
            guardians,
            users,
            cancellationToken);
    }
}
