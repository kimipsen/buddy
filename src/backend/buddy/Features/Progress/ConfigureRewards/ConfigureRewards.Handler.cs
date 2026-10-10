using System.Collections.Immutable;

using buddy.Common;
using buddy.Common.Validation;
using buddy.Features.Guardians;

using FluentValidation;

namespace buddy.Features.Progress;

public static class ConfigureRewardsHandler
{
    public static async Task<Result<ProgressSummary>> Handle(
        ConfigureRewards command,
        IValidator<ConfigureRewards> validator,
        IProgressEventStore progress,
        IGuardianLinkEventStore guardians,
        CancellationToken cancellationToken)
    {
        if (await validator.ValidateCommandAsync(command, cancellationToken) is { } problem)
        {
            return new Result<ProgressSummary>.Validation(problem);
        }

        var access = await ProgressAuthorization.CheckManage(command.ChildId, command.UserId, guardians, cancellationToken);

        if (access != ProgressAccess.Allowed)
        {
            return access.ToDeniedResult<ProgressSummary>();
        }

        var id = ProgressId.ForChild(command.ChildId);
        var existing = ChildProgress.Rehydrate(await progress.ReadAsync(id, cancellationToken));
        var current = existing ?? ChildProgress.Initial(id, command.ChildId);

        // An id the catalog doesn't have can't be kept; the client is working from a stale or
        // made-up list.
        var knownIds = current.Rewards.Select(r => r.Id).ToHashSet();

        for (var i = 0; i < command.Rewards.Length; i++)
        {
            if (command.Rewards[i].Id is { } rewardId && !knownIds.Contains(rewardId))
            {
                return new Result<ProgressSummary>.Validation(new ValidationProblem(
                    new Dictionary<string, string[]> { [$"Rewards[{i}].Id"] = ["This reward is not in the child's catalog."] }));
            }
        }

        var rewards = command.Rewards
            .Select(draft => new Reward(draft.Id ?? RewardId.New(), draft.Name, draft.Icon, draft.Cost))
            .ToImmutableArray();

        if (current.Rewards.SequenceEqual(rewards))
        {
            // Idempotent, same as ConfigureGoalPostsHandler.
            return new Result<ProgressSummary>.Success(ProgressSummary.From(current));
        }

        var now = DateTimeOffset.UtcNow;
        var configured = new RewardsConfigured(id, rewards, command.UserId, now);

        if (existing is null)
        {
            await progress.CreateAsync(id, [new ProgressStarted(id, command.ChildId, now), configured], cancellationToken);
        }
        else
        {
            await progress.AppendAsync(id, [configured], cancellationToken);
        }

        return new Result<ProgressSummary>.Success(ProgressSummary.From(current with { Rewards = rewards }));
    }
}
