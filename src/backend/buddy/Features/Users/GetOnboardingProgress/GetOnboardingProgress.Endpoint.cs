using System.Security.Claims;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.Users;

public static class GetOnboardingProgressEndpoint
{
    public static RouteGroupBuilder MapGetOnboardingProgress(this RouteGroupBuilder users)
    {
        // Always 200: a user who never started the guide gets NotStarted with version 0, which is
        // the state the frontend's eligibility check needs, rather than a 404 it would have to tell
        // apart from a failed lookup.
        users.MapGet("/me/onboarding", async Task<Ok<OnboardingProgressResponse>> (
            ClaimsPrincipal principal,
            IMessageBus bus,
            CancellationToken cancellationToken) =>
        {
            var progress = await bus.InvokeAsync<OnboardingProgress>(GetOnboardingProgress.FromClaims(principal), cancellationToken);

            return TypedResults.Ok(OnboardingProgressResponse.From(progress));
        })
        .WithName("GetOnboardingProgress");

        return users;
    }
}

public sealed record OnboardingProgressResponse(
    OnboardingStatus Status,
    Guid? SetupGroupId,
    bool InvitationsSkipped,
    int Version)
{
    public static OnboardingProgressResponse From(OnboardingProgress progress) => new(
        progress.Status,
        progress.SetupGroupId?.Value,
        progress.InvitationsSkipped,
        progress.Version);
}
