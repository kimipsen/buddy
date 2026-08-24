using buddy.Common;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.Guardians;

public static class PreviewGuardianInviteEndpoint
{
    // Deliberately not behind RequireAuthorization() -- mirrors PreviewGroupInvite: the app shows
    // "you're invited to help manage X's account" before forcing a login, and the token itself is
    // the secret.
    public static RouteGroupBuilder MapPreviewGuardianInvite(this RouteGroupBuilder invites)
    {
        invites.MapGet("/{token}/preview", async Task<Results<Ok<GuardianInvitePreviewResponse>, NotFound>> (
            string token,
            IMessageBus bus,
            CancellationToken cancellationToken) =>
        {
            var result = await bus.InvokeAsync<Result<GuardianInvitePreview>>(new PreviewGuardianInvite(token), cancellationToken);

            return result switch
            {
                Result<GuardianInvitePreview>.Success(var preview) => TypedResults.Ok(new GuardianInvitePreviewResponse(preview.ChildGivenName, preview.Kind)),
                Result<GuardianInvitePreview>.NotFound => TypedResults.NotFound(),
                // PreviewGuardianInviteHandler never produces Forbidden/Validation -- collapsed
                // to NotFound since this route declares no other status for them.
                Result<GuardianInvitePreview>.Forbidden => TypedResults.NotFound(),
                Result<GuardianInvitePreview>.Validation => TypedResults.NotFound(),
            };
        })
        .WithName("PreviewGuardianInvite");

        return invites;
    }
}

public sealed record GuardianInvitePreviewResponse(string ChildGivenName, GuardianKind Kind);
