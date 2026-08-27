using System.Security.Claims;

using buddy.Common;
using buddy.Features.Users;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.TaskLibrary;

public static class ListTaskTemplatesEndpoint
{
    public static RouteGroupBuilder MapListTaskTemplates(this RouteGroupBuilder taskTemplates)
    {
        taskTemplates.MapGet("/children/{childId:guid}", async Task<Results<Ok<IReadOnlyCollection<TaskTemplateResponse>>, NotFound, ForbidHttpResult>> (
            ClaimsPrincipal principal,
            Guid childId,
            IMessageBus bus,
            CancellationToken cancellationToken) =>
        {
            var result = await bus.InvokeAsync<Result<IReadOnlyCollection<TaskTemplate>>>(ListTaskTemplates.FromClaims(principal, new UserId(childId)), cancellationToken);

            return result switch
            {
                Result<IReadOnlyCollection<TaskTemplate>>.Success(var templates) =>
                    TypedResults.Ok<IReadOnlyCollection<TaskTemplateResponse>>([.. templates.Select(TaskTemplateResponse.FromTaskTemplate)]),
                Result<IReadOnlyCollection<TaskTemplate>>.Forbidden => TypedResults.Forbid(),
                Result<IReadOnlyCollection<TaskTemplate>>.NotFound => TypedResults.NotFound(),
                // ListTaskTemplatesHandler never produces Validation -- there's no BadRequest in
                // this route's declared results, so this collapses to NotFound like the others
                // (same convention ListMealsEndpoint uses).
                Result<IReadOnlyCollection<TaskTemplate>>.Validation => TypedResults.NotFound(),
            };
        })
        .WithName("ListTaskTemplates");

        return taskTemplates;
    }
}
