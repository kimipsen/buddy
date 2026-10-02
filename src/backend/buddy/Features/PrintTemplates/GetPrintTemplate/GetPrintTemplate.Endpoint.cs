using System.Security.Claims;

using buddy.Common;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.PrintTemplates;

public static class GetPrintTemplateEndpoint
{
    public static RouteGroupBuilder MapGetPrintTemplate(this RouteGroupBuilder printTemplates)
    {
        printTemplates.MapGet("/{templateId:guid}", async Task<Results<Ok<PrintTemplateResponse>, NotFound>> (
            ClaimsPrincipal principal,
            Guid templateId,
            IMessageBus bus,
            CancellationToken cancellationToken) =>
        {
            var query = GetPrintTemplate.FromClaims(principal, new PrintTemplateId(templateId));
            var result = await bus.InvokeAsync<Result<PrintTemplateResponse>>(query, cancellationToken);

            return result switch
            {
                Result<PrintTemplateResponse>.Success(var template) => TypedResults.Ok(template),
                Result<PrintTemplateResponse>.NotFound => TypedResults.NotFound(),
                // CheckManage never returns Forbidden and there's nothing to validate.
                Result<PrintTemplateResponse>.Forbidden => TypedResults.NotFound(),
                Result<PrintTemplateResponse>.Validation => TypedResults.NotFound(),
            };
        })
        .WithName("GetPrintTemplate");

        return printTemplates;
    }
}
