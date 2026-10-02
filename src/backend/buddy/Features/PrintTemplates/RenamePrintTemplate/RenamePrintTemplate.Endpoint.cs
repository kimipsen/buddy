using System.Security.Claims;

using buddy.Common;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.PrintTemplates;

public static class RenamePrintTemplateEndpoint
{
    public static RouteGroupBuilder MapRenamePrintTemplate(this RouteGroupBuilder printTemplates)
    {
        printTemplates.MapPatch("/{templateId:guid}/name", async Task<Results<Ok<PrintTemplateResponse>, NotFound, BadRequest<ErrorEnvelope>>> (
            ClaimsPrincipal principal,
            Guid templateId,
            RenamePrintTemplateRequest request,
            IMessageBus bus,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var command = RenamePrintTemplate.FromClaims(principal, new PrintTemplateId(templateId), request.Name);
            var result = await bus.InvokeAsync<Result<PrintTemplateResponse>>(command, cancellationToken);

            return result switch
            {
                Result<PrintTemplateResponse>.Success(var template) => TypedResults.Ok(template),
                Result<PrintTemplateResponse>.Validation(var problem) => TypedResults.BadRequest(problem.ToEnvelope(httpContext)),
                Result<PrintTemplateResponse>.NotFound => TypedResults.NotFound(),
                // CheckManage never returns Forbidden -- not in this route's declared results.
                Result<PrintTemplateResponse>.Forbidden => TypedResults.NotFound(),
            };
        })
        .WithName("RenamePrintTemplate");

        return printTemplates;
    }
}

public sealed record RenamePrintTemplateRequest(string Name);
