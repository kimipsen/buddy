using System.Security.Claims;

using buddy.Common;
using buddy.Features.Users;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.Medicines;

public static class ListTodaysDosesEndpoint
{
    public static RouteGroupBuilder MapListTodaysDoses(this RouteGroupBuilder medicines)
    {
        medicines.MapGet("/children/{childId:guid}/doses", async Task<Results<Ok<IReadOnlyCollection<MedicineDoseOccurrence>>, NotFound, BadRequest<string>>> (
            ClaimsPrincipal principal,
            Guid childId,
            DateOnly from,
            DateOnly to,
            IMessageBus bus,
            CancellationToken cancellationToken) =>
        {
            var query = ListTodaysDoses.FromClaims(principal, new UserId(childId), from, to);
            var result = await bus.InvokeAsync<Result<IReadOnlyCollection<MedicineDoseOccurrence>>>(query, cancellationToken);

            return result switch
            {
                Result<IReadOnlyCollection<MedicineDoseOccurrence>>.Success(var occurrences) => TypedResults.Ok(occurrences),
                Result<IReadOnlyCollection<MedicineDoseOccurrence>>.Validation(var message) => TypedResults.BadRequest(message),
                Result<IReadOnlyCollection<MedicineDoseOccurrence>>.NotFound => TypedResults.NotFound(),
                // CheckMark never returns Forbidden, so this is unreachable today -- there's no
                // ForbidHttpResult in this route's declared results, so it collapses to NotFound.
                Result<IReadOnlyCollection<MedicineDoseOccurrence>>.Forbidden => TypedResults.NotFound(),
            };
        })
        .WithName("ListTodaysDoses");

        return medicines;
    }
}
