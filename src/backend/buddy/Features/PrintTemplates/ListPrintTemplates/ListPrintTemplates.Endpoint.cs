using System.Security.Claims;

using buddy.Common;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.PrintTemplates;

public static class ListPrintTemplatesEndpoint
{
    public static RouteGroupBuilder MapListPrintTemplates(this RouteGroupBuilder printTemplates)
    {
        printTemplates.MapGet("/", async Task<Results<Ok<IReadOnlyCollection<PrintTemplateSummary>>, NotFound>> (
            ClaimsPrincipal principal,
            IMessageBus bus,
            CancellationToken cancellationToken) =>
        {
            var result = await bus.InvokeAsync<Result<IReadOnlyCollection<PrintTemplateSummary>>>(ListPrintTemplates.FromClaims(principal), cancellationToken);

            return result switch
            {
                Result<IReadOnlyCollection<PrintTemplateSummary>>.Success(var templates) => TypedResults.Ok(templates),
                Result<IReadOnlyCollection<PrintTemplateSummary>>.NotFound => TypedResults.NotFound(),
                // The handler never produces these -- neither is in this route's declared results.
                Result<IReadOnlyCollection<PrintTemplateSummary>>.Forbidden => TypedResults.NotFound(),
                Result<IReadOnlyCollection<PrintTemplateSummary>>.Validation => TypedResults.NotFound(),
            };
        })
        .WithName("ListPrintTemplates");

        return printTemplates;
    }
}
