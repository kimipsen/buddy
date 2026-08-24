using buddy.Common;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.Groups;

public static class PreviewGroupInviteEndpoint
{
    // Deliberately not behind RequireAuthorization() -- the app shows "You've been invited to X"
    // before forcing a login. The token itself is the secret (same exposure model as the email
    // verification link), and this only ever returns a group name, nothing sensitive.
    public static RouteGroupBuilder MapPreviewGroupInvite(this RouteGroupBuilder invites)
    {
        invites.MapGet("/{token}/preview", async Task<Results<Ok<GroupInvitePreviewResponse>, NotFound>> (
            string token,
            IMessageBus bus,
            CancellationToken cancellationToken) =>
        {
            var result = await bus.InvokeAsync<Result<GroupInvitePreview>>(new PreviewGroupInvite(token), cancellationToken);

            return result switch
            {
                Result<GroupInvitePreview>.Success(var preview) => TypedResults.Ok(new GroupInvitePreviewResponse(preview.GroupName)),
                Result<GroupInvitePreview>.NotFound => TypedResults.NotFound(),
                // PreviewGroupInviteHandler never produces Forbidden/Validation -- collapsed to
                // NotFound since this route declares no other status for them.
                Result<GroupInvitePreview>.Forbidden => TypedResults.NotFound(),
                Result<GroupInvitePreview>.Validation => TypedResults.NotFound(),
            };
        })
        .WithName("PreviewGroupInvite");

        return invites;
    }
}

public sealed record GroupInvitePreviewResponse(string GroupName);
