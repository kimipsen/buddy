using System.Security.Claims;

using buddy.Common;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.PrintTemplates;

public static class UpdatePrintTemplateLayoutEndpoint
{
    public static RouteGroupBuilder MapUpdatePrintTemplateLayout(this RouteGroupBuilder printTemplates)
    {
        printTemplates.MapPatch("/{templateId:guid}/layout", async Task<Results<Ok<PrintTemplateResponse>, NotFound, BadRequest<ErrorEnvelope>>> (
            ClaimsPrincipal principal,
            Guid templateId,
            UpdatePrintTemplateLayoutRequest request,
            IMessageBus bus,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var command = UpdatePrintTemplateLayout.FromClaims(
                principal, new PrintTemplateId(templateId), request.PaperSize, request.DefaultStartWeekday, request.ShowWeekNumber);
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
        .WithName("UpdatePrintTemplateLayout");

        return printTemplates;
    }
}

public sealed record UpdatePrintTemplateLayoutRequest(PaperSize PaperSize, DayOfWeek DefaultStartWeekday, bool ShowWeekNumber);
