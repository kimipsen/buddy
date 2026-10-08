using buddy.Common;
using buddy.Common.Validation;
using buddy.Features.Groups;

using FluentValidation;

using JasperFx;

namespace buddy.Features.Users;

public static class UpdateOnboardingProgressHandler
{
    public static async Task<Result<OnboardingProgress>> Handle(
        UpdateOnboardingProgress command,
        IValidator<UpdateOnboardingProgress> validator,
        IOnboardingProgressStore store,
        IGroupEventStore groups,
        CancellationToken cancellationToken)
    {
        if (await validator.ValidateCommandAsync(command, cancellationToken) is { } problem)
        {
            return new Result<OnboardingProgress>.Validation(problem);
        }

        var current = await store.FindAsync(command.UserId, cancellationToken) ?? OnboardingProgress.NotStarted(command.UserId);

        // Same 409 concurrency_conflict as a lost race inside SaveAsync: the caller read an older
        // revision (another tab wrote since) and must reload rather than overwrite newer progress.
        if (current.Version != command.ExpectedVersion)
        {
            throw new ConcurrencyException(typeof(OnboardingProgressDocument), command.UserId.Value);
        }

        var updated = current with
        {
            Status = command.Status,
            SetupGroupId = command.SetupGroupId,
            InvitationsSkipped = command.InvitationsSkipped
        };

        if (updated == current)
        {
            return new Result<OnboardingProgress>.Success(current);
        }

        // A completed guide stays completed: a guardian who later loses every group isn't sent back
        // into it. Active and Deferred move freely between each other and to Completed.
        if (current.Status == OnboardingStatus.Completed)
        {
            return new Result<OnboardingProgress>.Validation(
                ValidationProblem.Of("A completed guide can't be changed."));
        }

        if (current.SetupGroupId is not null && updated.SetupGroupId is null)
        {
            return new Result<OnboardingProgress>.Validation(
                ValidationProblem.Of("The setup group can be replaced but not cleared."));
        }

        // Only a changed reference is checked: a guide whose group was deleted later must still be
        // deferrable, so the guardian can leave it (the frontend blocks the dependent steps).
        if (updated.SetupGroupId is { } groupId && updated.SetupGroupId != current.SetupGroupId)
        {
            var group = await groups.FindSnapshotAsync(groupId, cancellationToken);

            if (group is null || GroupAuthorization.CheckManage(group, command.UserId) != GroupAccess.Allowed)
            {
                return new Result<OnboardingProgress>.Validation(
                    ValidationProblem.Of("SetupGroupId must be a group you manage."));
            }
        }

        return new Result<OnboardingProgress>.Success(await store.SaveAsync(updated, cancellationToken));
    }
}
