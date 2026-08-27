using System.Security.Claims;

using buddy.Common;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.TaskLibrary;

public static class ArchiveTaskTemplateEndpoint
{
    public static RouteGroupBuilder MapArchiveTaskTemplate(this RouteGroupBuilder taskTemplates)
    {
        taskTemplates.MapDelete("/{templateId:guid}", async Task<Results<NoContent, NotFound, ForbidHttpResult>> (
            ClaimsPrincipal principal,
            Guid templateId,
            IMessageBus bus,
            CancellationToken cancellationToken) =>
        {
            var command = ArchiveTaskTemplate.FromClaims(principal, new TaskTemplateId(templateId));
            var result = await bus.InvokeAsync<Result<Unit>>(command, cancellationToken);

            return result switch
            {
                Result<Unit>.Success => TypedResults.NoContent(),
                Result<Unit>.Forbidden => TypedResults.Forbid(),
                Result<Unit>.NotFound => TypedResults.NotFound(),
                // ArchiveTaskTemplateHandler never produces Validation -- there's no BadRequest in
                // this route's declared results, so this collapses to NotFound like the others
                // (same convention ArchiveMealEndpoint uses).
                Result<Unit>.Validation => TypedResults.NotFound(),
            };
        })
        .WithName("ArchiveTaskTemplate");

        return taskTemplates;
    }
}
