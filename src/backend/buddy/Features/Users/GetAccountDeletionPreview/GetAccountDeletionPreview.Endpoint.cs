using System.Security.Claims;

using buddy.Features.Privacy;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.Users;

public static class GetAccountDeletionPreviewEndpoint
{
    public static RouteGroupBuilder MapGetAccountDeletionPreview(this RouteGroupBuilder users)
    {
        users.MapGet("/me/deletion-preview", async Task<Ok<AccountDeletionPreview>> (
            ClaimsPrincipal principal,
            IMessageBus bus,
            CancellationToken cancellationToken) =>
            TypedResults.Ok(await bus.InvokeAsync<AccountDeletionPreview>(GetAccountDeletionPreview.FromClaims(principal), cancellationToken)))
        .WithName("GetAccountDeletionPreview");

        return users;
    }
}
