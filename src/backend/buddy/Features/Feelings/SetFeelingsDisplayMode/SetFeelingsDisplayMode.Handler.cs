using buddy.Common;
using buddy.Common.Validation;
using buddy.Features.Guardians;

using FluentValidation;

namespace buddy.Features.Feelings;

// The faces-only switch for a young child (docs/backend/analysis/feelings-check-in.md, Question 9).
public static class SetFeelingsDisplayModeHandler
{
    public static async Task<Result<DisplayModeResponse>> Handle(
        SetFeelingsDisplayMode command,
        IValidator<SetFeelingsDisplayMode> validator,
        IFeelingsJournalEventStore journals,
        IGuardianLinkEventStore guardians,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        if (await validator.ValidateCommandAsync(command, cancellationToken) is { } problem)
        {
            return new Result<DisplayModeResponse>.Validation(problem);
        }

        var access = await FeelingsAuthorization.CheckManage(command.ChildId, command.UserId, guardians, cancellationToken);

        if (access != FeelingsAccess.Allowed)
        {
            return access.ToDeniedResult<DisplayModeResponse>();
        }

        var id = FeelingsJournalId.ForChild(command.ChildId);
        var journal = FeelingsJournal.Rehydrate(await journals.ReadAsync(id, cancellationToken));
        var before = journal?.DisplayMode ?? FeelingsDisplayMode.FacesAndWords;

        // The same mode appends nothing -- including the default on a child with no journal yet.
        if (before == command.DisplayMode)
        {
            return new Result<DisplayModeResponse>.Success(new DisplayModeResponse(before));
        }

        var now = time.GetUtcNow();

        await FeelingsJournalWrites.WriteAsync(
            journals,
            id,
            journal,
            new FeelingsJournalStarted(id, command.ChildId, now),
            new FeelingsDisplayModeSet(id, before, command.DisplayMode, command.UserId, now),
            cancellationToken);

        return new Result<DisplayModeResponse>.Success(new DisplayModeResponse(command.DisplayMode));
    }
}
