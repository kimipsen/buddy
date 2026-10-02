using System.Security.Claims;

using buddy.Common;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.PrintTemplates;

public static class ReplacePrintTemplateRowsEndpoint
{
    public static RouteGroupBuilder MapReplacePrintTemplateRows(this RouteGroupBuilder printTemplates)
    {
        printTemplates.MapPut("/{templateId:guid}/rows", async Task<Results<Ok<PrintTemplateResponse>, NotFound, BadRequest<ErrorEnvelope>>> (
            ClaimsPrincipal principal,
            Guid templateId,
            ReplacePrintTemplateRowsRequest request,
            IMessageBus bus,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            // Null entries can't reach the validator (its row rules would dereference them). An
            // omitted label reads as empty, which a Blank row allows and every other kind rejects.
            if (request.Rows?.Any(row => row is null) == true)
            {
                return TypedResults.BadRequest(Common.Validation.ValidationProblem.Of("rows must not contain null entries.").ToEnvelope(httpContext));
            }

            IReadOnlyList<PrintTemplateRow> rows = [.. (request.Rows ?? []).Select(row => row with { Label = row.Label ?? "" })];
            var command = ReplacePrintTemplateRows.FromClaims(principal, new PrintTemplateId(templateId), rows);
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
        .WithName("ReplacePrintTemplateRows");

        return printTemplates;
    }
}

// Rows bind straight to PrintTemplateRow: ids arrive as bare Guids (StronglyTypedIdJsonConverterFactory)
// and enums as ordinals, the same shape GetPrintTemplate returns.
public sealed record ReplacePrintTemplateRowsRequest(IReadOnlyList<PrintTemplateRow>? Rows);
