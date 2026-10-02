using System.Security.Claims;

using buddy.Common;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.PrintTemplates;

public static class DeletePrintTemplateEndpoint
{
    public static RouteGroupBuilder MapDeletePrintTemplate(this RouteGroupBuilder printTemplates)
    {
        printTemplates.MapDelete("/{templateId:guid}", async Task<Results<NoContent, NotFound>> (
            ClaimsPrincipal principal,
            Guid templateId,
            IMessageBus bus,
            CancellationToken cancellationToken) =>
        {
            var result = await bus.InvokeAsync<Result<Unit>>(DeletePrintTemplate.FromClaims(principal, new PrintTemplateId(templateId)), cancellationToken);

            return result switch
            {
                Result<Unit>.Success => TypedResults.NoContent(),
                Result<Unit>.NotFound => TypedResults.NotFound(),
                // CheckManage never returns Forbidden and there's nothing to validate.
                Result<Unit>.Forbidden => TypedResults.NotFound(),
                Result<Unit>.Validation => TypedResults.NotFound(),
            };
        })
        .WithName("DeletePrintTemplate");

        return printTemplates;
    }
}
