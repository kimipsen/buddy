using System.Collections.Immutable;
using System.Security.Claims;

using buddy.Common;
using buddy.Features.Users;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.Progress;

public static class ConfigureGoalPostsEndpoint
{
    public static RouteGroupBuilder MapConfigureGoalPosts(this RouteGroupBuilder progress)
    {
        progress.MapPut("/children/{childId:guid}/goals", async Task<Results<Ok<ProgressSummary>, NotFound, ForbidHttpResult, BadRequest<ErrorEnvelope>>> (
            ClaimsPrincipal principal,
            Guid childId,
            ConfigureGoalPostsRequest request,
            IMessageBus bus,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            // RespectNullableAnnotations doesn't cover collection elements (as ConfigureRewards).
            if (request.GoalPosts.Any(p => p is null))
            {
                return TypedResults.BadRequest(Common.Validation.ValidationProblem.Of("goalPosts must not contain null entries.").ToEnvelope(httpContext));
            }

            // Icon is trimmed like a reward's, so the validator and the stored event see the same text.
            var goalPosts = request.GoalPosts.Select(p => new GoalPost(p.Threshold, p.Icon.Trim(), FreeText.Normalize(p.Label))).ToImmutableArray();
            var command = ConfigureGoalPosts.FromClaims(principal, new UserId(childId), goalPosts);
            var result = await bus.InvokeAsync<Result<ProgressSummary>>(command, cancellationToken);

            return result switch
            {
                Result<ProgressSummary>.Success(var summary) => TypedResults.Ok(summary),
                Result<ProgressSummary>.Forbidden => TypedResults.Forbid(),
                Result<ProgressSummary>.NotFound => TypedResults.NotFound(),
                Result<ProgressSummary>.Validation(var problem) => TypedResults.BadRequest(problem.ToEnvelope(httpContext)),
            };
        })
        .WithName("ConfigureGoalPosts");

        return progress;
    }
}

public sealed record ConfigureGoalPostsRequest(IReadOnlyList<GoalPostRequest> GoalPosts);

public sealed record GoalPostRequest(int Threshold, string Icon, string? Label = null);
