using System.Security.Claims;

using buddy.Common;
using buddy.Features.Groups;
using buddy.Features.Users;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.Medicines;

public static class ListTodaysDosesForGroupEndpoint
{
    public static RouteGroupBuilder MapListTodaysDosesForGroup(this RouteGroupBuilder medicines)
    {
        medicines.MapGet("/groups/{groupId:guid}/children/{childId:guid}/doses", async Task<Results<Ok<IReadOnlyCollection<MedicineDoseOccurrence>>, NotFound, ForbidHttpResult, BadRequest<string>>> (
            ClaimsPrincipal principal,
            Guid groupId,
            Guid childId,
            DateOnly from,
            DateOnly to,
            IMessageBus bus,
            CancellationToken cancellationToken) =>
        {
            var query = ListTodaysDosesForGroup.FromClaims(principal, new GroupId(groupId), new UserId(childId), from, to);
            var result = await bus.InvokeAsync<Result<IReadOnlyCollection<MedicineDoseOccurrence>>>(query, cancellationToken);

            return result switch
            {
                Result<IReadOnlyCollection<MedicineDoseOccurrence>>.Success(var occurrences) => TypedResults.Ok(occurrences),
                Result<IReadOnlyCollection<MedicineDoseOccurrence>>.Validation(var message) => TypedResults.BadRequest(message),
                Result<IReadOnlyCollection<MedicineDoseOccurrence>>.NotFound => TypedResults.NotFound(),
                // Reachable for a caller whose group policy resolves to no access at all.
                Result<IReadOnlyCollection<MedicineDoseOccurrence>>.Forbidden => TypedResults.Forbid(),
            };
        })
        .WithName("ListTodaysDosesForGroup");

        return medicines;
    }
}
