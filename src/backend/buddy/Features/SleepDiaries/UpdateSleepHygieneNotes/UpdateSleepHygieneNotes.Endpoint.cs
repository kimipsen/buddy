using System.Security.Claims;

using buddy.Common;
using buddy.Features.Users;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.SleepDiaries;

public static class UpdateSleepHygieneNotesEndpoint
{
    public static RouteGroupBuilder MapUpdateSleepHygieneNotes(this RouteGroupBuilder sleepDiary)
    {
        sleepDiary.MapPut("/children/{childId:guid}/hygiene-notes", async Task<Results<NoContent, NotFound, BadRequest<ErrorEnvelope>>> (
            ClaimsPrincipal principal,
            Guid childId,
            UpdateSleepHygieneNotesRequest request,
            IMessageBus bus,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var command = UpdateSleepHygieneNotes.FromClaims(principal, new UserId(childId), FreeText.Normalize(request.Notes));
            var result = await bus.InvokeAsync<Result<Unit>>(command, cancellationToken);

            return result switch
            {
                Result<Unit>.Success => TypedResults.NoContent(),
                Result<Unit>.Validation(var problem) => TypedResults.BadRequest(problem.ToEnvelope(httpContext)),
                Result<Unit>.NotFound => TypedResults.NotFound(),
                // Guardian-only feature: no Forbidden outcome exists.
                Result<Unit>.Forbidden => TypedResults.NotFound(),
            };
        })
        .WithName("UpdateSleepHygieneNotes");

        return sleepDiary;
    }
}

public sealed record UpdateSleepHygieneNotesRequest(string? Notes);
