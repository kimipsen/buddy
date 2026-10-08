using System.Diagnostics;
using System.Security.Claims;

using buddy.Common;
using buddy.Features.Groups;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.Users;

public static class UpdateOnboardingProgressEndpoint
{
    public static RouteGroupBuilder MapUpdateOnboardingProgress(this RouteGroupBuilder users)
    {
        // A stale Version answers 409 concurrency_conflict through ConcurrencyConflictMiddleware.
        users.MapPut("/me/onboarding", async Task<Results<Ok<OnboardingProgressResponse>, BadRequest<ErrorEnvelope>>> (
            ClaimsPrincipal principal,
            UpdateOnboardingProgressRequest request,
            IMessageBus bus,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var command = UpdateOnboardingProgress.FromClaims(
                principal,
                request.Status,
                request.SetupGroupId is { } groupId ? new GroupId(groupId) : null,
                request.InvitationsSkipped,
                request.Version);

            var result = await bus.InvokeAsync<Result<OnboardingProgress>>(command, cancellationToken);

            return result switch
            {
                Result<OnboardingProgress>.Success(var progress) => TypedResults.Ok(OnboardingProgressResponse.From(progress)),
                Result<OnboardingProgress>.Validation(var problem) => TypedResults.BadRequest(problem.ToEnvelope(httpContext)),
                // The caller only ever touches their own document, so there is nothing to hide or deny.
                Result<OnboardingProgress>.NotFound or Result<OnboardingProgress>.Forbidden =>
                    throw new UnreachableException("UpdateOnboardingProgressHandler never denies access."),
            };
        })
        .WithName("UpdateOnboardingProgress");

        return users;
    }
}

public sealed record UpdateOnboardingProgressRequest(
    OnboardingStatus Status,
    Guid? SetupGroupId,
    bool InvitationsSkipped,
    int Version);
