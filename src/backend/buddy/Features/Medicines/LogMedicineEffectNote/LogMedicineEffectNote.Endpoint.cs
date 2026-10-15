using System.Security.Claims;

using buddy.Common;
using buddy.Features.Users;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.Medicines;

public static class LogMedicineEffectNoteEndpoint
{
    public static RouteGroupBuilder MapLogMedicineEffectNote(this RouteGroupBuilder medicines)
    {
        medicines.MapPut("/children/{childId:guid}/effect-notes/{date}", async Task<Results<Ok<MedicineEffectNoteResponse>, NotFound, ForbidHttpResult, BadRequest<ErrorEnvelope>>> (
            ClaimsPrincipal principal,
            Guid childId,
            DateOnly date,
            LogMedicineEffectNoteRequest request,
            IMessageBus bus,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            // A null element can't reach the validator (sorting the ratings would throw).
            // RespectNullableAnnotations doesn't cover collection elements, so this stays.
            if (request.Effects?.Any(r => r is null) == true || request.SideEffects?.Any(r => r is null) == true)
            {
                return TypedResults.BadRequest(Common.Validation.ValidationProblem.Of("effects and sideEffects must not contain null entries.").ToEnvelope(httpContext));
            }

            var command = LogMedicineEffectNote.FromClaims(principal, new UserId(childId), date, request);
            var result = await bus.InvokeAsync<Result<MedicineEffectNoteResponse>>(command, cancellationToken);

            return result switch
            {
                Result<MedicineEffectNoteResponse>.Success(var note) => TypedResults.Ok(note),
                Result<MedicineEffectNoteResponse>.Forbidden => TypedResults.Forbid(),
                Result<MedicineEffectNoteResponse>.Validation(var problem) => TypedResults.BadRequest(problem.ToEnvelope(httpContext)),
                Result<MedicineEffectNoteResponse>.NotFound => TypedResults.NotFound(),
            };
        })
        .WithName("LogMedicineEffectNote");

        return medicines;
    }
}

public sealed record LogMedicineEffectNoteRequest(
    IReadOnlyList<EffectRating>? Effects = null,
    IReadOnlyList<SideEffectRating>? SideEffects = null,
    TimeOnly? WoreOffAt = null,
    string? Note = null);
