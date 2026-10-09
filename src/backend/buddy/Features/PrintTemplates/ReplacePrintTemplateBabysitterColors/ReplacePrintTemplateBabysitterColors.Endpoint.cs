using System.Security.Claims;

using buddy.Common;
using buddy.Features.Babysitters;
using buddy.Features.Calendars;
using buddy.Features.Users;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.PrintTemplates;

public static class ReplacePrintTemplateBabysitterColorsEndpoint
{
    public static RouteGroupBuilder MapReplacePrintTemplateBabysitterColors(this RouteGroupBuilder printTemplates)
    {
        printTemplates.MapPut("/{templateId:guid}/babysitter-colors", async Task<Results<Ok<PrintTemplateResponse>, NotFound, BadRequest<ErrorEnvelope>>> (
            ClaimsPrincipal principal,
            Guid templateId,
            ReplacePrintTemplateBabysitterColorsRequest request,
            IMessageBus bus,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            // RespectNullableAnnotations doesn't cover collection elements, so this stays.
            if (request.Colors.Any(c => c is null))
            {
                return TypedResults.BadRequest(Common.Validation.ValidationProblem.Of("colors must not contain null entries.").ToEnvelope(httpContext));
            }

            var colors = request.Colors
                .Select(c => new BabysitterColor(new UserId(c.GuardianId), new BabysitterId(c.BabysitterId), new Color(c.Color.Trim())))
                .ToList();
            var command = ReplacePrintTemplateBabysitterColors.FromClaims(principal, new PrintTemplateId(templateId), colors);
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
        .WithName("ReplacePrintTemplateBabysitterColors");

        return printTemplates;
    }
}

public sealed record ReplacePrintTemplateBabysitterColorsRequest(IReadOnlyList<BabysitterColorRequest> Colors);

public sealed record BabysitterColorRequest(Guid GuardianId, Guid BabysitterId, string Color);
