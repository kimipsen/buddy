using System.Collections.Immutable;
using System.Security.Claims;

using buddy.Common;
using buddy.Features.Users;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.Progress;

public static class ConfigureRewardsEndpoint
{
    public static RouteGroupBuilder MapConfigureRewards(this RouteGroupBuilder progress)
    {
        progress.MapPut("/children/{childId:guid}/rewards", async Task<Results<Ok<ProgressSummary>, NotFound, ForbidHttpResult, BadRequest<ErrorEnvelope>>> (
            ClaimsPrincipal principal,
            Guid childId,
            ConfigureRewardsRequest request,
            IMessageBus bus,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            // RespectNullableAnnotations doesn't cover collection elements (see ReplaceWorkPattern).
            if (request.Rewards.Any(r => r is null))
            {
                return TypedResults.BadRequest(Common.Validation.ValidationProblem.Of("rewards must not contain null entries.").ToEnvelope(httpContext));
            }

            var rewards = request.Rewards
                .Select(r => new RewardDraft(r.Id is { } id ? new RewardId(id) : null, r.Name.Trim(), r.Icon.Trim(), r.Cost))
                .ToImmutableArray();
            var command = ConfigureRewards.FromClaims(principal, new UserId(childId), rewards);
            var result = await bus.InvokeAsync<Result<ProgressSummary>>(command, cancellationToken);

            return result switch
            {
                Result<ProgressSummary>.Success(var summary) => TypedResults.Ok(summary),
                Result<ProgressSummary>.Forbidden => TypedResults.Forbid(),
                Result<ProgressSummary>.NotFound => TypedResults.NotFound(),
                Result<ProgressSummary>.Validation(var problem) => TypedResults.BadRequest(problem.ToEnvelope(httpContext)),
            };
        })
        .WithName("ConfigureRewards");

        return progress;
    }
}

public sealed record ConfigureRewardsRequest(IReadOnlyList<RewardBody> Rewards);

// Id is omitted for a new reward (optional, so it has to come last with a default, like
// GoalPostRequest.Label).
public sealed record RewardBody(string Name, string Icon, int Cost, Guid? Id = null);
